using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Raiz {
public class WebhookRequest {
    public string Method {get;set;} public string Target {get;set;}
    public Dictionary<string,string> Headers {get;set;} public byte[] Body {get;set;}
}
public class WebhookReply {
    public int Status {get;set;} public string Text {get;set;}
    public WebhookReply(int status,string text){Status=status;Text=text;}
}
// Local receiver only. A separately configured HTTPS tunnel forwards /whatsapp here.
// It does not expose files, the dashboard, tokens, or a general remote-control API.
public class WebhookServer : IDisposable {
    readonly TcpListener listener;readonly Func<WebhookRequest,Task<WebhookReply>> handler;
    readonly SemaphoreSlim concurrency=new SemaphoreSlim(4,4);readonly object gate=new object();
    readonly HashSet<TcpClient> clients=new HashSet<TcpClient>();
    volatile bool stopped;DateTime rateStart=DateTime.UtcNow;int requests;
    public int Port {get {return ((IPEndPoint)listener.LocalEndpoint).Port;}}
    public WebhookServer(int port,Func<WebhookRequest,Task<WebhookReply>> handler){listener=new TcpListener(IPAddress.Loopback,port);this.handler=handler;}
    public void Start(){listener.Start(16);Task.Run((Func<Task>)AcceptLoop);}
    async Task AcceptLoop() {
        while(!stopped) {
            TcpClient client;try{client=await listener.AcceptTcpClientAsync().ConfigureAwait(false);}catch{break;}
            if(!concurrency.Wait(0)){client.Close();continue;}
            lock(gate)clients.Add(client);
            BeginServe(client);
        }
    }
    void BeginServe(TcpClient client){Task.Run(()=>Serve(client));}
    bool RateAllowed(){lock(gate){if(DateTime.UtcNow-rateStart>TimeSpan.FromMinutes(1)){rateStart=DateTime.UtcNow;requests=0;}return ++requests<=120;}}
    void Serve(TcpClient client) {
        try {
            client.ReceiveTimeout=5000;client.SendTimeout=5000;
            using(client)using(var deadline=new Timer(_=>{try{client.Close();}catch{}},null,12000,Timeout.Infinite))using(var stream=client.GetStream())using(var buffered=new BufferedStream(stream,8192)) {
                WebhookReply response;
                try {
                    if(!RateAllowed())response=new WebhookReply(429,"Too many requests");
                    else {
                        var request=ReadRequest(buffered);
                        var work=handler(request);
                        if(!work.Wait(TimeSpan.FromSeconds(5)))response=new WebhookReply(503,"Try again later");
                        else response=work.GetAwaiter().GetResult();
                    }
                }catch(InvalidRequest ex){response=new WebhookReply(ex.Code,"Invalid request");}
                catch{response=new WebhookReply(503,"Try again later");}
                var body=Encoding.UTF8.GetBytes(response.Text??"");
                var header=Encoding.ASCII.GetBytes("HTTP/1.1 "+response.Status+" Response\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n\r\n");
                buffered.Write(header,0,header.Length);buffered.Write(body,0,body.Length);buffered.Flush();
            }
        }catch{}finally{lock(gate)clients.Remove(client);client.Close();concurrency.Release();}
    }
    class InvalidRequest:Exception {public int Code;public InvalidRequest(int code){Code=code;}}
    static string Line(Stream stream,int limit) {
        var bytes=new List<byte>();
        while(true) {
            int b=stream.ReadByte();if(b<0)throw new InvalidRequest(400);
            if(b==13){if(stream.ReadByte()!=10)throw new InvalidRequest(400);return Encoding.ASCII.GetString(bytes.ToArray());}
            if(b==10||b>127||bytes.Count>=limit)throw new InvalidRequest(400);bytes.Add((byte)b);
        }
    }
    static byte[] Exactly(Stream stream,int count) {
        var data=new byte[count];int offset=0;
        while(offset<count){int n=stream.Read(data,offset,count-offset);if(n<=0)throw new InvalidRequest(400);offset+=n;}return data;
    }
    static WebhookRequest ReadRequest(Stream stream) {
        var first=Line(stream,4096).Split(' ');
        if(first.Length!=3||first[2]!="HTTP/1.1"||!first[1].StartsWith("/")||first[1].StartsWith("//"))throw new InvalidRequest(400);
        if(first[0]!="GET"&&first[0]!="POST")throw new InvalidRequest(405);
        var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);int total=0;
        while(true) {
            string line=Line(stream,8192);total+=line.Length+2;if(total>16384)throw new InvalidRequest(431);if(line=="")break;
            int colon=line.IndexOf(':');if(colon<=0||char.IsWhiteSpace(line[0]))throw new InvalidRequest(400);
            string key=line.Substring(0,colon).Trim(),value=line.Substring(colon+1).Trim();if(headers.ContainsKey(key))throw new InvalidRequest(400);headers.Add(key,value);
        }
        byte[] body=new byte[0];string transfer,length;
        bool hasTransfer=headers.TryGetValue("Transfer-Encoding",out transfer),hasLength=headers.TryGetValue("Content-Length",out length);
        if(hasTransfer&&hasLength)throw new InvalidRequest(400);
        if(hasTransfer) {
            if(!transfer.Equals("chunked",StringComparison.OrdinalIgnoreCase))throw new InvalidRequest(400);
            using(var result=new MemoryStream()) {
                while(true) {
                    string sizeText=Line(stream,128).Split(';')[0];int size;
                    if(!int.TryParse(sizeText,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out size)||size<0)throw new InvalidRequest(400);
                    if(result.Length+size>262144)throw new InvalidRequest(413);
                    if(size==0){int trailer=0;while(true){string end=Line(stream,8192);trailer+=end.Length+2;if(trailer>16384)throw new InvalidRequest(431);if(end=="")break;}break;}
                    var block=Exactly(stream,size);result.Write(block,0,block.Length);if(Line(stream,0)!="")throw new InvalidRequest(400);
                }
                body=result.ToArray();
            }
        } else if(hasLength) {
            int count;if(!int.TryParse(length,NumberStyles.None,CultureInfo.InvariantCulture,out count)||count<0)throw new InvalidRequest(400);
            if(count>262144)throw new InvalidRequest(413);body=Exactly(stream,count);
        } else if(first[0]=="POST")throw new InvalidRequest(411);
        if(first[0]=="GET"&&body.Length!=0)throw new InvalidRequest(400);
        return new WebhookRequest{Method=first[0],Target=first[1],Headers=headers,Body=body};
    }
    public void Dispose() {
        stopped=true;listener.Stop();lock(gate){foreach(var c in clients)c.Close();}
    }
}
}
