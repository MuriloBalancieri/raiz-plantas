using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Reflection;using System.Threading.Tasks;using System.Windows.Forms;using Raiz;
public static class DemoHistoryTests {
    static BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
    public static void Run(Action<bool,string> check,string root){
        var now=new DateTime(2026,11,6,20,0,0,DateTimeKind.Utc);var source=new Store(Path.Combine(root,"real-preservado"));var p=new Plant{Name="Planta real de teste"};source.Settings.Plants.Add(p);source.Settings.ActiveId=p.LocalId;source.Save();source.Append(new Sample{Utc=now,Soil=330,Light=240},p,null,null,new Calibration());var before=File.ReadAllBytes(source.HistoryPath);var configuration=File.ReadAllBytes(Path.Combine(source.Folder,"configuracao.json"));
        var demo=DemoHistory.Generate(Path.Combine(source.Folder,"Demonstracao"),now,p,new Calibration{Dry=800,Wet=300,Dark=100,Bright=900});var files=Directory.GetFiles(demo.Folder);
        check(demo.Plant.Name==p.Name&&demo.Plant.Scientific==p.Scientific&&!demo.Plant.Name.Contains("FICT"),"simulacao usa nome e especie da planta escolhida sem sufixo de demonstracao");
        check(demo.Plant.LocalId!=p.LocalId&&!object.ReferenceEquals(demo.Plant,p),"perfil simulado tem identidade separada da planta real");
        var calibration=Json.Read<Settings>(File.ReadAllText(Path.Combine(demo.Folder,"configuracao-exemplo.json"))).Calibration;check(calibration.Dry==800&&calibration.Wet==300&&calibration.Dark==100,"simulacao usa copia das referencias locais validas");
        check(files.Length==6&&files.Count(f=>f.EndsWith(".csv"))==3,"exemplos incluem CSV de medicoes e regas, JSON, resumo, PDF e guia");
        check(File.ReadAllBytes(source.HistoryPath).SequenceEqual(before)&&File.ReadAllBytes(Path.Combine(source.Folder,"configuracao.json")).SequenceEqual(configuration),"gerar historico ficticio preserva medicoes e configuracao reais");
        var realRead=DailyHistory.Read(demo.HistoryPath,demo.Plant.LocalId,new WateringRecord[0],new DateTime[0],7,now);check(realRead.Points.Count==0,"leitor normal rejeita medicoes com origem simulada");
        var data=DailyHistory.Read(demo.HistoryPath,demo.Plant.LocalId,demo.Plant.Care.Waterings,new DateTime[0],7,demo.EndUtc,true);
        check(data.Days.All(d=>d.ElapsedSeconds>86399)&&data.Days.Last().Date==now.ToLocalTime().Date.AddDays(-1),"historico cobre sete dias completos anteriores a hoje");
        check(data.Simulated&&data.Days.Count==7&&data.Days.Sum(d=>d.Count)>40000,"historico de exemplo tem sete dias e origem ficticia explicita");
        check(data.Points.Select(x=>x.Segment).Distinct().Count()>1,"lacunas ficticias aparecem como trechos separados");
        check(data.Waterings.Count>=3&&data.Days.Sum(d=>d.Waterings)==data.Waterings.Count,"regas ilustrativas aparecem nos totais diarios");
        check(data.Points.Max(x=>x.Light)>80&&data.Points.Min(x=>x.Light)==0,"exemplo mostra ciclo diario de luminosidade");
        check(data.Points.Max(x=>x.Soil)>80&&data.Points.Min(x=>x.Soil)<12,"exemplo mostra secagem e recuperacao apos rega");
        check(data.Points.All(x=>x.Utc<=now)&&data.Waterings.All(x=>x.FinishedUtc<=now),"historico ficticio nao cria registros futuros");
        check(File.ReadAllText(Path.Combine(demo.Folder,"resumo.csv")).Contains("simulado"),"CSV de resumo preserva origem simulada");
        string pdf=Encoding.GetEncoding(1252).GetString(File.ReadAllBytes(Path.Combine(demo.Folder,"relatorio.pdf")));check(pdf.Contains("Dados simulados")&&pdf.Contains("leituras no período"),"PDF identifica dados inventados nas paginas e no resumo");
        var example=new Store(demo.Folder);check(example.Settings.Plants.Count==0,"configuracao de exemplo nao e ativada automaticamente pelo Store");
        string json=File.ReadAllText(Path.Combine(demo.Folder,"configuracao-exemplo.json"));check(!json.Contains("Token")&&!json.Contains("PerenualKey"),"arquivos de exemplo nao incluem credenciais");
        Bot(check,source,now);
    }
    static void Bot(Action<bool,string> check,Store store,DateTime now){
        var secret=new TelegramSecrets{Token="123456789:abcdefghijklmnopqrstuvwxyz_123456",BotId=123456789,ChatId=5000000001,UserId=5000000001,PairedUtc=now};var p=store.Settings.Plants[0];var e=TelegramRules.Prepare(store.Settings,p,secret,now,true);e.Kind="exhibition";e.State="sent";e.MetaMessageId="tg:77";
        string message=Json.Write(TelegramRules.Alert(secret,e,8,null));check(message.Contains("DEMONSTRA")&&message.Contains("Confirmar demonstra"),"mensagem real de apresentacao e marcada como demonstracao e tem botao");
        var update=new TelegramUpdate{UpdateId=1,Kind="callback",PrivateChat=true,ChatId=secret.ChatId,UserId=secret.UserId,CallbackId="callback-ficticio",MessageId=77,MessageBotId=secret.BotId,Data=TelegramRules.Prefix+e.Id};
        var real=TelegramRules.Prepare(store.Settings,p,secret,now);real.State="sent";real.MetaMessageId="tg:78";var other=new TelegramUpdate{UpdateId=2,Kind="callback",PrivateChat=true,ChatId=secret.ChatId,UserId=secret.UserId,CallbackId="outro",MessageId=78,MessageBotId=secret.BotId,Data=TelegramRules.Prefix+real.Id};
        long offset=store.Settings.Telegram.NextOffset;var seen=new HashSet<long>();var results=ExhibitNotifications.ApplyTests(store.Settings,secret,new[]{other,update},seen,now);
        check(e.ConfirmedUtc.HasValue&&results.Count==1,"confirmacao do celular chega ao evento de teste");
        check(!real.ConfirmedUtc.HasValue&&p.Care.Waterings.Count==0,"receptor da mostra nao processa regas reais nem cria registros ficticios na planta");
        check(store.Settings.Telegram.NextOffset==offset,"leitura didatica preserva offset normal para respostas pendentes");
        seen.Add(update.UpdateId);check(ExhibitNotifications.ApplyTests(store.Settings,secret,new[]{update},seen,now).Count==0,"resposta de demonstracao repetida nao e reprocessada");
        var second=TelegramRules.Prepare(store.Settings,p,secret,now,true);second.Kind="exhibition";second.State="sent";second.MetaMessageId="tg:79";update.UpdateId=3;update.Data=TelegramRules.Prefix+second.Id;update.MessageId=79;update.UserId++;
        ExhibitNotifications.ApplyTests(store.Settings,secret,new[]{update},seen,now);check(!second.ConfirmedUtc.HasValue,"outra pessoa nao pode confirmar demonstracao vinculada");
        using(var form=new MainForm(store.Folder,false)){((Timer)typeof(MainForm).GetField("timer",F).GetValue(form)).Stop();typeof(MainForm).GetField("historyFiction",F).SetValue(form,true);typeof(MainForm).GetField("page",F).SetValue(form,"Histórico");var mem=(Store)typeof(MainForm).GetField("store",F).GetValue(form);int count=mem.Settings.Notifications.Count;((Task)typeof(MainForm).GetMethod("PollTelegram",F).Invoke(form,null)).GetAwaiter().GetResult();((Task)typeof(MainForm).GetMethod("PollWhatsApp",F).Invoke(form,null)).GetAwaiter().GetResult();check(mem.Settings.Notifications.Count==count,"envios automaticos ficam pausados tambem no historico ficticio");}
    }
}
