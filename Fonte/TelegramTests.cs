using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raiz;

public static class TelegramTests {
    static DateTime Now=new DateTime(2026,10,29,20,0,0,DateTimeKind.Utc);
    const string Token="123456789:FAKE_TEST_TOKEN_abcdefghijklmnopqrstuvwxyz";
    static TelegramSecrets Secret(){return new TelegramSecrets{Token=Token,BotId=123456789,BotUsername="raiz_test_bot",ChatId=5000000001,UserId=5000000001,PairedUtc=Now.AddHours(-1)};}
    static Settings Setup(out Plant p){p=new Plant{Name="Jade",Scientific="Crassula ovata"};var s=new Settings();s.Plants.Add(p);s.ActiveId=p.LocalId;return s;}
    static bool Reject(Action a){try{a();return false;}catch(ApiException){return true;}}
    static TelegramUpdate Pair(TelegramSecrets s){return new TelegramUpdate{UpdateId=10,Kind="message",ChatId=5000000001,UserId=5000000001,PrivateChat=true,Text="/start "+s.PairCode,MessageUtc=Now};}
    static TelegramUpdate Reply(NotificationEvent e){return new TelegramUpdate{UpdateId=20,Kind="callback",ChatId=5000000001,UserId=5000000001,PrivateChat=true,CallbackId="callback-"+e.Id,Data=TelegramRules.Prefix+e.Id,MessageId=42,MessageBotId=123456789};}
    class Mock:HttpMessageHandler {
        public int Code=200,Calls;public string Content,Method,Url;public bool Hook;
        public string Body="{\"ok\":true,\"result\":{\"message_id\":42,\"chat\":{\"id\":5000000001}}}";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
            Calls++;Url=request.RequestUri.AbsoluteUri;Method=request.RequestUri.Segments.Last();Content=request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            string body=Method=="getMe"?"{\"ok\":true,\"result\":{\"id\":123456789,\"is_bot\":true,\"username\":\"raiz_test_bot\"}}":Method=="getWebhookInfo"?"{\"ok\":true,\"result\":{\"url\":\""+(Hook?"https://example.test/existing":"")+"\"}}":Body;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Code){Content=new StringContent(body,Encoding.UTF8,"application/json")});
        }
    }
    public static void Run(Action<bool,string> check,string folder){
        var health=new TelegramConnectionState();
        check(health.Describe(false,false,false,false,false,Now).Title=="Bot desconectado","TG sem configuracao mostra bot desconectado");
        check(health.Describe(true,true,false,false,false,Now).Title=="Conectando ao bot","TG token salvo nao e prova de conexao atual");
        health.Success(Now);check(health.Describe(true,true,false,false,false,Now.AddSeconds(30)).Severity=="ok","TG resposta autenticada recente mostra bot conectado");
        check(health.Describe(true,true,false,false,false,Now.AddSeconds(76)).Severity!="ok","TG verificacao antiga nao mantem indicador conectado");
        health.Failure(0);check(health.Describe(true,true,false,false,false,Now).Title=="Bot desconectado","TG falha de rede remove indicador conectado imediatamente");
        health.Failure(401);check(health.Describe(true,true,true,false,false,Now).Body.Contains("Token inválido"),"TG token revogado explica desconexao");
        health.Failure(409);check(health.Describe(true,true,true,false,false,Now).Body.Contains("Outro programa"),"TG conflito de leitor mostra causa da desconexao");
        health.Success(Now);check(health.Describe(true,true,false,false,false,Now).Severity=="ok","TG reconexao restabelece indicador conectado");
        check(health.Describe(true,false,false,false,false,Now).Title.Contains("pausada"),"TG recepcao desativada nao aparece como conexao ativa");
        check(health.Describe(true,true,false,true,false,Now).Severity!="ok","TG demonstracao nao anuncia conexao real ativa");
        health.Reset();check(health.Describe(true,true,false,false,false,Now).Severity!="ok","TG reiniciar recepcao exige nova verificacao");
        Plant p;var settings=Setup(out p);var secret=Secret();var cal=new Calibration{Dry=392,Wet=281,Dark=22,Bright=316};
        TelegramRules.ValidateToken(Token);
        check(Reject(()=>TelegramRules.ValidateToken(Token+"/../getMe")),"TG rejeita token com desvio de URL");
        check(Reject(()=>TelegramRules.ValidateToken("token")),"TG rejeita token incompleto");
        check(TelegramRules.Id(5000000001L)==5000000001L&&TelegramRules.Id(-1)==0&&TelegramRules.Id("9007199254740992")==0,"TG conserva IDs de 64 bits dentro de 52 bits");
        check(!settings.Telegram.Enabled&&!settings.Telegram.PollEnabled,"TG comeca sem mensagens ou consultas automaticas");
        check(Reject(()=>TelegramRules.Prepare(settings,p,new TelegramSecrets(),Now)),"TG nao envia sem vinculo");
        TelegramRules.BeginPairing(secret,Now);var pair=Pair(secret);
        check(secret.PairCode.Length==8&&secret.PairExpiresUtc==Now.AddMinutes(10)&&!secret.Paired,"TG codigo aleatorio de oito digitos expira em dez minutos");
        pair.PrivateChat=false;check(!TelegramRules.Apply(settings,secret,pair,null,cal,Now).PairedNow,"TG nao vincula em grupo");pair.PrivateChat=true;
        pair.FromBot=true;check(!TelegramRules.Apply(settings,secret,pair,null,cal,Now).PairedNow,"TG nao vincula outro bot");pair.FromBot=false;
        pair.UserId++;check(!TelegramRules.Apply(settings,secret,pair,null,cal,Now).PairedNow,"TG exige chat privado do proprio usuario");pair.UserId--;
        pair.MessageUtc=Now.AddMinutes(-11);check(!TelegramRules.Apply(settings,secret,pair,null,cal,Now).PairedNow,"TG rejeita comando de vinculo antigo");pair.MessageUtc=Now;
        check(!TelegramRules.Apply(settings,secret,pair,null,cal,Now.AddMinutes(11)).PairedNow,"TG rejeita codigo expirado");
        var paired=TelegramRules.Apply(settings,secret,pair,null,cal,Now);
        check(paired.PairedNow&&paired.SecretChanged&&secret.Paired&&secret.PairCode==null,"TG vincula usuario com codigo e apaga codigo consumido");
        pair.UserId=pair.ChatId=6000000001;check(!TelegramRules.Apply(settings,secret,pair,null,cal,Now).PairedNow&&secret.ChatId==5000000001,"TG nao substitui vinculo com novo start");
        var lockout=Secret();TelegramRules.BeginPairing(lockout,Now);var wrong=Pair(lockout);wrong.Text="/start "+(lockout.PairCode=="11111111"?"22222222":"11111111");
        for(int i=0;i<5;i++)TelegramRules.Apply(settings,lockout,wrong,null,cal,Now);
        check(lockout.PairFailures==5&&lockout.PairCode==null&&!lockout.Paired,"TG bloqueia codigo apos cinco erros");
        var e=TelegramRules.Prepare(settings,p,secret,Now.AddMinutes(-1));e.State="sent";e.MetaMessageId="tg:42";var reply=Reply(e);
        var sample=new Sample{Soil=281,Light=200,Utc=Now};
        reply.UserId++;check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded,"TG outro usuario nao registra rega");reply.UserId--;
        reply.ChatId++;check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded,"TG outro chat nao registra rega");reply.ChatId--;
        reply.PrivateChat=false;check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded,"TG botao em grupo recusado");reply.PrivateChat=true;
        reply.MessageBotId++;check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded,"TG outro bot nao confirma rega");reply.MessageBotId--;
        reply.MessageId++;check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded,"TG mensagem diferente nao confirma o mesmo aviso");reply.MessageId--;
        reply.MessageId=0;check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded,"TG mensagem inacessivel sem ID nao gera rega");reply.MessageId=42;
        reply.Data="raiz_regou:forjado";check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded,"TG payload malformado recusado");reply.Data=TelegramRules.Prefix+e.Id;
        var applied=TelegramRules.Apply(settings,secret,reply,sample,cal,Now);
        check(applied.WateringAdded&&applied.Answer&&p.Care.Waterings.Count==1&&e.State=="confirmed","TG botao autorizado registra rega e confirma alerta");
        var record=p.Care.Waterings[0];
        check(record.Origin=="Telegram"&&record.FinishedUtc==Now&&record.VolumeUnknown&&!record.DrainageConfirmed,"TG usa horario recebido e nao inventa quantidade ou drenagem");
        check(!record.SensorReadingUnknown&&record.InitialReadingUnknown,"TG usa somente leitura atual conhecida");
        check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded&&p.Care.Waterings.Count==1,"TG callback repetido nao duplica log");
        reply.CallbackId="callback-test-2";check(!TelegramRules.Apply(settings,secret,reply,sample,cal,Now).WateringAdded&&p.Care.Waterings.Count==1,"TG segundo clique no mesmo botao nao duplica rega");
        var test=TelegramRules.Prepare(settings,p,secret,Now,true);var testReply=Reply(test);
        TelegramRules.Apply(settings,secret,testReply,sample,cal,Now);
        check(test.State=="confirmed"&&p.Care.Waterings.Count==1,"TG botao de teste nao cria rega real");
        var other=new Plant{Name="Outra"};settings.Plants.Add(other);settings.ActiveId=other.LocalId;
        var foreign=TelegramRules.Prepare(settings,p,secret,Now.AddSeconds(1));
        check(TelegramRules.Apply(settings,secret,Reply(foreign),sample,cal,Now.AddSeconds(2)).WateringAdded&&p.Care.Waterings.Last().SensorReadingUnknown,"TG nao empresta leitura de outro vaso");
        var old=TelegramRules.Prepare(settings,p,secret,Now.AddDays(-8));
        check(!TelegramRules.Apply(settings,secret,Reply(old),sample,cal,Now).WateringAdded,"TG recusa aviso de mais de sete dias");
        var earlier=TelegramRules.Prepare(settings,p,secret,Now.AddMinutes(1));var latest=TelegramRules.Prepare(settings,p,secret,Now.AddMinutes(2));
        check(!TelegramRules.Apply(settings,secret,Reply(earlier),null,cal,Now.AddMinutes(3)).WateringAdded,"TG aviso de ciclo anterior nao confirma novo ciclo");
        var waSecret=new WhatsAppSecrets{PhoneNumberId="1234567890",Recipient="5511000000000"};var wa=NotificationEngine.Prepare(settings,p,waSecret,Now.AddMinutes(2));wa.State="failed";
        check(NotificationEngine.Active(settings,p.LocalId,"Telegram")==latest&&NotificationEngine.Active(settings,p.LocalId)==wa,"TG falha do WhatsApp nao bloqueia o alerta Telegram");
        TelegramRules.Apply(settings,secret,Reply(latest),null,cal,Now.AddMinutes(3));
        check(wa.ConfirmedUtc.HasValue&&!NotificationEngine.CanAttempt(wa,Now.AddMinutes(4)),"TG confirmacao interrompe aviso paralelo do WhatsApp");
        int count=p.Care.Waterings.Count;
        var retry=new NotificationEvent{State="failed",RetryAllowed=true,RetryAfterUtc=Now.AddMinutes(10),LastAttemptUtc=Now,Attempts=1};check(!NotificationEngine.CanAttempt(retry,Now.AddMinutes(6))&&NotificationEngine.CanAttempt(retry,Now.AddMinutes(10)),"TG respeita retry_after mais longo que intervalo local");
        var inbound=new InboundEvent{Kind="button",Payload=wa.Id,FromHash=wa.RecipientHash,SenderHash=wa.SenderHash,MessageId="fake-wa-confirm",Utc=Now.AddMinutes(3)};
        check(NotificationEngine.Apply(settings,new[]{inbound},null,cal,Now.AddMinutes(3))==0&&p.Care.Waterings.Count==count,"TG e WhatsApp nao registram duas regas do mesmo ciclo");
        var raw=Json.Read<object>("[{\"update_id\":-1},{\"update_id\":0},{\"update_id\":21,\"message\":{\"chat\":{\"id\":5000000001,\"type\":\"private\"},\"from\":{\"id\":5000000001,\"is_bot\":false},\"text\":\"/start 12345678\",\"date\":1793304000}},{\"update_id\":22,\"callback_query\":{\"id\":\"q\",\"from\":{\"id\":5000000001},\"data\":\"opaque\",\"message\":{\"message_id\":42,\"chat\":{\"id\":5000000001,\"type\":\"private\"},\"from\":{\"id\":123456789}}}}]");
        var parsed=TelegramRules.ParseUpdates(raw);
        check(parsed.Count==3&&parsed[0].UpdateId==0&&parsed[1].ChatId==5000000001&&parsed[2].Kind=="callback"&&parsed[2].MessageBotId==secret.BotId,"TG parseia comandos e callbacks conservando IDs grandes");
        check(parsed[2].MessageUtc==null,"TG nao usa data da mensagem original como horario do clique");
        var mock=new Mock();using(var api=new TelegramTransport(mock)){
            var valid=api.ValidateBot(Token).GetAwaiter().GetResult();check(valid.BotId==secret.BotId&&valid.BotUsername==secret.BotUsername&&mock.Calls==2,"TG valida getMe e ausencia de webhook");
            check(mock.Url.StartsWith("https://api.telegram.org/bot")&&mock.Method=="getWebhookInfo","TG usa somente servidor oficial com HTTPS");
            mock.Hook=true;check(Reject(()=>api.ValidateBot(Token).GetAwaiter().GetResult())&&mock.Method=="getWebhookInfo","TG preserva webhook existente e recusa disputa");mock.Hook=false;
            check(Reject(()=>api.Call(Token,"deleteWebhook",new{}).GetAwaiter().GetResult()),"TG nao permite apagar webhook pelo transporte");
            mock.Body="{\"ok\":true,\"result\":[]}";api.Updates(secret,123).GetAwaiter().GetResult();
            check(mock.Content.Contains("\"offset\":123")&&mock.Content.Contains("\"timeout\":20")&&mock.Content.Contains("callback_query"),"TG long polling usa offset e consulta mensagens e botoes");
            mock.Body="{\"ok\":true,\"result\":{\"message_id\":42,\"chat\":{\"id\":5000000001}}}";var sent=api.Send(secret,e,5,null).GetAwaiter().GetResult();
            check(sent.State=="sent"&&sent.MessageId=="tg:42"&&!sent.RetryAllowed,"TG identifica mensagem criada sem afirmar leitura no celular");
            check(mock.Content.Contains("inline_keyboard")&&mock.Content.Contains(TelegramRules.Prefix+e.Id)&&!mock.Content.Contains(Token),"TG botao tem payload opaco e corpo sem token");
            mock.Body="{\"ok\":true,\"result\":{\"message_id\":42,\"chat\":{\"id\":99}}}";check(api.Send(secret,e,5,null).GetAwaiter().GetResult().State=="uncertain","TG chat inesperado deixa resultado indefinido sem repetir");
            mock.Body="{\"ok\":true,\"result\":{}}";check(api.Send(secret,e,5,null).GetAwaiter().GetResult().State=="uncertain","TG resposta sem ID nao confirma envio");
            mock.Code=429;mock.Body="{\"ok\":false,\"error_code\":429,\"description\":\""+Token+"\",\"parameters\":{\"retry_after\":600}}";var limited=api.Send(secret,e,5,null).GetAwaiter().GetResult();
            check(limited.State=="failed"&&limited.RetryAllowed&&limited.RetryAfterSeconds==600&&!limited.Detail.Contains(Token),"TG limita repeticoes e nao mostra descricao com segredo");
            mock.Code=401;mock.Body="{\"ok\":false,\"error_code\":401}";check(!api.Send(secret,e,5,null).GetAwaiter().GetResult().RetryAllowed,"TG token revogado nao provoca reenvio automatico");
            mock.Code=503;mock.Body="HTML invalid "+Token;var uncertain=api.Send(secret,e,5,null).GetAwaiter().GetResult();check(uncertain.State=="uncertain"&&!uncertain.Detail.Contains(Token),"TG erro indefinido sanitizado nao repete mensagem");
            mock.Code=200;mock.Body="{\"ok\":true,\"result\":true}";api.Answer(secret,"callback-1","Rega registrada").GetAwaiter().GetResult();check(mock.Method=="answerCallbackQuery"&&mock.Content.Contains("callback-1"),"TG responde ao botao apos registro");
        }
        var isolated=Path.Combine(folder,"telegram");var store=new Store(isolated);store.Settings.Plants=settings.Plants;store.Settings.Notifications=settings.Notifications;store.Settings.Telegram.NextOffset=23;store.SaveTelegramSecrets(secret);store.Save();
        var loaded=new Store(isolated);var loadedSecret=loaded.LoadTelegramSecrets();
        check(loadedSecret.Token==Token&&loadedSecret.Paired&&loadedSecret.ChatId==5000000001,"TG DPAPI recupera token e vinculo no mesmo usuario Windows");
        check(loaded.Settings.Telegram.NextOffset==23&&loaded.Settings.Plants[0].Care.Waterings.Count==count,"TG offset e regas persistem juntos");
        string publicData=File.ReadAllText(Path.Combine(isolated,"configuracao.json"))+File.ReadAllText(Path.Combine(isolated,"regas.csv"));
        check(!publicData.Contains(Token)&&!publicData.Contains("5000000001")&&publicData.Contains("Telegram"),"TG configuracao e CSV nao expoem token ou chat pessoal");
        check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(isolated,"telegram.protegido"))).Contains(Token),"TG segredo no disco esta cifrado");
        check(publicData.Contains("nao informado")||publicData.Contains("não informado")||p.Care.Waterings.All(x=>x.VolumeUnknown),"TG logs mantem quantidade desconhecida");
    }
}

