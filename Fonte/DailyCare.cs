using System;using System.Collections.Generic;using System.Linq;
namespace Raiz {
public static class DailyRecommendation {
    public static Advice Get(Plant plant,Sample sample,CareReport report,bool demo,DateTime now){
        if(demo)return new Advice("Demonstração · não regue com base nestes dados","Saia da demonstração para acompanhar seus sensores reais.","pending");
        if(plant==null)return new Advice("Escolha sua planta","Comece pelo assistente para cadastrar a espécie e preparar os sensores.","pending");
        if(sample==null||!sample.Fresh(now))return new Advice("Aguardando sensor","Conecte o Arduino. Sem uma leitura atual, o app não recomenda regar.","pending");
        if(!report.HasLiveSoil)return new Advice("Confira a configuração do solo",report.Soil.Body,report.Soil.Severity);
        if(report.CanStartWatering)return new Advice("Confira o solo antes de regar",report.Soil.Body+" "+report.Water.Title+".","attention");
        if(report.Soil.Severity=="attention")return new Advice("Confira o solo",report.Soil.Body,"attention");
        if(report.Light.Title=="Iluminação abaixo da referência")return new Advice("Confira a iluminação","Não há indicação de rega agora. A luz permaneceu abaixo da referência local; confira sombras e a posição da planta.","attention");
        if(report.Water.Title.Contains("0 mL")||report.Water.Title.Contains("não adicionar"))return new Advice("Não precisa regar agora",report.Soil.Body,"ok");
        return new Advice("Acompanhe o substrato",report.Soil.Body,"pending");
    }
}
public static class LightAlerts {
    public static NotificationEvent Active(Settings settings,string plantId){return settings.Notifications.LastOrDefault(x=>x.Channel=="Telegram"&&x.PlantId==plantId&&x.Kind=="light"&&!x.ClosedUtc.HasValue);}
    public static bool Low(Plant p,Sample s,Weather w,Calibration c,IList<Sample> history,DateTime now){
        if(p==null||s==null||!s.Fresh(now)||w==null||!w.Fresh(now)||w.IsDay!=true)return false;
        return CareEngine.Evaluate(p,s,w,c,history,now).Light.Title=="Iluminação abaixo da referência";
    }
    public static NotificationEvent Prepare(Settings settings,Plant p,TelegramSecrets secret,Sample s,IList<Sample> history,DateTime now){
        if(!settings.Telegram.LightAlertsEnabled||!secret.Paired||!Low(p,s,settings.Weather,settings.Calibration,history,now))return null;
        var active=Active(settings,p.LocalId);if(active!=null){if(now-(active.LastAttemptUtc??active.CreatedUtc)<TimeSpan.FromHours(6))return active;active.ClosedUtc=now;active.Detail="Intervalo do aviso anterior encerrado.";}
        var last=settings.Notifications.LastOrDefault(x=>x.Channel=="Telegram"&&x.PlantId==p.LocalId&&x.Kind=="light");
        if(last!=null&&now-(last.LastAttemptUtc??last.CreatedUtc)<TimeSpan.FromHours(6))return null;
        var c=settings.Calibration;double minimum;
        if(p.Light!=null&&p.Light.Enabled)minimum=p.Light.Min.Value;
        else if(p.Care.LightReferenceRaw.HasValue)minimum=c.Light(p.Care.LightReferenceRaw.Value).Value*0.8;else return null;
        if(!CareEngine.Persistent(history,now,300,x=>x.Daylight==true&&c.Light(x.Light).HasValue&&c.Light(x.Light).Value<minimum))return null;
        var e=TelegramRules.Prepare(settings,p,secret,now);e.Kind="light";e.Detail="Pouca luz persistente durante o dia.";return e;
    }
    public static bool Recover(Settings settings,Plant p,Sample s,IList<Sample> history,DateTime now){
        var e=p==null?null:Active(settings,p.LocalId);if(e==null)return false;
        var w=settings.Weather;if(w==null||!w.Fresh(now)||w.IsDay!=true)return false;
        if(CareEngine.Persistent(history,now,60,x=>x.Daylight==true&&CareEngine.Evaluate(p,x,w,settings.Calibration,history,x.Utc).Light.Severity=="ok")){
            e.ClosedUtc=now;e.State="resolved";e.Detail="Iluminação voltou à referência local.";return true;
        }return false;
    }
}
}
