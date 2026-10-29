using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Raiz {
public class WhatsAppOptions {
    public bool Enabled {get;set;} public bool ReceiverEnabled {get;set;}
    public string ApiVersion {get;set;} public string TemplateName {get;set;} public string Language {get;set;}
    public string PublicWebhookUrl {get;set;} public int LocalPort {get;set;}
    public DateTime? VerifiedUtc {get;set;} public string VerifiedConfig {get;set;}
    public WhatsAppOptions(){ApiVersion="";TemplateName="raiz_rega";Language="pt_BR";LocalPort=8788;}
}
public class WhatsAppSecrets {
    public string AccessToken {get;set;} public string PhoneNumberId {get;set;} public string Recipient {get;set;}
    public string AppSecret {get;set;} public string VerifyToken {get;set;}
}
public class NotificationEvent {
    public string Channel {get;set;}
    public string Id {get;set;} public string PlantId {get;set;} public string PlantName {get;set;}
    public string RecipientHash {get;set;} public string SenderHash {get;set;}
    public List<string> RecipientAliases {get;set;}
    public DateTime CreatedUtc {get;set;} public DateTime? LastAttemptUtc {get;set;}
    public DateTime? RetryAfterUtc {get;set;}
    public DateTime? ConfirmedUtc {get;set;} public DateTime? ClosedUtc {get;set;}
    public int Attempts {get;set;} public string State {get;set;} public string Detail {get;set;}
    public string MetaMessageId {get;set;} public bool IsTest {get;set;} public bool RetryAllowed {get;set;}
    public string ConfirmMessageId {get;set;}
    public NotificationEvent(){RecipientAliases=new List<string>();Channel="WhatsApp";}
}
public class SendResult {
    public int RetryAfterSeconds {get;set;}
    public string State {get;set;} public string MessageId {get;set;} public string Detail {get;set;} public bool RetryAllowed {get;set;}
    public string[] RecipientAliases {get;set;}
}
public class InboundEvent {
    public string Kind {get;set;} public string MessageId {get;set;} public string Payload {get;set;}
    public string FromHash {get;set;} public string SenderHash {get;set;} public string ContextId {get;set;}
    public DateTime Utc {get;set;} public string Delivery {get;set;}
}
public static class WhatsAppRules {
    public const string PayloadPrefix="raiz_regou:";
    public static readonly DateTime Epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
    public static string Phone(string value) {
        value=(value??"").Trim();if(!Regex.IsMatch(value,@"^\+?[0-9 ()-]{8,30}$"))throw new ApiException("Informe o celular com código do país e DDD, por exemplo +55 DDD número.");
        string digits=Regex.Replace(value,@"\D","");if(!Regex.IsMatch(digits,@"^[1-9][0-9]{7,14}$"))throw new ApiException("Número de WhatsApp inválido. Use código do país, DDD e número.");return digits;
    }
    public static string Hash(string value){using(var sha=SHA256.Create())return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value??"")));}
    public static string Hex(byte[] bytes){return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}
    public static bool Equal(string a,string b) {
        if(a==null||b==null)return false;byte[] x=Encoding.UTF8.GetBytes(a),y=Encoding.UTF8.GetBytes(b);if(x.Length!=y.Length)return false;
        int diff=0;for(int i=0;i<x.Length;i++)diff|=x[i]^y[i];return diff==0;
    }
    public static string NewVerifyToken(){var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);return Hex(bytes);}
    public static string Challenge(string target,string token) {
        try {
            var q=HttpUtility.ParseQueryString(target.Contains("?")?target.Substring(target.IndexOf('?')+1):"");
            foreach(var key in new[]{"hub.mode","hub.verify_token","hub.challenge"})if(q.GetValues(key)==null||q.GetValues(key).Length!=1)return null;
            return q["hub.mode"]=="subscribe"&&Equal(q["hub.verify_token"],token)&&Regex.IsMatch(q["hub.challenge"]??"",@"^[0-9]{1,1000}$")?q["hub.challenge"]:null;
        }catch{return null;}
    }
    public static string Fingerprint(WhatsAppOptions o,WhatsAppSecrets s){return Hash(o.PublicWebhookUrl+"|"+o.LocalPort+"|"+s.PhoneNumberId+"|"+s.Recipient+"|"+s.VerifyToken+"|"+s.AppSecret);}
    public static void ValidateReceiver(WhatsAppOptions o,WhatsAppSecrets s) {
        if(o.LocalPort<1024||o.LocalPort>65535)throw new ApiException("A porta local deve estar entre 1024 e 65535.");
        if(!Regex.IsMatch(s.PhoneNumberId??"",@"^[0-9]{5,30}$"))throw new ApiException("Informe o Phone Number ID do remetente, exibido no painel da Meta.");
        Phone(s.Recipient);
        if(string.IsNullOrWhiteSpace(s.AppSecret)||s.AppSecret.Length<16||s.AppSecret.Length>1024)throw new ApiException("Informe o App Secret do aplicativo Meta para verificar as confirmações.");
        if(string.IsNullOrWhiteSpace(s.VerifyToken)||s.VerifyToken.Length<24||s.VerifyToken.Length>1024)throw new ApiException("Gere ou informe um token de verificação com pelo menos 24 caracteres.");
    }
    public static void ValidateSend(WhatsAppOptions o,WhatsAppSecrets s) {
        ValidateReceiver(o,s);
        if(string.IsNullOrWhiteSpace(s.AccessToken)||s.AccessToken.Length<20||s.AccessToken.Length>8192||s.AccessToken.Any(char.IsControl))throw new ApiException("Informe o token de acesso da API oficial do WhatsApp.");
        if(!Regex.IsMatch(o.ApiVersion??"",@"^v[0-9]{1,3}\.0$"))throw new ApiException("Copie do painel Meta a versão da Graph API, no formato vNN.0.");
        if(!Regex.IsMatch(o.TemplateName??"",@"^[a-z0-9_]{1,512}$"))throw new ApiException("Informe o nome do modelo aprovado, usando letras minúsculas, números e sublinhado.");
        if(!Regex.IsMatch(o.Language??"",@"^[a-z]{2}(_[A-Z]{2})?$"))throw new ApiException("Informe o idioma aprovado do modelo, como pt_BR.");
        Uri uri;IPAddress address;
        if(!Uri.TryCreate(o.PublicWebhookUrl,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||uri.AbsolutePath!="/whatsapp"||uri.HostNameType!=UriHostNameType.Dns||uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||IPAddress.TryParse(uri.Host,out address))
            throw new ApiException("Informe o endereço HTTPS público do receptor, terminando em /whatsapp. O endereço local sozinho não recebe cliques do celular.");
    }
    static string Param(string value,int length){var text=Regex.Replace(value??"",@"\s+"," ").Trim();if(text.Length>length)text=text.Substring(0,length);return text==""?"Não informado":text;}
    public static object Payload(WhatsAppOptions o,WhatsAppSecrets s,NotificationEvent e,double? soilIndex,int? estimatedMl) {
        string water=estimatedMl.HasValue?"Estimativa aprendida: cerca de "+estimatedMl.Value+" mL; pare ao iniciar drenagem":"Primeira porção experimental: 30 mL; total ainda desconhecido";
        if(e.IsTest)water="TESTE de configuração; não há indicação de rega e o botão não registra uma rega real";
        return new{messaging_product="whatsapp",recipient_type="individual",to=Phone(s.Recipient),type="template",template=new{name=o.TemplateName,language=new{code=o.Language},components=new object[]{
            new{type="body",parameters=new object[]{new{type="text",text=Param((e.IsTest?"[TESTE] ":"")+e.PlantName,120)},new{type="text",text=soilIndex.HasValue?soilIndex.Value.ToString("0",CultureInfo.InvariantCulture):"teste"},new{type="text",text=Param(water,500)}}},
            new{type="button",sub_type="quick_reply",index="0",parameters=new[]{new{type="payload",payload=PayloadPrefix+e.Id}}}
        }}};
    }
    public static bool VerifySignature(byte[] body,string signature,string secret) {
        if(body==null||body.Length>262144||string.IsNullOrEmpty(secret)||!Regex.IsMatch(signature??"",@"^sha256=[a-fA-F0-9]{64}$"))return false;
        using(var hmac=new HMACSHA256(Encoding.UTF8.GetBytes(secret)))return Equal("sha256="+Hex(hmac.ComputeHash(body)),signature.ToLowerInvariant());
    }
    public static List<InboundEvent> ParseWebhook(byte[] body,string signature,WhatsAppSecrets secrets) {
        if(!VerifySignature(body,signature,secrets.AppSecret))throw new ApiException("Assinatura inválida.");
        var result=new List<InboundEvent>();Dictionary<string,object> root;
        try{root=Json.Read<Dictionary<string,object>>(new UTF8Encoding(false,true).GetString(body));}catch{throw new ApiException("Mensagem inválida.");}
        if(root==null)throw new ApiException("Mensagem inválida.");
        if(Json.Str(Json.Get(root,"object"))!="whatsapp_business_account")return result;
        foreach(var entry in Json.List(Json.Get(root,"entry")))foreach(var change in Json.List(Json.Get(Json.Map(entry),"changes"))) {
            var value=Json.Map(Json.Get(Json.Map(change),"value"));
            var metadata=Json.Map(Json.Get(value,"metadata"));string sender=Json.Str(Json.Get(metadata,"phone_number_id"));
            if(sender!=secrets.PhoneNumberId)continue;
            foreach(var item in Json.List(Json.Get(value,"messages"))) {
                var m=Json.Map(item);string type=Json.Str(Json.Get(m,"type")),payload="";
                if(type=="button")payload=Json.Str(Json.Get(Json.Map(Json.Get(m,"button")),"payload"));
                if(type=="interactive")payload=Json.Str(Json.Get(Json.Map(Json.Get(Json.Map(Json.Get(m,"interactive")),"button_reply")),"id"));
                if(!Regex.IsMatch(payload,@"^raiz_regou:[a-f0-9]{32}$"))continue;
                string from=Json.Str(Json.Get(m,"from")),id=Json.Str(Json.Get(m,"id"));
                if(!Regex.IsMatch(from,@"^[1-9][0-9]{7,14}$")||id.Length==0||id.Length>300)continue;
                long timestamp;DateTime time;
                if(!long.TryParse(Json.Str(Json.Get(m,"timestamp")),out timestamp))continue;
                try{time=Epoch.AddSeconds(timestamp);}catch{continue;}
                result.Add(new InboundEvent{Kind="confirmation",MessageId=id,Payload=payload.Substring(PayloadPrefix.Length),FromHash=Hash(from),SenderHash=Hash(sender),ContextId=Json.Str(Json.Get(Json.Map(Json.Get(m,"context")),"id")),Utc=time});
            }
            foreach(var item in Json.List(Json.Get(value,"statuses"))) {
                var m=Json.Map(item);string recipient=Json.Str(Json.Get(m,"recipient_id")),delivery=Json.Str(Json.Get(m,"status"));
                if(!Regex.IsMatch(recipient,@"^[1-9][0-9]{7,14}$")||!new[]{"sent","delivered","read","failed"}.Contains(delivery))continue;
                result.Add(new InboundEvent{Kind="status",MessageId=Json.Str(Json.Get(m,"id")),Delivery=delivery,SenderHash=Hash(sender),FromHash=Hash(recipient)});
            }
        }
        return result;
    }
}
public class WhatsAppTransport : IDisposable {
    readonly HttpClient client;
    public WhatsAppTransport():this(new HttpClientHandler{AllowAutoRedirect=false}){}
    public WhatsAppTransport(HttpMessageHandler handler){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(20)};}
    public async Task<SendResult> Send(WhatsAppOptions o,WhatsAppSecrets s,NotificationEvent e,double? index,int? ml) {
        WhatsAppRules.ValidateSend(o,s);
        try {
            using(var request=new HttpRequestMessage(HttpMethod.Post,"https://graph.facebook.com/"+o.ApiVersion+"/"+s.PhoneNumberId+"/messages")) {
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",s.AccessToken);
                request.Content=new StringContent(Json.Write(WhatsAppRules.Payload(o,s,e,index,ml)),Encoding.UTF8,"application/json");
                using(var response=await client.SendAsync(request).ConfigureAwait(false)) {
                    var body=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Dictionary<string,object> data;try{data=Json.Read<Dictionary<string,object>>(body);}catch{data=new Dictionary<string,object>();}
                    if(response.IsSuccessStatusCode) {
                        var identities=new List<string>{WhatsAppRules.Phone(s.Recipient)};
                        foreach(var contact in Json.List(Json.Get(data,"contacts"))) {
                            var item=Json.Map(contact);string input=Json.Str(Json.Get(item,"input")),canonical=Json.Str(Json.Get(item,"wa_id"));
                            try{if(WhatsAppRules.Phone(input)==WhatsAppRules.Phone(s.Recipient)&&Regex.IsMatch(canonical,@"^[1-9][0-9]{7,14}$"))identities.Add(canonical);}catch{}
                        }
                        var first=Json.Map(Json.List(Json.Get(data,"messages")).FirstOrDefault());string id=Json.Str(Json.Get(first,"id"));
                        return id!=""?new SendResult{State="accepted",MessageId=id,RecipientAliases=identities.Distinct().ToArray(),Detail="Aceito pela Meta; entrega ainda não confirmada."}:new SendResult{State="uncertain",RecipientAliases=identities.Distinct().ToArray(),Detail="Resposta sem identificador. Confira o celular; o envio não será repetido automaticamente."};
                    }
                    int code=(int)(Json.Num(Json.Get(Json.Map(Json.Get(data,"error")),"code"))??0);
                    int http=(int)response.StatusCode;
                    return new SendResult{State=http>=500?"uncertain":"failed",RetryAllowed=http==429,Detail=http==401||http==403?"Acesso recusado. Confira token, permissões e conta Meta.":http==429?"Limite de envio atingido; nova tentativa em pelo menos 5 minutos.":http>=500?"Falha do serviço; resultado do envio indefinido. Não haverá repetição automática.":"Meta recusou o envio (HTTP "+http+", código "+code+"). Confira modelo, parâmetros e destinatário autorizado."};
                }
            }
        }catch{return new SendResult{State="uncertain",Detail="Conexão interrompida durante o envio; resultado indefinido. Confira o celular antes de repetir."};}
    }
    public void Dispose(){client.Dispose();}
}
public static class NotificationEngine {
    public static string Channel(NotificationEvent e){return string.IsNullOrEmpty(e.Channel)?"WhatsApp":e.Channel;}
    public static NotificationEvent Active(Settings settings,string plantId,string channel="WhatsApp"){return settings.Notifications.LastOrDefault(x=>x.PlantId==plantId&&Channel(x)==channel&&!x.IsTest&&!x.ClosedUtc.HasValue);}
    public static NotificationEvent Prepare(Settings settings,Plant p,WhatsAppSecrets secrets,DateTime now,bool test=false) {
        var e=new NotificationEvent{Id=Guid.NewGuid().ToString("N"),PlantId=p.LocalId,PlantName=p.Name,CreatedUtc=now,State="prepared",IsTest=test,RecipientHash=WhatsAppRules.Hash(WhatsAppRules.Phone(secrets.Recipient)),SenderHash=WhatsAppRules.Hash(secrets.PhoneNumberId)};
        e.RecipientAliases.Add(e.RecipientHash);settings.Notifications.Add(e);return e;
    }
    public static bool RecipientMatches(NotificationEvent e,string hash){return e.RecipientHash==hash||(e.RecipientAliases!=null&&e.RecipientAliases.Contains(hash));}
    public static void AcceptAliases(NotificationEvent e,SendResult result){if(result.RecipientAliases==null)return;if(e.RecipientAliases==null)e.RecipientAliases=new List<string>();foreach(var id in result.RecipientAliases){string hash=WhatsAppRules.Hash(id);if(!e.RecipientAliases.Contains(hash))e.RecipientAliases.Add(hash);}}
    public static bool CanAttempt(NotificationEvent e,DateTime now) {
        if(e==null||e.ClosedUtc.HasValue||e.ConfirmedUtc.HasValue||e.Attempts>=3)return false;
        if(e.RetryAfterUtc.HasValue&&now<e.RetryAfterUtc.Value)return false;
        if(e.State=="sending"||e.State=="uncertain"||new[]{"accepted","sent","delivered","read"}.Contains(e.State))return false;
        if(e.State=="failed"&&!e.RetryAllowed)return false;
        return !e.LastAttemptUtc.HasValue||now-e.LastAttemptUtc.Value>=TimeSpan.FromMinutes(5);
    }
    public static bool ResolveIfMoist(Settings settings,Plant p,Calibration c,IList<Sample> samples,DateTime now) {
        if(p==null)return false;var active=settings.Notifications.Where(x=>x.PlantId==p.LocalId&&!x.IsTest&&!x.ClosedUtc.HasValue).ToList();if(active.Count==0)return false;
        if(CareEngine.Persistent(samples,now,30,s=>CareEngine.ReliableSoil(s,c,s.Utc)&&c.Soil(s.Soil).Value>CareEngine.Trigger(p)+10)){foreach(var e in active){e.ClosedUtc=now;if(!e.ConfirmedUtc.HasValue)e.State="resolved";}return true;}return false;
    }
    public static void AcknowledgeLocal(Settings settings,Plant p,DateTime time){AcknowledgeCycle(settings,p.LocalId,time,"Rega confirmada no app.");}
    public static void AcknowledgeCycle(Settings settings,string plantId,DateTime time,string detail){foreach(var e in settings.Notifications.Where(x=>x.PlantId==plantId&&!x.IsTest).GroupBy(Channel).Select(g=>g.Last()))if(!e.ConfirmedUtc.HasValue&&(!e.ClosedUtc.HasValue||e.State=="resolved")){e.ConfirmedUtc=time;e.State="confirmed";e.Detail=detail;}}
    public static void ResetFailed(Settings settings,DateTime now,string channel="WhatsApp"){foreach(var e in settings.Notifications)if(Channel(e)==channel&&e.State=="failed"&&!e.ClosedUtc.HasValue&&!e.ConfirmedUtc.HasValue){e.ClosedUtc=now;e.Detail="Configuração salva novamente; nova tentativa permitida para um próximo alerta.";}}
    public static int Apply(Settings settings,IEnumerable<InboundEvent> incoming,Sample s,Calibration c,DateTime now) {
        int added=0;
        foreach(var message in incoming) {
            if(message.Kind=="status") {
                var delivery=settings.Notifications.LastOrDefault(x=>Channel(x)=="WhatsApp"&&x.MetaMessageId==message.MessageId&&RecipientMatches(x,message.FromHash)&&x.SenderHash==message.SenderHash);
                if(delivery!=null&&!delivery.ConfirmedUtc.HasValue&&!delivery.ClosedUtc.HasValue) {
                    int oldRank=Rank(delivery.State),newRank=Rank(message.Delivery);
                    if(message.Delivery=="failed"&&oldRank<3){delivery.State="failed";delivery.RetryAllowed=false;delivery.Detail="A Meta informou falha de entrega. Confira o destinatário e a configuração.";}
                    else if(newRank>oldRank){delivery.State=message.Delivery;delivery.Detail=message.Delivery=="delivered"?"Entrega confirmada pela Meta.":message.Delivery=="read"?"Leitura confirmada pela Meta.":"Envio confirmado pela Meta.";}
                }continue;
            }
            var e=settings.Notifications.LastOrDefault(x=>Channel(x)=="WhatsApp"&&x.Id==message.Payload);
            if(e==null||(e.ClosedUtc.HasValue&&e.State!="resolved")||e.ConfirmedUtc.HasValue||!RecipientMatches(e,message.FromHash)||e.SenderHash!=message.SenderHash||message.Utc<e.CreatedUtc.AddMinutes(-5)||message.Utc>now.AddMinutes(5)||now-message.Utc>TimeSpan.FromDays(7))continue;
            if(!e.IsTest&&settings.Notifications.Any(x=>!x.IsTest&&Channel(x)=="WhatsApp"&&x.PlantId==e.PlantId&&x.CreatedUtc>e.CreatedUtc))continue;
            // The signed opaque payload and bound recipient identify the alert. Meta may
            // represent context IDs in another namespace; context is not an auth credential.
            var p=settings.Plants.Find(x=>x.LocalId==e.PlantId);if(p==null)continue;
            if(!e.IsTest&&CareEngine.ConfirmAlreadyWatered(p,settings.ActiveId==p.LocalId?s:null,c,"WhatsApp","wa:"+message.MessageId,message.Utc,now))added++;
            e.ConfirmedUtc=message.Utc;e.ConfirmMessageId=message.MessageId;e.State="confirmed";e.Detail=e.IsTest?"Botão de teste recebido. Nenhuma rega real foi registrada.":"Já reguei recebido pelo WhatsApp e registrado.";
            if(!e.IsTest)AcknowledgeCycle(settings,p.LocalId,message.Utc,"Rega já confirmada em um canal vinculado.");
        }return added;
    }
    static int Rank(string status){return status=="read"?4:status=="delivered"?3:status=="sent"?2:status=="accepted"?1:0;}
}
}
