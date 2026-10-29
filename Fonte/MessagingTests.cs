using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raiz;

public static class MessagingTests {
    static readonly DateTime Now=new DateTime(2026,10,29,18,0,0,DateTimeKind.Utc);
    static WhatsAppSecrets Secrets(){return new WhatsAppSecrets{AccessToken="unit-test-token-not-a-real-meta-token",PhoneNumberId="12345678901",Recipient="5511000000000",AppSecret="unit-test-app-secret-0123456789",VerifyToken="unit-test-verify-token-0123456789"};}
    static WhatsAppOptions Options(){return new WhatsAppOptions{ApiVersion="v23.0",TemplateName="raiz_rega",Language="pt_BR",PublicWebhookUrl="https://example.test/whatsapp",ReceiverEnabled=true};}
    static string Sign(byte[] body,string secret){using(var h=new HMACSHA256(Encoding.UTF8.GetBytes(secret)))return "sha256="+WhatsAppRules.Hex(h.ComputeHash(body));}
    static bool Reject(Action a){try{a();return false;}catch(ApiException){return true;}}
    static byte[] Body(NotificationEvent e,WhatsAppSecrets s,string id="wamid.reply1",string from=null,string context=null,string type="button") {
        object message=type=="button"?(object)new{from=from??s.Recipient,id=id,timestamp=((long)(Now-WhatsAppRules.Epoch).TotalSeconds).ToString(),type="button",button=new{payload=WhatsAppRules.PayloadPrefix+e.Id,text="Já reguei"},context=new{id=context??e.MetaMessageId??""}}:
            new{from=from??s.Recipient,id=id,timestamp=((long)(Now-WhatsAppRules.Epoch).TotalSeconds).ToString(),type="interactive",interactive=new{type="button_reply",button_reply=new{id=WhatsAppRules.PayloadPrefix+e.Id,title="Já reguei"}},context=new{id=context??e.MetaMessageId??""}};
        return Encoding.UTF8.GetBytes(Json.Write(new{@object="whatsapp_business_account",entry=new[]{new{changes=new[]{new{field="messages",value=new{messaging_product="whatsapp",metadata=new{phone_number_id=s.PhoneNumberId},messages=new[]{message}}}}}}}));
    }
    static Settings Setup(out Plant p){p=new Plant{Name="Jade",Scientific="Crassula ovata"};var s=new Settings();s.Plants.Add(p);s.ActiveId=p.LocalId;return s;}
    class MockHttp : HttpMessageHandler {
        public int Code=200;public string Body="{\"messages\":[{\"id\":\"wamid.accepted\"}]}";
        public string Url,Authorization,Content;public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;Url=request.RequestUri.AbsoluteUri;Authorization=request.Headers.Authorization.ToString();Content=request.Content.ReadAsStringAsync().GetAwaiter().GetResult();return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Code){Content=new StringContent(Body,Encoding.UTF8,"application/json")});}
    }
    public static void Run(Action<bool,string> check,string folder) {
        var secret=Secrets();var options=Options();var cal=new Calibration{Dry=392,Wet=281,Dark=22,Bright=316};Plant p;var settings=Setup(out p);
        check(WhatsAppRules.Phone("+55 (11) 00000-0000")==secret.Recipient,"normaliza celular internacional sem expor em logs");
        check(Reject(()=>WhatsAppRules.Phone("+55 abc 123456")),"rejeita telefone com letras");
        check(Reject(()=>WhatsAppRules.ValidateSend(new WhatsAppOptions(),secret)),"API sem versao ou endereco nao e ativada por suposicao");
        WhatsAppRules.ValidateSend(options,secret);
        var invalid=Options();invalid.PublicWebhookUrl="http://example.test/whatsapp";
        check(Reject(()=>WhatsAppRules.ValidateSend(invalid,secret)),"exige HTTPS no retorno publico");
        invalid=Options();invalid.PublicWebhookUrl="https://localhost/whatsapp";
        check(Reject(()=>WhatsAppRules.ValidateSend(invalid,secret)),"endereco apenas local nao e tratado como webhook publico");
        invalid=Options();invalid.PublicWebhookUrl="https://example.test/whatsapp?token=segredo";
        check(Reject(()=>WhatsAppRules.ValidateSend(invalid,secret)),"nao coloca segredos em query do endereco publico");
        var badSecret=Secrets();badSecret.PhoneNumberId="12345/../../evil";
        check(Reject(()=>WhatsAppRules.ValidateSend(options,badSecret)),"Phone Number ID nao permite desvio de caminho da API");
        check(WhatsAppRules.NewVerifyToken().Length==64,"token de verificacao usa 256 bits aleatorios");
        string verify="/whatsapp?hub.mode=subscribe&hub.verify_token="+secret.VerifyToken+"&hub.challenge=123456";
        check(WhatsAppRules.Challenge(verify,secret.VerifyToken)=="123456","handshake devolve somente challenge autorizado");
        check(WhatsAppRules.Challenge(verify,"wrong-token")==null,"handshake com token errado e recusado");
        check(WhatsAppRules.Challenge(verify+"&hub.challenge=999",secret.VerifyToken)==null,"rejeita parametros de verificacao duplicados");
        var e=NotificationEngine.Prepare(settings,p,secret,Now.AddMinutes(-1));e.MetaMessageId="wamid.sent1";e.State="accepted";
        check(!NotificationEngine.CanAttempt(e,Now),"alerta aceito nao e repetido em cada amostra seca");
        e.State="uncertain";check(!NotificationEngine.CanAttempt(e,Now),"timeout indefinido nao causa repeticao automatica");
        e.State="sending";check(!NotificationEngine.CanAttempt(e,Now),"envio em andamento nao e duplicado");
        e.State="failed";e.RetryAllowed=false;check(!NotificationEngine.CanAttempt(e,Now),"falha definitiva aguarda correcao da configuracao");
        e.RetryAllowed=true;e.Attempts=1;e.LastAttemptUtc=Now;
        check(!NotificationEngine.CanAttempt(e,Now.AddMinutes(4))&&NotificationEngine.CanAttempt(e,Now.AddMinutes(5)),"retry de limite tem intervalo de cinco minutos");
        e.Attempts=3;check(!NotificationEngine.CanAttempt(e,Now.AddHours(1)),"limita tentativas do mesmo alerta");
        e.State="accepted";
        var body=Body(e,secret);string signature=Sign(body,secret.AppSecret);
        check(WhatsAppRules.VerifySignature(body,signature,secret.AppSecret),"verifica HMAC sobre bytes exatos do webhook");
        check(!WhatsAppRules.VerifySignature(body,signature,"wrong-secret"),"recusa assinatura com segredo incorreto");
        var changed=(byte[])body.Clone();changed[changed.Length-2]^=1;
        check(!WhatsAppRules.VerifySignature(changed,signature,secret.AppSecret),"qualquer alteracao do corpo invalida assinatura");
        check(!WhatsAppRules.VerifySignature(body,null,secret.AppSecret),"POST sem assinatura nao altera dados");
        check(!WhatsAppRules.VerifySignature(new byte[262145],signature,secret.AppSecret),"limita tamanho de webhook antes do parse");
        var nullJson=Encoding.UTF8.GetBytes("null");check(Reject(()=>WhatsAppRules.ParseWebhook(nullJson,Sign(nullJson,secret.AppSecret),secret)),"JSON nulo assinado e rejeitado com erro controlado");
        var parsed=WhatsAppRules.ParseWebhook(body,signature,secret);
        check(parsed.Count==1&&parsed[0].Payload==e.Id,"botao do template preserva correlacao com a planta");
        var foreign=Body(e,secret,from:"5511999999999");
        check(NotificationEngine.Apply(settings,WhatsAppRules.ParseWebhook(foreign,Sign(foreign,secret.AppSecret),secret),null,cal,Now)==0,"outro celular nao pode confirmar a rega");
        var wrongSender=Secrets();wrongSender.PhoneNumberId="99999999999";var wrongBody=Body(e,wrongSender);
        check(WhatsAppRules.ParseWebhook(wrongBody,Sign(wrongBody,secret.AppSecret),secret).Count==0,"outro remetente empresarial nao altera a planta");
        Plant contextPlant;var contextSettings=Setup(out contextPlant);var contextEvent=NotificationEngine.Prepare(contextSettings,contextPlant,secret,Now.AddMinutes(-1));contextEvent.MetaMessageId="wamid.original";
        var mismatch=Body(contextEvent,secret,context:"another-id-namespace");
        check(NotificationEngine.Apply(contextSettings,WhatsAppRules.ParseWebhook(mismatch,Sign(mismatch,secret.AppSecret),secret),null,cal,Now)==1,"payload assinado e destinatario correlacionam botao sem depender do namespace de context");
        int added=NotificationEngine.Apply(settings,parsed,null,cal,Now);
        check(added==1&&p.Care.Waterings.Count==1,"clique autentico do WhatsApp registra uma rega");
        var record=p.Care.Waterings.Single();
        check(record.Origin=="WhatsApp"&&record.VolumeUnknown&&record.SensorReadingUnknown,"confirmacao sem medicao nao inventa volume ou indice");
        check(record.InitialReadingUnknown,"botao rapido nao inventa medicao anterior a rega");
        check(record.PlantName=="Jade","log preserva nome da planta no momento da confirmacao");
        check(CareEngine.Estimate(p,cal)==null,"confirmacao simples nao ensina dose nao medida");
        check(NotificationEngine.Apply(settings,parsed,null,cal,Now)==0&&p.Care.Waterings.Count==1,"reenvio de webhook e idempotente");
        var secondTap=Body(e,secret,id:"wamid.reply2");
        check(NotificationEngine.Apply(settings,WhatsAppRules.ParseWebhook(secondTap,Sign(secondTap,secret.AppSecret),secret),null,cal,Now)==0,"segundo clique no mesmo aviso tambem nao duplica rega");
        check(e.ConfirmedUtc.HasValue&&!NotificationEngine.CanAttempt(e,Now),"ja reguei pausa avisos do mesmo ciclo");
        var wet=Enumerable.Range(0,31).Select(i=>new Sample{Soil=281,Light=260,Utc=Now.AddSeconds(i-30)}).ToList();
        check(NotificationEngine.ResolveIfMoist(settings,p,cal,wet,Now)&&NotificationEngine.Active(settings,p.LocalId)==null,"solo umido persistente rearma apenas um ciclo futuro");
        check(!NotificationEngine.ResolveIfMoist(settings,p,cal,wet,Now),"fechamento de ciclo nao grava repetidamente");
        Plant laterPlant;var later=Setup(out laterPlant);var laterEvent=NotificationEngine.Prepare(later,laterPlant,secret,Now.AddMinutes(-3));laterEvent.State="accepted";
        NotificationEngine.ResolveIfMoist(later,laterPlant,cal,wet,Now.AddSeconds(-1));
        laterEvent.ClosedUtc=Now.AddSeconds(-20);laterEvent.State="resolved";
        var afterWet=Body(laterEvent,secret);
        check(NotificationEngine.Apply(later,WhatsAppRules.ParseWebhook(afterWet,Sign(afterWet,secret.AppSecret),secret),null,cal,Now)==1,"botao ainda registra rega apos sensor ja ter ficado umido");
        Plant localPlant;var local=Setup(out localPlant);var localEvent=NotificationEngine.Prepare(local,localPlant,secret,Now.AddMinutes(-3));localEvent.State="resolved";localEvent.ClosedUtc=Now.AddMinutes(-1);NotificationEngine.AcknowledgeLocal(local,localPlant,Now);var localBody=Body(localEvent,secret);
        check(NotificationEngine.Apply(local,WhatsAppRules.ParseWebhook(localBody,Sign(localBody,secret.AppSecret),secret),null,cal,Now)==0,"confirmacao no app impede duplicata pelo botao do celular mesmo apos solo umido");
        Plant stalePlant;var stale=Setup(out stalePlant);var oldEvent=NotificationEngine.Prepare(stale,stalePlant,secret,Now.AddHours(-1));oldEvent.State="resolved";oldEvent.ClosedUtc=Now.AddMinutes(-40);NotificationEngine.Prepare(stale,stalePlant,secret,Now.AddMinutes(-2));var oldBody=Body(oldEvent,secret);
        check(NotificationEngine.Apply(stale,WhatsAppRules.ParseWebhook(oldBody,Sign(oldBody,secret.AppSecret),secret),null,cal,Now)==0,"botao de ciclo anterior nao confirma nova rega");
        var test=NotificationEngine.Prepare(settings,p,secret,Now.AddSeconds(-2),true);var testBody=Body(test,secret);
        check(NotificationEngine.Apply(settings,WhatsAppRules.ParseWebhook(testBody,Sign(testBody,secret.AppSecret),secret),null,cal,Now)==0&&test.ConfirmedUtc.HasValue,"botao de mensagem de teste nao cria rega real");
        Plant switchPlant;var switched=Setup(out switchPlant);var switchEvent=NotificationEngine.Prepare(switched,switchPlant,secret,Now.AddMinutes(-1));switched.ActiveId="outro-vaso";var switchBody=Body(switchEvent,secret);
        NotificationEngine.Apply(switched,WhatsAppRules.ParseWebhook(switchBody,Sign(switchBody,secret.AppSecret),secret),new Sample{Soil=281,Light=250,Utc=Now},cal,Now);
        check(switchPlant.Care.Waterings.Single().SensorReadingUnknown,"medicao de outro vaso nao e anexada ao clique antigo");
        var direct=new Plant{Name="Regada sem sensores",Scientific="Crassula ovata"};
        check(CareEngine.ConfirmAlreadyWatered(direct,null,cal,"App","manual-1",Now,Now)&&!CareEngine.ConfirmAlreadyWatered(direct,null,cal,"App","manual-1",Now,Now),"botao manual tambem usa evento idempotente");
        var recovered=Json.Read<Settings>(Json.Write(settings));
        check(NotificationEngine.Apply(recovered,parsed,null,cal,Now)==0,"deduplicacao persiste depois de reiniciar o app");
        using(var mock=new MockHttp())using(var transport=new WhatsAppTransport(mock)) {
            var result=transport.Send(options,secret,e,5,120).GetAwaiter().GetResult();
            check(result.State=="accepted"&&result.MessageId=="wamid.accepted"&&!result.Detail.Contains("entregue"),"HTTP 200 nao e anunciado como entrega no celular");
            check(mock.Url=="https://graph.facebook.com/v23.0/12345678901/messages"&&mock.Authorization.StartsWith("Bearer "),"token vai no cabecalho apenas para Graph oficial");
            var payload=Json.Read<Dictionary<string,object>>(mock.Content);var template=Json.Map(Json.Get(payload,"template"));var components=Json.List(Json.Get(template,"components"));
            check(Json.Str(Json.Get(payload,"type"))=="template"&&components.Count==2&&mock.Content.Contains("quick_reply")&&mock.Content.Contains(e.Id),"alerta proativo usa template e botao com identificador unico");
            mock.Body="{\"contacts\":[{\"input\":\"5511000000000\",\"wa_id\":\"551100000000\"}],\"messages\":[{\"id\":\"wamid.canonical\"}]}";
            result=transport.Send(options,secret,e,5,null).GetAwaiter().GetResult();
            check(result.RecipientAliases.Contains("551100000000"),"mapeia wa_id canonico devolvido pela Meta para o numero consultado");
            NotificationEngine.AcceptAliases(e,result);check(NotificationEngine.RecipientMatches(e,WhatsAppRules.Hash("551100000000")),"correlacao aceita identidade canonica autenticada sem heuristica no telefone");
            mock.Body="{\"contacts\":[{\"input\":\"5511999999999\",\"wa_id\":\"551100000001\"}],\"messages\":[{\"id\":\"wamid.other\"}]}";
            result=transport.Send(options,secret,e,5,null).GetAwaiter().GetResult();check(!result.RecipientAliases.Contains("551100000001"),"nao autoriza alias de um contato diferente do solicitado");
            mock.Body="{}";result=transport.Send(options,secret,e,5,null).GetAwaiter().GetResult();check(result.State=="uncertain","resposta sem ID nao e tratada como envio comprovado");
            mock.Code=400;mock.Body="{\"error\":{\"code\":132000,\"message\":\"DO-NOT-LOG-SECRET\"}}";result=transport.Send(options,secret,e,5,null).GetAwaiter().GetResult();
            check(result.State=="failed"&&result.Detail.Contains("132000")&&!result.Detail.Contains("DO-NOT-LOG-SECRET"),"erros externos nao vazam corpo ou credenciais");
            mock.Code=429;result=transport.Send(options,secret,e,5,null).GetAwaiter().GetResult();check(result.RetryAllowed,"limite de API admite retry controlado");
            mock.Code=500;result=transport.Send(options,secret,e,5,null).GetAwaiter().GetResult();check(result.State=="uncertain"&&!result.RetryAllowed,"falha ambigua do servidor nao duplica mensagem");
        }
        var isolated=Path.Combine(folder,"messaging-"+Guid.NewGuid().ToString("N"));var store=new Store(isolated);store.Settings.Plants.Add(p);store.Settings.ActiveId=p.LocalId;store.Settings.Notifications.AddRange(settings.Notifications);store.SaveWhatsAppSecrets(secret);store.Save();
        check(store.LoadWhatsAppSecrets().AccessToken==secret.AccessToken,"credenciais WhatsApp fazem ida e volta com DPAPI");
        var config=File.ReadAllText(Path.Combine(isolated,"configuracao.json"));
        check(!config.Contains(secret.AccessToken)&&!config.Contains(secret.AppSecret)&&!config.Contains(secret.Recipient),"configuracao e alertas persistidos nao contem token ou celular em texto puro");
        var csv=File.ReadAllText(store.WateringLogPath);check(csv.Contains("\"WhatsApp\",\"\",")&&!csv.Contains(secret.AccessToken),"CSV de regas deixa volume desconhecido vazio e nao inclui segredo");
        store.Save();check(File.ReadAllText(store.WateringLogPath)==csv,"salvar novamente nao duplica linhas de rega");
        var crash=NotificationEngine.Prepare(store.Settings,p,secret,Now);crash.State="sending";store.Save();var loaded=new Store(isolated);
        check(loaded.Settings.Notifications.Last().State=="uncertain"&&!NotificationEngine.CanAttempt(loaded.Settings.Notifications.Last(),Now.AddDays(1)),"envio interrompido em reinicio fica indefinido e nao repete");
        check(PlotData.Visible(new[]{new Sample{Utc=Now.AddSeconds(-100)},new Sample{Utc=Now.AddSeconds(-10)},new Sample{Utc=Now.AddSeconds(1)}},Now,30).Count==1,"plotter respeita janela de tempo e exclui dados futuros");
        check(!PlotData.Join(new Sample{Utc=Now},new Sample{Utc=Now.AddSeconds(12)}),"plotter deixa lacuna quando a conexao para");
        check(PlotData.Value(new Sample{Soil=281},cal,true,true)==100&&PlotData.Value(new Sample{Soil=281},new Calibration(),true,true)==null,"plotter normaliza apenas quando ha calibracao");
        check(PlotData.Value(new Sample{Soil=0},cal,true,false)==0,"plotter conserva zero bruto como leitura valida");
        var pathRoot=Path.Combine(Path.GetFullPath(isolated),"path-test");Directory.CreateDirectory(pathRoot);var external=Path.Combine(pathRoot,"originais");Directory.CreateDirectory(external);
        check(AppPaths.DataFolder(pathRoot,null)==Path.Combine(pathRoot,"Dados"),"pacote portatil usa sua propria pasta Dados");
        File.WriteAllText(Path.Combine(pathRoot,"dados-local.txt"),external,Encoding.UTF8);
        check(AppPaths.DataFolder(pathRoot,null)==Path.GetFullPath(external),"nova versao pode usar os dados originais sem copiar ou perder registros");
        check(AppPaths.DataFolder(pathRoot,isolated)==Path.GetFullPath(isolated),"testes isolados nao reutilizam os dados do usuario");
        check(AppPaths.MutexName(external)==AppPaths.MutexName(external.ToUpperInvariant()),"trava de dados usa identidade de caminho consistente no Windows");
        HttpTests(check,secret,folder);
    }
    static string Exchange(int port,string raw) {
        using(var client=new TcpClient()){client.ReceiveTimeout=6000;client.SendTimeout=6000;client.Connect(IPAddress.Loopback,port);using(var stream=client.GetStream()){var bytes=Encoding.UTF8.GetBytes(raw);stream.Write(bytes,0,bytes.Length);using(var data=new MemoryStream()){stream.CopyTo(data);return Encoding.UTF8.GetString(data.ToArray());}}}
    }
    static void HttpTests(Action<bool,string> check,WhatsAppSecrets secret,string folder) {
        Plant p;var state=Setup(out p);var e=NotificationEngine.Prepare(state,p,secret,Now.AddMinutes(-1));e.State="accepted";var cal=new Calibration{Dry=392,Wet=281};var sync=new object();
        using(var server=new WebhookServer(0,request=> {
            if(request.Target.Split('?')[0]!="/whatsapp")return Task.FromResult(new WebhookReply(404,"Not found"));
            if(request.Method=="GET"){var challenge=WhatsAppRules.Challenge(request.Target,secret.VerifyToken);return Task.FromResult(new WebhookReply(challenge==null?403:200,challenge??"Forbidden"));}
            string signature;request.Headers.TryGetValue("X-Hub-Signature-256",out signature);if(!WhatsAppRules.VerifySignature(request.Body,signature,secret.AppSecret))return Task.FromResult(new WebhookReply(403,"Forbidden"));
            try{var events=WhatsAppRules.ParseWebhook(request.Body,signature,secret);lock(sync)NotificationEngine.Apply(state,events,null,cal,Now);return Task.FromResult(new WebhookReply(200,"OK"));}catch{return Task.FromResult(new WebhookReply(400,"Bad payload"));}
        })) {
            server.Start();int port=server.Port;var body=Body(e,secret);string payload=Encoding.UTF8.GetString(body);string sig=Sign(body,secret.AppSecret);
            var get=Exchange(port,"GET /whatsapp?hub.mode=subscribe&hub.verify_token="+secret.VerifyToken+"&hub.challenge=777 HTTP/1.1\r\nHost: localhost\r\n\r\n");
            check(get.StartsWith("HTTP/1.1 200")&&get.EndsWith("777"),"receptor HTTP local valida assinatura inicial da Meta");
            string post="POST /whatsapp HTTP/1.1\r\nHost: localhost\r\nX-Hub-Signature-256: "+sig+"\r\nContent-Length: "+body.Length+"\r\n\r\n"+payload;
            check(Exchange(port,post).StartsWith("HTTP/1.1 200")&&p.Care.Waterings.Count==1,"receptor real processa POST assinado e registra clique");
            check(Exchange(port,post).StartsWith("HTTP/1.1 200")&&p.Care.Waterings.Count==1,"retry HTTP completo nao duplica registro");
            string chunked="POST /whatsapp HTTP/1.1\r\nHost: localhost\r\nX-Hub-Signature-256: "+sig+"\r\nTransfer-Encoding: chunked\r\n\r\n"+body.Length.ToString("X")+"\r\n"+payload+"\r\n0\r\n\r\n";
            check(Exchange(port,chunked).StartsWith("HTTP/1.1 200")&&p.Care.Waterings.Count==1,"receptor aceita transferencia chunked de tunel sem duplicar");
            check(Exchange(port,post.Replace(sig,"sha256="+new string('0',64))).StartsWith("HTTP/1.1 403"),"receptor rejeita requisicao sem HMAC valido");
            check(Exchange(port,"POST /whatsapp HTTP/1.1\r\nHost: localhost\r\nContent-Length: 300000\r\n\r\n").StartsWith("HTTP/1.1 413"),"receptor limita corpo antes de alocar dados externos");
            check(Exchange(port,"POST /whatsapp HTTP/1.1\r\nHost: localhost\r\nContent-Length: 0\r\nTransfer-Encoding: chunked\r\n\r\n").StartsWith("HTTP/1.1 400"),"rejeita framing HTTP ambiguo");
            check(Exchange(port,"GET /arquivo/segredo HTTP/1.1\r\nHost: localhost\r\n\r\n").StartsWith("HTTP/1.1 404"),"receptor nao serve arquivos ou outros caminhos");
        }
    }
}
