using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using Raiz;
public static class FeatureTests {
    static DateTime Now=new DateTime(2026,10,29,18,0,0,DateTimeKind.Utc);
    static Calibration Cal(){return new Calibration{Dry=392,Wet=281,Dark=22,Bright=316};}
    static TelegramSecrets Secret(){return new TelegramSecrets{Token="123456789:FAKE_TEST_TOKEN_abcdefghijklmnopqrstuvwxyz",BotId=123456789,BotUsername="test_raiz_bot",ChatId=5000000001,UserId=5000000001,PairedUtc=Now.AddDays(-1)};}
    static Settings Setup(out Plant p){p=new Plant{Name="Jade, da sala",Scientific="Crassula ovata"};var s=new Settings{ActiveId=p.LocalId,Calibration=Cal(),Weather=new Weather{FetchedUtc=Now,ModelUtc=Now,IsDay=true}};s.Plants.Add(p);return s;}
    static List<Sample> Samples(DateTime now,int light,int soil,int seconds){return Enumerable.Range(0,seconds+1).Select(i=>new Sample{Daylight=true,Soil=soil,Light=light,Utc=now.AddSeconds(i-seconds)}).ToList();}
    static TelegramUpdate Callback(NotificationEvent e,string prefix,long messageId=42){var s=Secret();return new TelegramUpdate{Kind="callback",PrivateChat=true,ChatId=s.ChatId,UserId=s.UserId,MessageBotId=s.BotId,MessageId=messageId,CallbackId=Guid.NewGuid().ToString("N"),Data=prefix+e.Id};}
    static TelegramUpdate Text(long prompt,string value){var s=Secret();return new TelegramUpdate{Kind="message",PrivateChat=true,ChatId=s.ChatId,UserId=s.UserId,ReplyToBotId=s.BotId,ReplyToMessageId=prompt,MessageUtc=Now,Text=value};}
    public static void Run(Action<bool,string> check,string folder){
        Plant p;var settings=Setup(out p);var c=Cal();var secret=Secret();var h=Samples(Now,260,281,300);var sample=h.Last();var report=CareEngine.Evaluate(p,sample,settings.Weather,c,h,Now);
        check(DailyRecommendation.Get(p,sample,report,false,Now).Title=="Não precisa regar agora","recomendacao principal informa nao regar no solo umido");
        check(DailyRecommendation.Get(p,null,report,false,Now).Title=="Aguardando sensor","recomendacao principal nao usa leitura ausente");
        check(DailyRecommendation.Get(p,sample,report,true,Now).Severity!="ok","recomendacao principal identifica demonstracao");
        var dry=Samples(Now,260,392,60);report=CareEngine.Evaluate(p,dry.Last(),settings.Weather,c,dry,Now);
        check(DailyRecommendation.Get(p,dry.Last(),report,false,Now).Title=="Confira o solo antes de regar","recomendacao seca pede conferir antes de aplicar agua");
        CareEngine.RegisterLight(p,sample,settings.Weather,c,"Janela validada",Now);var low=Samples(Now,100,281,300);
        var light=LightAlerts.Prepare(settings,p,secret,low.Last(),low,Now);
        check(light!=null&&light.Kind=="light","pouca luz por cinco minutos gera evento separado");
        check(LightAlerts.Prepare(settings,p,secret,low.Last(),low,Now)==light,"pouca luz continua reutiliza o mesmo aviso");
        check(!Json.Write(TelegramRules.Alert(secret,light,30,null)).Contains("Já reguei"),"aviso de luz nao oferece registrar rega");
        check(NotificationEngine.Active(settings,p.LocalId,"Telegram")==null,"aviso de luz nao bloqueia ou vira aviso de agua");
        NotificationEngine.AcknowledgeLocal(settings,p,Now);check(!light.ConfirmedUtc.HasValue,"confirmar rega nao confirma indevidamente aviso de luz");
        check(!TelegramRules.Apply(settings,secret,Callback(light,TelegramRules.Prefix),sample,c,Now).WateringAdded,"payload de rega forjado para evento de luz e recusado");
        light.State="sent";check(LightAlerts.Recover(settings,p,sample,h,Now)&&light.ClosedUtc.HasValue,"luz recuperada persistentemente encerra o ciclo");
        check(LightAlerts.Prepare(settings,p,secret,low.Last(),low,Now.AddMinutes(1))==null,"cooldown impede repetir aviso de luz em poucos minutos");
        Plant nightPlant;var night=Setup(out nightPlant);CareEngine.RegisterLight(nightPlant,sample,night.Weather,c,"Janela",Now);night.Weather.IsDay=false;
        check(LightAlerts.Prepare(night,nightPlant,secret,low.Last(),low,Now)==null,"alerta de pouca luz nao e enviado a noite");night.Weather.IsDay=true;
        var dawn=Samples(Now,100,281,300);dawn[0].Daylight=false;check(LightAlerts.Prepare(night,nightPlant,secret,dawn.Last(),dawn,Now)==null,"periodo noturno anterior nao conta para cinco minutos de pouca luz diurna");
        var shortLow=Samples(Now,100,281,100);check(LightAlerts.Prepare(night,nightPlant,secret,shortLow.Last(),shortLow,Now)==null,"mudanca curta de luz nao envia alerta");
        var later=Now.AddHours(7);settings.Weather=new Weather{FetchedUtc=later,ModelUtc=later,IsDay=true};var laterLow=Samples(later,100,281,300);check(LightAlerts.Prepare(settings,p,secret,laterLow.Last(),laterLow,later)!=null,"novo aviso de luz exige novo periodo persistente apos cooldown");
        night.Telegram.LightAlertsEnabled=false;check(LightAlerts.Prepare(night,nightPlant,secret,low.Last(),low,Now)==null,"usuario pode desligar apenas avisos de luz");
        report=CareEngine.Evaluate(nightPlant,low.Last(),night.Weather,c,low,Now);check(DailyRecommendation.Get(nightPlant,low.Last(),report,false,Now).Title=="Confira a iluminação","recomendacao principal destaca luz quando nao precisa regar");
        var steps=SetupChecklist.Get(settings,false,true);check(steps.Length==6&&steps.Count(x=>x.Done)==4,"assistente distingue etapas prontas de cidade e sensor pendentes");
        settings.Location=new Location{Name="Guarulhos"};check(SetupChecklist.Get(settings,true,true).All(x=>x.Done),"assistente reconhece configuracao completa sem repetir cadastro");
        Volume(check,folder);History(check,folder);
    }
    static void Volume(Action<bool,string> check,string folder){
        Plant p;var settings=Setup(out p);var c=Cal();var secret=Secret();var sample=new Sample{Soil=281,Light=260,Utc=Now};
        var e=TelegramRules.Prepare(settings,p,secret,Now.AddMinutes(-1));e.MetaMessageId="tg:42";e.AlertSoilIndex=0;e.AlertDry=392;e.AlertWet=281;
        var first=TelegramRules.Apply(settings,secret,Callback(e,TelegramRules.Prefix),sample,c,Now);
        check(first.WateringAdded&&first.ReplyKind=="quantity_choice"&&p.Care.Waterings.Count==1,"Ja reguei registra uma vez e oferece quantidade opcional");
        check(p.Care.Waterings[0].VolumeUnknown&&!p.Care.Waterings[0].InitialReadingUnknown,"rega sem quantidade fica desconhecida mas preserva leitura inicial do aviso");
        e.QuantityMessageId=43;e.QuantityState="choice";
        var foreign=Callback(e,"raiz_ml:",43);foreign.UserId++;
        check(!TelegramRules.Apply(settings,secret,foreign,sample,c,Now).Handled,"outro usuario nao pode alterar quantidade");
        check(TelegramRules.Apply(settings,secret,Callback(e,"raiz_ml:",999),sample,c,Now).ReplyKind==null,"botao de outra mensagem nao abre quantidade");
        var ask=TelegramRules.Apply(settings,secret,Callback(e,"raiz_ml:",43),sample,c,Now);
        check(ask.ReplyKind=="quantity_prompt"&&e.QuantityState=="prompt_pending","botao de quantidade solicita resposta vinculada ao evento");
        check(Json.Write(TelegramWater.Message(secret,e,"quantity_prompt")).Contains("force_reply"),"pergunta de quantidade usa resposta dirigida do Telegram");
        e.QuantityPromptId=44;e.QuantityState="awaiting";
        check(!TelegramRules.Apply(settings,secret,Text(999,"120 mL"),sample,c,Now).Handled,"texto sem responder a pergunta correta nao altera rega");
        var badBot=Text(44,"120 mL");badBot.ReplyToBotId++;check(!TelegramRules.Apply(settings,secret,badBot,sample,c,Now).Handled,"resposta a outro bot nao altera quantidade");
        foreach(string invalid in new[]{"0","100001","NaN","1 litro"}){var r=TelegramRules.Apply(settings,secret,Text(44,invalid),sample,c,Now);check(r.ReplyKind=="invalid_quantity"&&p.Care.Waterings[0].VolumeUnknown,"quantidade invalida nao grava volume: "+invalid);}
        var expired=Text(44,"120 mL");expired.MessageUtc=Now.AddDays(-2);check(!TelegramRules.Apply(settings,secret,expired,sample,c,Now).Handled,"mensagem de quantidade antiga nao atualiza rega atual");
        var saved=TelegramRules.Apply(settings,secret,Text(44,"120 mL"),sample,c,Now);
        check(saved.ReplyKind=="drainage"&&p.Care.Waterings[0].TotalMl==120&&!p.Care.Waterings[0].VolumeUnknown,"volume medido atualiza o mesmo registro sem nova rega");
        check(CareEngine.Learned(p,c).Count==0,"quantidade sozinha nao aprende dose sem drenagem");
        e.DrainageMessageId=45;e.QuantityState="drainage";
        TelegramRules.Apply(settings,secret,Callback(e,"raiz_drenou:",45),sample,c,Now);
        check(p.Care.Waterings.Count==1&&p.Care.Waterings[0].DrainageConfirmed&&CareEngine.Estimate(p,c)==120,"drenagem explicita e leituras validas permitem aprendizado");
        TelegramRules.Apply(settings,secret,Text(44,"900 mL"),sample,c,Now);TelegramRules.Apply(settings,secret,Callback(e,"raiz_drenou:",45),sample,c,Now);
        check(p.Care.Waterings.Count==1&&p.Care.Waterings[0].TotalMl==120,"respostas repetidas nao duplicam ou alteram rega finalizada");
        var other=TelegramRules.Prepare(settings,p,secret,Now.AddSeconds(1));TelegramRules.Apply(settings,secret,Callback(other,TelegramRules.Prefix),sample,c,Now.AddSeconds(2));other.QuantityState="choice";other.QuantityMessageId=46;
        TelegramRules.Apply(settings,secret,Callback(other,"raiz_sem:",46),sample,c,Now.AddSeconds(2));
        check(p.Care.Waterings.Last().VolumeUnknown&&other.QuantityState=="skipped","Nao medi preserva volume desconhecido e encerra pergunta");
        var store=new Store(Path.Combine(folder,"volume-telegram"));store.Settings.Plants=settings.Plants;store.Settings.ActiveId=p.LocalId;store.Settings.Notifications=settings.Notifications;store.Save();var loaded=new Store(store.Folder);
        check(loaded.Settings.Plants[0].Care.Waterings[0].TotalMl==120&&loaded.Settings.Notifications[0].QuantityState=="done","volume e estado da pergunta sobrevivem a reinicio");
        p.Care.Waterings[0].SensorReadingUnknown=true;check(CareEngine.Estimate(p,c)==null,"volume e drenagem sem leitura final nao ensinam dose");
        var parsed=TelegramRules.ParseUpdates(Json.Read<object>("[{\"update_id\":81,\"message\":{\"chat\":{\"id\":5000000001,\"type\":\"private\"},\"from\":{\"id\":5000000001},\"date\":1793296800,\"text\":\"120 mL\",\"reply_to_message\":{\"message_id\":44,\"from\":{\"id\":123456789}}}}]"));
        check(parsed[0].ReplyToMessageId==44&&parsed[0].ReplyToBotId==secret.BotId,"parser preserva identidade da pergunta respondida");
    }
    static void History(Action<bool,string> check,string folder){
        var dir=Path.Combine(folder,"historico-diario");var store=new Store(dir);var p=new Plant{Name="Jade, \"da sala\""};var c=Cal();DateTime start=Now.AddHours(-1);
        store.Append(new Sample{Soil=392,Light=260,Utc=start},p,null,null,c);store.Append(new Sample{Soil=337,Light=150,Utc=start.AddSeconds(3)},p,null,null,c);store.Append(new Sample{Soil=281,Light=100,Utc=start.AddSeconds(40)},p,null,null,c);
        store.Append(new Sample{Soil=100,Light=100,Utc=start.AddSeconds(45)},new Plant{Name="Outra"},null,null,c);
        File.AppendAllText(store.HistoryPath,"linha,incompleta\r\n");var waters=new[]{new WateringRecord{FinishedUtc=start.AddMinutes(2),TotalMl=120},new WateringRecord{FinishedUtc=start.AddMinutes(5),VolumeUnknown=true}};
        var snapshot=DailyHistory.Read(store.HistoryPath,p.LocalId,waters,new DateTime[0],7,Now);var day=snapshot.Days.Single(x=>x.Date==start.ToLocalTime().Date);
        check(day.Count==3&&day.SoilMin==0&&day.SoilMax==100,"historico diario filtra planta e conserva extremos reais");
        check(snapshot.Points.Count==2&&snapshot.Points[0].Segment!=snapshot.Points[1].Segment,"lacuna de leituras divide curvas em vez de interpolar desconexao");
        check(day.Waterings==2&&day.KnownMl==120&&day.UnknownVolumes==1,"historico diferencia volume conhecido de rega nao medida");
        check(day.CoveredSeconds==3&&day.MissingMinutes>0,"tempo sem cobertura nao vira valor de umidade zero");
        check(snapshot.Days.Count==7&&snapshot.Days.Any(x=>x.Count==0),"historico inclui dias inteiros sem coleta");
        check(snapshot.Invalid==1,"linha incompleta e ignorada sem perder historico valido");
        var changed=DailyHistory.Read(store.HistoryPath,p.LocalId,waters,new[]{start.AddSeconds(2)},7,Now);
        check(changed.Points.Count==3,"mudanca de calibracao interrompe segmento mesmo sem desconexao");
        check(DailyHistory.Csv("\"a,b\",\"c\"\"d\"")[1]=="c\"d","parser de historico aceita virgula e aspas escapadas");
        check(DailyHistory.Read(Path.Combine(dir,"ausente.csv"),p.LocalId,waters,null,7,Now).Days.All(x=>x.Count==0),"arquivo ausente apresenta dias sem dados sem inventar leituras");
    }
}
