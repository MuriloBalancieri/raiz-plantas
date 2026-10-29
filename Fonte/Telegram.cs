using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Raiz {
public class TelegramConnectionState {
    public DateTime? LastSuccessUtc {get;private set;} public bool Failed {get;private set;} public int ErrorCode {get;private set;}
    public void Reset(){LastSuccessUtc=null;Failed=false;ErrorCode=0;}
    public void Success(DateTime now){LastSuccessUtc=now;Failed=false;ErrorCode=0;}
    public void Failure(int code){Failed=true;ErrorCode=code;}
    public Advice Describe(bool configured,bool polling,bool paused,bool demo,bool configuring,DateTime now){
        if(demo)return new Advice("Conexão pausada","Saia da demonstração para conectar o bot.","pending");
        if(configuring)return new Advice("Verificando conexão do bot","Aguarde a resposta do Telegram.","pending");
        if(!configured)return new Advice("Bot desconectado","Configure seu bot na seção abaixo.","pending");
        if(Failed){string reason=ErrorCode==401?"Token inválido ou revogado.":ErrorCode==403?"Conversa com o bot bloqueada.":ErrorCode==409?"Outro programa ou webhook está usando este bot.":ErrorCode==429?"Limite temporário do Telegram; aguarde a nova tentativa.":"Sem resposta do Telegram. Confira a internet.";return new Advice("Bot desconectado",reason,"attention");}
        if(paused||!polling)return new Advice("Recepção do bot pausada","Use Receber respostas novamente para retomar.","pending");
        if(!LastSuccessUtc.HasValue)return new Advice("Conectando ao bot","Aguardando a primeira resposta do Telegram.","pending");
        if(now<LastSuccessUtc.Value||now-LastSuccessUtc.Value>TimeSpan.FromSeconds(75))return new Advice("Conexão do bot sem confirmação recente","Aguardando nova resposta do Telegram.","attention");
        return new Advice("Bot conectado","Última resposta às "+LastSuccessUtc.Value.ToLocalTime().ToString("HH:mm:ss")+". Recepção ativa.","ok");
    }
}
public class TelegramOptions {
    public bool Enabled {get;set;} public bool PollEnabled {get;set;} public long NextOffset {get;set;}
    public string BotFingerprint {get;set;} public DateTime? LastUpdateUtc {get;set;}
}
public class TelegramSecrets {
    public string Token {get;set;} public long BotId {get;set;} public string BotUsername {get;set;}
    public long ChatId {get;set;} public long UserId {get;set;} public DateTime? PairedUtc {get;set;}
    public string PairCode {get;set;} public DateTime? PairExpiresUtc {get;set;} public int PairFailures {get;set;}
    public bool Paired {get {return BotId>0&&ChatId>0&&UserId>0&&PairedUtc.HasValue;}}
}
public class TelegramUpdate {
    public long UpdateId {get;set;} public string Kind {get;set;} public long ChatId {get;set;} public long UserId {get;set;}
    public bool PrivateChat {get;set;} public bool FromBot {get;set;} public string Text {get;set;}
    public string CallbackId {get;set;} public string Data {get;set;} public long MessageId {get;set;}
    public long MessageBotId {get;set;} public DateTime? MessageUtc {get;set;}
}
public class TelegramApplyResult {
    public bool PairedNow {get;set;} public bool SecretChanged {get;set;} public bool WateringAdded {get;set;}
    public bool Answer {get;set;} public string Feedback {get;set;}
}
public class TelegramApiException:ApiException {
    public int Code {get;private set;} public int RetryAfter {get;private set;}
    public TelegramApiException(int code,int retryAfter=30):base(code==401?"Token do Telegram inválido ou revogado. Confira no BotFather.":code==403?"O bot não pode conversar com esse usuário. Inicie a conversa e confira se o bot está bloqueado.":code==409?"Outro leitor ou webhook está usando esse bot. Use um bot exclusivo para o Raiz; nenhuma configuração externa foi apagada.":code==429?"Limite do Telegram atingido. Aguarde antes de tentar novamente.":code==0?"Não foi possível concluir a consulta ao Telegram. Confira sua internet.":"O Telegram recusou a operação (código "+code+"). Confira a configuração do bot."){Code=code;RetryAfter=retryAfter;}
}
public static class TelegramRules {
    public const string Prefix="raiz_regou:";
    public static void ValidateToken(string token){if(!Regex.IsMatch(token??"",@"^[0-9]{5,20}:[A-Za-z0-9_-]{20,200}$"))throw new ApiException("Cole o token completo do bot criado no BotFather, somente no campo protegido do app.");}
    public static long Id(object value){long n;return long.TryParse(Json.Str(value),NumberStyles.Integer,CultureInfo.InvariantCulture,out n)&&n>=0&&n<=4503599627370495L?n:0;}
    public static string NewPairCode() {
        var bytes=new byte[4];uint n;using(var rng=RandomNumberGenerator.Create()){do{rng.GetBytes(bytes);n=BitConverter.ToUInt32(bytes,0);}while(n>=uint.MaxValue-uint.MaxValue%90000000u);}
        return (10000000u+n%90000000u).ToString(CultureInfo.InvariantCulture);
    }
    public static void BeginPairing(TelegramSecrets s,DateTime now){s.ChatId=0;s.UserId=0;s.PairedUtc=null;s.PairCode=NewPairCode();s.PairExpiresUtc=now.AddMinutes(10);s.PairFailures=0;}
    public static List<TelegramUpdate> ParseUpdates(object result) {
        var updates=new List<TelegramUpdate>();
        foreach(var item in Json.List(result)) {
            var root=Json.Map(item);object rawId=Json.Get(root,"update_id");long id;
            if(rawId==null||!long.TryParse(Json.Str(rawId),NumberStyles.Integer,CultureInfo.InvariantCulture,out id)||id<0||id>4503599627370495L)continue;
            var u=new TelegramUpdate{UpdateId=id,Kind="ignored"};
            var callback=Json.Map(Json.Get(root,"callback_query"));
            if(callback.Count>0) {
                var message=Json.Map(Json.Get(callback,"message"));var chat=Json.Map(Json.Get(message,"chat"));var user=Json.Map(Json.Get(callback,"from"));
                u.Kind="callback";u.ChatId=Id(Json.Get(chat,"id"));u.UserId=Id(Json.Get(user,"id"));u.FromBot=object.Equals(Json.Get(user,"is_bot"),true);u.PrivateChat=Json.Str(Json.Get(chat,"type"))=="private";
                u.CallbackId=Json.Str(Json.Get(callback,"id"));u.Data=Json.Str(Json.Get(callback,"data"));u.MessageId=Id(Json.Get(message,"message_id"));u.MessageBotId=Id(Json.Get(Json.Map(Json.Get(message,"from")),"id"));
            } else {
                var message=Json.Map(Json.Get(root,"message"));var chat=Json.Map(Json.Get(message,"chat"));var user=Json.Map(Json.Get(message,"from"));
                if(message.Count>0) {
                    u.Kind="message";u.ChatId=Id(Json.Get(chat,"id"));u.UserId=Id(Json.Get(user,"id"));u.PrivateChat=Json.Str(Json.Get(chat,"type"))=="private";u.FromBot=object.Equals(Json.Get(user,"is_bot"),true);u.Text=Json.Str(Json.Get(message,"text"));
                    long seconds;try{if(long.TryParse(Json.Str(Json.Get(message,"date")),out seconds))u.MessageUtc=WhatsAppRules.Epoch.AddSeconds(seconds);}catch{}
                }
            }
            updates.Add(u);
        }
        return updates.OrderBy(x=>x.UpdateId).ToList();
    }
    public static TelegramApplyResult Apply(Settings settings,TelegramSecrets s,TelegramUpdate u,Sample sample,Calibration cal,DateTime now) {
        var r=new TelegramApplyResult();
        if(u.Kind=="message"&&!s.Paired&&s.PairExpiresUtc.HasValue&&now<=s.PairExpiresUtc.Value&&s.PairFailures<5&&u.PrivateChat&&!u.FromBot&&u.ChatId>0&&u.ChatId==u.UserId&&u.MessageUtc.HasValue&&u.MessageUtc>=now.AddMinutes(-10)&&u.MessageUtc<=now.AddMinutes(5)) {
            var m=Regex.Match((u.Text??"").Trim(),@"^/start\s+([0-9]{8})$");
            if(m.Success) {
                r.SecretChanged=true;
                if(WhatsAppRules.Equal(m.Groups[1].Value,s.PairCode)){s.ChatId=u.ChatId;s.UserId=u.UserId;s.PairedUtc=now;s.PairCode=null;s.PairExpiresUtc=null;s.PairFailures=0;r.PairedNow=true;}
                else {s.PairFailures++;if(s.PairFailures>=5){s.PairCode=null;s.PairExpiresUtc=null;}}
            }
        }
        if(u.Kind!="callback"||!s.Paired||!u.PrivateChat||u.FromBot||u.ChatId!=s.ChatId||u.UserId!=s.UserId||u.CallbackId==null||u.CallbackId.Length==0||u.CallbackId.Length>256)return r;
        if(u.MessageId<=0||u.MessageBotId!=s.BotId)return r;
        if(!Regex.IsMatch(u.Data??"",@"^raiz_regou:[a-f0-9]{32}$"))return r;
        r.Answer=true;r.Feedback="Este aviso não está mais ativo. Confira os registros no app.";
        string payload=u.Data.Substring(Prefix.Length);
        var e=settings.Notifications.LastOrDefault(x=>NotificationEngine.Channel(x)=="Telegram"&&x.Id==payload);
        if(e==null||e.SenderHash!=WhatsAppRules.Hash(s.BotId.ToString(CultureInfo.InvariantCulture))||e.RecipientHash!=WhatsAppRules.Hash(s.ChatId.ToString(CultureInfo.InvariantCulture)))return r;
        if(!string.IsNullOrEmpty(e.MetaMessageId)&&e.MetaMessageId!="tg:"+u.MessageId.ToString(CultureInfo.InvariantCulture))return r;
        if(e.ConfirmedUtc.HasValue){r.Feedback=e.IsTest?"Teste já confirmado. Nenhuma rega real foi criada.":"Essa rega já está registrada no Raiz.";return r;}
        if((e.ClosedUtc.HasValue&&e.State!="resolved")||now-e.CreatedUtc>TimeSpan.FromDays(7)||e.CreatedUtc>now.AddMinutes(5))return r;
        if(!e.IsTest&&settings.Notifications.Any(x=>NotificationEngine.Channel(x)=="Telegram"&&!x.IsTest&&x.PlantId==e.PlantId&&x.CreatedUtc>e.CreatedUtc))return r;
        var p=settings.Plants.Find(x=>x.LocalId==e.PlantId);if(p==null)return r;
        if(!e.IsTest)r.WateringAdded=CareEngine.ConfirmAlreadyWatered(p,settings.ActiveId==p.LocalId?sample:null,cal,"Telegram","tg:"+u.CallbackId,now,now);
        e.ConfirmedUtc=now;e.ConfirmMessageId=u.CallbackId;e.State="confirmed";e.Detail=e.IsTest?"Botão de teste recebido pelo Telegram; sem rega real.":"Já reguei recebido pelo Telegram e salvo nos logs.";
        if(!e.IsTest)NotificationEngine.AcknowledgeCycle(settings,p.LocalId,now,"Rega já confirmada em um canal vinculado.");
        r.Feedback=e.IsTest?"Teste recebido. Nenhuma rega real foi criada.":"Rega registrada no Raiz. Obrigado!";return r;
    }
    public static NotificationEvent Prepare(Settings settings,Plant p,TelegramSecrets s,DateTime now,bool test=false) {
        if(!s.Paired)throw new ApiException("Vincule seu Telegram ao bot antes de enviar avisos.");
        var e=new NotificationEvent{Channel="Telegram",Id=Guid.NewGuid().ToString("N"),PlantId=p.LocalId,PlantName=p.Name,CreatedUtc=now,State="prepared",IsTest=test,RecipientHash=WhatsAppRules.Hash(s.ChatId.ToString(CultureInfo.InvariantCulture)),SenderHash=WhatsAppRules.Hash(s.BotId.ToString(CultureInfo.InvariantCulture))};settings.Notifications.Add(e);return e;
    }
    public static object Alert(TelegramSecrets s,NotificationEvent e,double? index,int? ml) {
        string text=e.IsTest?"TESTE do Raiz — "+e.PlantName+"\nEsta mensagem verifica a configuração. O botão não cria uma rega real.":"Sua planta "+e.PlantName+" atingiu o gatilho local de rega.\nÍndice relativo: "+(index.HasValue?index.Value.ToString("0",CultureInfo.InvariantCulture)+"%":"indisponível")+". Confira o substrato antes de regar.\n"+(ml.HasValue?"Água: cerca de "+ml.Value+" mL, estimativa aprendida. Pare ao começar a drenar.":"Sem dose aprendida: primeira porção experimental de 30 mL; total desconhecido.")+"\nDepois da rega, toque em Já reguei para registrar.";
        return new{chat_id=s.ChatId,text=text,reply_markup=new{inline_keyboard=new[]{new[]{new{text="Já reguei",callback_data=Prefix+e.Id}}}}};
    }
}
public class TelegramTransport:IDisposable {
    readonly HttpClient client;
    static readonly string[] Methods={"getMe","getWebhookInfo","getUpdates","sendMessage","answerCallbackQuery"};
    public TelegramTransport():this(new HttpClientHandler{AllowAutoRedirect=false}){}
    public TelegramTransport(HttpMessageHandler handler){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(35)};}
    public async Task<object> Call(string token,string method,object body) {
        TelegramRules.ValidateToken(token);if(!Methods.Contains(method))throw new ApiException("Método de Telegram não permitido.");
        try {
            using(var request=new HttpRequestMessage(HttpMethod.Post,"https://api.telegram.org/bot"+token+"/"+method)) {
                request.Content=new StringContent(Json.Write(body),Encoding.UTF8,"application/json");
                using(var response=await client.SendAsync(request).ConfigureAwait(false)) {
                    var text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);if(text.Length>4000000)throw new TelegramApiException(0);
                    Dictionary<string,object> root;try{root=Json.Read<Dictionary<string,object>>(text);}catch{throw new TelegramApiException(0);}
                    if(root==null||!response.IsSuccessStatusCode||!object.Equals(Json.Get(root,"ok"),true)) {
                        int code=(int)(root==null?(int)response.StatusCode:Json.Num(Json.Get(root,"error_code"))??(int)response.StatusCode);
                        int retry=(int)(root==null?30:Json.Num(Json.Get(Json.Map(Json.Get(root,"parameters")),"retry_after"))??30);
                        throw new TelegramApiException(code,Math.Max(5,Math.Min(3600,retry)));
                    }
                    return Json.Get(root,"result");
                }
            }
        }catch(TelegramApiException){throw;}catch{throw new TelegramApiException(0);}
    }
    public async Task<TelegramSecrets> ValidateBot(string token) {
        var me=Json.Map(await Call(token,"getMe",new{}).ConfigureAwait(false));long id=TelegramRules.Id(Json.Get(me,"id"));string username=Json.Str(Json.Get(me,"username"));
        if(id<=0||!Regex.IsMatch(username,@"^[A-Za-z0-9_]{5,64}$")||!object.Equals(Json.Get(me,"is_bot"),true))throw new ApiException("O Telegram não retornou um bot válido para esse token.");
        var hook=Json.Map(await Call(token,"getWebhookInfo",new{}).ConfigureAwait(false));
        if(!string.IsNullOrEmpty(Json.Str(Json.Get(hook,"url"))))throw new TelegramApiException(409);
        return new TelegramSecrets{Token=token,BotId=id,BotUsername=username};
    }
    public async Task<List<TelegramUpdate>> Updates(TelegramSecrets s,long offset){return TelegramRules.ParseUpdates(await Call(s.Token,"getUpdates",new{offset=offset,limit=50,timeout=20,allowed_updates=new[]{"message","callback_query"}}).ConfigureAwait(false));}
    public async Task<SendResult> Send(TelegramSecrets s,NotificationEvent e,double? index,int? ml) {
        try {
            var message=Json.Map(await Call(s.Token,"sendMessage",TelegramRules.Alert(s,e,index,ml)).ConfigureAwait(false));long id=TelegramRules.Id(Json.Get(message,"message_id"));
            long chat=TelegramRules.Id(Json.Get(Json.Map(Json.Get(message,"chat")),"id"));
            if(id<=0||chat!=s.ChatId)return new SendResult{State="uncertain",Detail="Resposta sem identificação do chat esperado. Confira o Telegram antes de repetir."};
            return new SendResult{State="sent",MessageId="tg:"+id.ToString(CultureInfo.InvariantCulture),Detail="Mensagem criada pelo Telegram. A confirmação de rega chega ao tocar no botão."};
        }catch(TelegramApiException ex){return new SendResult{State=ex.Code==0||ex.Code>=500?"uncertain":"failed",RetryAllowed=ex.Code==429,RetryAfterSeconds=ex.RetryAfter,Detail=ex.Code==0?"Envio ao Telegram com resultado indefinido. Confira o celular; não haverá repetição automática.":ex.Message};}
    }
    public async Task Answer(TelegramSecrets s,string callbackId,string feedback){await Call(s.Token,"answerCallbackQuery",new{callback_query_id=callbackId,text=feedback,show_alert=false}).ConfigureAwait(false);}
    public void Dispose(){client.Dispose();}
}
}
