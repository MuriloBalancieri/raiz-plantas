using System;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Text.RegularExpressions;using System.Threading.Tasks;
namespace Raiz {
public static class TelegramWater {
    public static bool Bound(TelegramSecrets secret,TelegramUpdate u){return secret.Paired&&u.PrivateChat&&!u.FromBot&&u.ChatId==secret.ChatId&&u.UserId==secret.UserId;}
    public static WateringRecord Record(Settings settings,NotificationEvent e){var p=settings.Plants.Find(x=>x.LocalId==e.PlantId);return p==null?null:p.Care.Waterings.LastOrDefault(x=>x.EventKey=="tg:"+e.ConfirmMessageId);}
    static bool EventBound(NotificationEvent e,TelegramSecrets secret,DateTime now){return e!=null&&e.Kind!="light"&&!e.IsTest&&e.ConfirmedUtc.HasValue&&now>=e.ConfirmedUtc.Value.AddMinutes(-1)&&now-e.ConfirmedUtc.Value<=TimeSpan.FromHours(24)&&e.SenderHash==WhatsAppRules.Hash(secret.BotId.ToString(CultureInfo.InvariantCulture))&&e.RecipientHash==WhatsAppRules.Hash(secret.ChatId.ToString(CultureInfo.InvariantCulture));}
    public static TelegramApplyResult Apply(Settings settings,TelegramSecrets secret,TelegramUpdate u,Sample sample,Calibration cal,DateTime now){
        var result=new TelegramApplyResult();if(!Bound(secret,u))return result;
        if(u.Kind=="message"){
            if(u.ReplyToMessageId<=0||u.ReplyToBotId!=secret.BotId||!u.MessageUtc.HasValue||u.MessageUtc<now.AddHours(-24)||u.MessageUtc>now.AddMinutes(5))return result;
            var e=settings.Notifications.LastOrDefault(x=>x.Channel=="Telegram"&&x.QuantityPromptId==u.ReplyToMessageId&&x.QuantityState=="awaiting");
            if(!EventBound(e,secret,now))return result;var record=Record(settings,e);if(record==null)return result;
            result.Handled=true;result.ReplyEventId=e.Id;string text=(u.Text??"").Trim().ToLowerInvariant();
            if(text=="não medi"||text=="nao medi"){e.QuantityState="skipped";result.ReplyKind="saved";result.Feedback="Rega mantida com volume não informado.";return result;}
            var match=Regex.Match(text,@"^([0-9]{1,6})\s*(?:ml)?$");int ml;
            if(!match.Success||!int.TryParse(match.Groups[1].Value,out ml)||ml<1||ml>100000){result.ReplyKind="invalid_quantity";result.Feedback="Informe um número inteiro de 1 a 100000 mL (ex.: 120 mL), ou responda Não medi. Use Responder na pergunta do bot.";return result;}
            record.TotalMl=ml;record.VolumeUnknown=false;e.QuantityState="drainage_pending";e.FollowupAttempts=1;e.LastFollowupUtc=now;result.ReplyKind="drainage";result.Feedback="Quantidade salva: "+ml+" mL.";return result;
        }
        if(u.Kind!="callback"||u.MessageBotId!=secret.BotId||u.MessageId<=0||string.IsNullOrEmpty(u.CallbackId)||u.CallbackId.Length>256)return result;
        var code=Regex.Match(u.Data??"",@"^raiz_(ml|sem|drenou|naodrenou):([a-f0-9]{32})$");if(!code.Success)return result;
        result.Handled=true;result.Answer=true;result.Feedback="Essa pergunta já foi respondida ou expirou.";
        var notification=settings.Notifications.LastOrDefault(x=>x.Id==code.Groups[2].Value&&x.Channel=="Telegram");
        if(!EventBound(notification,secret,now))return result;var watering=Record(settings,notification);if(watering==null)return result;string action=code.Groups[1].Value;
        result.ReplyEventId=notification.Id;
        if((action=="ml"||action=="sem")&&notification.QuantityMessageId==u.MessageId&&notification.QuantityState=="choice"){
            if(action=="sem"){notification.QuantityState="skipped";result.Feedback="Rega salva com volume não informado.";return result;}
            notification.QuantityState="prompt_pending";notification.FollowupAttempts=1;notification.LastFollowupUtc=now;result.ReplyKind="quantity_prompt";result.Feedback="Responda à pergunta do bot com o volume medido.";return result;
        }
        if((action=="drenou"||action=="naodrenou")&&notification.DrainageMessageId==u.MessageId&&notification.QuantityState=="drainage"){
            notification.QuantityState="done";watering.DrainageConfirmed=action=="drenou";
            if(now-notification.ConfirmedUtc.Value<=TimeSpan.FromMinutes(30)&&settings.ActiveId==notification.PlantId&&cal.Dry==watering.Dry&&cal.Wet==watering.Wet&&sample!=null&&CareEngine.ReliableSoil(sample,cal,now)){watering.FinalIndex=cal.Soil(sample.Soil).Value;watering.SensorReadingUnknown=false;}
            result.Feedback=watering.DrainageConfirmed?"Drenagem registrada. O aprendizado também exige leituras válidas antes e depois da rega.":"Quantidade registrada sem usar esta rega no aprendizado.";return result;
        }return result;
    }
    public static object Message(TelegramSecrets secret,NotificationEvent e,string kind){
        if(kind=="quantity_prompt")return new{chat_id=secret.ChatId,text="Quanto de água você colocou em "+e.PlantName+"? Responda a ESTA mensagem com o total medido em mL, por exemplo 120 mL. Se não mediu, responda Não medi.",reply_markup=new{force_reply=true,selective=true,input_field_placeholder="Total em mL ou Não medi"}};
        if(kind=="drainage")return new{chat_id=secret.ChatId,text="A água começou a sair pelos furos do vaso durante esta rega de "+e.PlantName+"? Só confirme se observou a drenagem. Não coloque mais água para testar.",reply_markup=new{inline_keyboard=new[]{new[]{new{text="Sim, observei",callback_data="raiz_drenou:"+e.Id},new{text="Não observei",callback_data="raiz_naodrenou:"+e.Id}}}}};
        return new{chat_id=secret.ChatId,text="Rega de "+e.PlantName+" registrada. Deseja informar quanto de água realmente colocou? É opcional.",reply_markup=new{inline_keyboard=new[]{new[]{new{text="Informar quantidade",callback_data="raiz_ml:"+e.Id},new{text="Não medi",callback_data="raiz_sem:"+e.Id}}}}};
    }
}
public partial class MainForm {
    async Task SendWaterFollowup(TelegramSecrets secret,TelegramApplyResult result){
        if(closing||secret.Token!=telegramSecrets.Token||secret.BotId!=telegramSecrets.BotId||string.IsNullOrEmpty(result.ReplyKind))return;
        var e=store.Settings.Notifications.LastOrDefault(x=>x.Id==result.ReplyEventId);if(e==null)return;
        try{
            object body=result.ReplyKind=="saved"||result.ReplyKind=="invalid_quantity"?(object)new{chat_id=secret.ChatId,text=result.Feedback}:TelegramWater.Message(secret,e,result.ReplyKind);
            var raw=Json.Map(await telegramTransport.Call(secret.Token,"sendMessage",body));if(closing)return;
            long id=TelegramRules.Id(Json.Get(raw,"message_id")),chat=TelegramRules.Id(Json.Get(Json.Map(Json.Get(raw,"chat")),"id"));if(id<=0||chat!=secret.ChatId)throw new ApiException("Resposta sem identificação de mensagem.");
            if(result.ReplyKind=="quantity_choice"){e.QuantityMessageId=id;e.QuantityState="choice";}
            if(result.ReplyKind=="quantity_prompt"){e.QuantityPromptId=id;e.QuantityState="awaiting";}
            if(result.ReplyKind=="drainage"){e.DrainageMessageId=id;e.QuantityState="drainage";}
            store.Save();
        }catch{telegramStatus="A rega foi salva, mas a pergunta opcional não pôde ser enviada. Toque novamente em Já reguei após um minuto para tentar de novo.";UpdateTelegramPage();}
    }
}
}
