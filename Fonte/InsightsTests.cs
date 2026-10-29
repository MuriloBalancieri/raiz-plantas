using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Reflection;using System.Windows.Forms;using Raiz;
public static class InsightsTests {
    static readonly DateTime Now=new DateTime(2026,10,29,18,0,0,DateTimeKind.Utc);
    static Calibration Cal(){return new Calibration{Dry=392,Wet=281,Dark=22,Bright=316};}
    static bool Reject(Action work){try{work();return false;}catch(ApiException){return true;}}
    static List<Sample> Samples(int seconds,int soil,int light){return Enumerable.Range(0,seconds/3+1).Select(i=>new Sample{Soil=soil,Light=light,Utc=Now.AddSeconds(-seconds+i*3)}).ToList();}
    public static void Run(Action<bool,string> check,string root){
        var c=Cal();var values=Samples(600,330,240);var last=values.Last();
        check(SensorHealth.Evaluate(values,last,c,false,false,Now,null).Title.Contains("desconectado"),"diagnostico identifica cabo desconectado");
        check(SensorHealth.Evaluate(values,last,c,true,true,Now,null).Title.Contains("demonstração"),"diagnostico nao acusa sensor na demonstracao");
        check(SensorHealth.Evaluate(values,null,c,true,false,Now,Now).Body.Contains("inválido"),"pacote invalido e explicado sem fabricar amostra");
        check(SensorHealth.Evaluate(values,last,c,true,false,Now.AddSeconds(13),null).Title.Contains("Sem medições"),"leituras antigas geram diagnostico de interrupcao");
        check(SensorHealth.Evaluate(values,last,c,true,false,Now,null).Title.Contains("Possível"),"dez minutos identicos geram suspeita e nao defeito confirmado");
        check(SensorHealth.Evaluate(values,last,c,true,false,Now,null).Body.Contains("estável"),"diagnostico informa que ambiente estavel tambem repete valores");
        var shortValues=Samples(60,330,240);check(SensorHealth.Evaluate(shortValues,shortValues.Last(),c,true,false,Now,null).Severity=="ok","um minuto estavel nao e tratado como leitura travada");
        var rails=Samples(60,0,240);check(SensorHealth.Evaluate(rails,rails.Last(),c,true,false,Now,null).Title.Contains("limite"),"saturacao persistente de ADC produz aviso");
        var outside=Samples(30,700,240);check(SensorHealth.Evaluate(outside,outside.Last(),c,true,false,Now,null).Title.Contains("referências"),"solo persistentemente fora da calibracao produz aviso");
        var broken=Samples(600,330,240);broken.RemoveRange(40,20);check(SensorHealth.Evaluate(broken,broken.Last(),c,true,false,Now,null).Severity=="ok","lacuna interrompe a janela de sensor travado");
        check(SensorHealth.Evaluate(shortValues,shortValues.Last(),c,true,false,Now.AddSeconds(1),Now.AddMilliseconds(500)).Title.Contains("descartado"),"ultimo pacote ruim aparece mesmo com leitura valida recente");
        var p=new Plant{Name="Jade do teste",Scientific="Crassula ovata"};var snap=new DailySnapshot();var start=Now.AddHours(-3);snap.Waterings.Add(new WateringRecord{FinishedUtc=start,VolumeUnknown=true});snap.Points.Add(new HistoryPoint{Utc=start.AddMinutes(4),Soil=80,Segment=1});snap.Points.Add(new HistoryPoint{Utc=start.AddHours(2),Soil=8,Segment=1});
        check(Evolution.DryingHours(snap,10).Single()==2,"evolucao calcula ciclo continuo ate gatilho");
        snap.Points[1].Segment=2;check(Evolution.DryingHours(snap,10).Count==0,"evolucao nao atravessa lacuna nem calibracao");snap.Points[1].Segment=1;
        snap.Waterings.Add(new WateringRecord{FinishedUtc=start.AddHours(1)});check(Evolution.DryingHours(snap,10).Count==0,"nova rega encerra ciclo anterior incompleto");snap.Waterings.RemoveAt(1);
        snap.LastCalibrationUtc=start.AddHours(1);check(Evolution.DryingHours(snap,10).Count==0,"resumo nao mistura escalas anteriores de calibracao");snap.LastCalibrationUtc=null;
        snap.Points[0].Utc=start.AddMinutes(15);check(Evolution.DryingHours(snap,10).Count==0,"ausencia de coleta apos rega nao produz tempo estimado");snap.Points[0].Utc=start.AddMinutes(4);
        check(Evolution.Describe(new DailySnapshot(),p).Contains("ainda falta"),"sem dados resumo explica insuficiencia");snap.LightHours[10].Seconds=600;snap.LightHours[10].WeightedIndex=48000;
        check(Evolution.Describe(snap,p).Contains("10h–11h"),"resumo compara horas realmente observadas");
        string folder=Path.Combine(root,"insights");var store=new Store(folder);store.Settings.Plants.Add(p);store.Settings.ActiveId=p.LocalId;store.Settings.Calibration=c;
        for(int i=0;i<=300;i++)store.Append(new Sample{Utc=Now.AddSeconds(-900+i*3),Soil=330,Light=240},p,null,null,c);
        var parsed=DailyHistory.Read(store.HistoryPath,p.LocalId,p.Care.Waterings,new DateTime[0],7,Now);
        check(Math.Abs(parsed.LightHours.Sum(x=>x.Seconds)-900)<0.01,"luz por hora usa duracao continua das leituras completas");
        var changed=DailyHistory.Read(store.HistoryPath,p.LocalId,p.Care.Waterings,new[]{Now.AddMinutes(-5)},7,Now);check(changed.LightHours.Sum(x=>x.Seconds)<=300,"luz do resumo respeita ultima calibracao");
        string precise=Path.Combine(folder,"precisao.csv");File.WriteAllLines(precise,File.ReadAllLines(store.HistoryPath).Select((line,i)=>i==0?line:line.Replace(Now.AddSeconds(-900+(i-1)*3).ToString("o"),Now.AddSeconds(-900+(i-1)*3).AddTicks(i*137).ToString("o"))));
        var precision=DailyHistory.Read(precise,p.LocalId,p.Care.Waterings,new DateTime[0],7,Now.AddSeconds(1));check(precision.LightHours.Sum(x=>x.Seconds)>899,"horarios com fracao de milissegundo terminam e preservam duracao");
        var sparse=Path.Combine(folder,"esparso.csv");File.WriteAllLines(sparse,File.ReadAllLines(store.HistoryPath).Where((line,i)=>i==0||i%20==0));var sparseData=DailyHistory.Read(sparse,p.LocalId,p.Care.Waterings,new DateTime[0],7,Now);
        check(sparseData.LightHours.Sum(x=>x.Seconds)==0,"lacunas nao contam como exposicao luminosa");
        parsed.Waterings.Add(new WateringRecord{FinishedUtc=Now,TotalMl=120,Origin="Teste"});parsed.Waterings.Add(new WateringRecord{FinishedUtc=Now,VolumeUnknown=true});
        parsed.Days.Last().Waterings=2;parsed.Days.Last().KnownMl=120;parsed.Days.Last().UnknownVolumes=1;
        CareReports.Pdf(Path.Combine(folder,"relatorio.pdf"),p,parsed,Now);CareReports.Csv(Path.Combine(folder,"relatorio.csv"),p,parsed);
        check(File.ReadAllText(Path.Combine(folder,"relatorio.pdf")).StartsWith("%PDF-1.4"),"relatorio PDF gerado sem impressora ou dependencia externa");
        check(File.ReadAllText(Path.Combine(folder,"relatorio.csv")).Contains("volume_desconhecido"),"CSV diferencia volume desconhecido de zero");
        p.Name="=SOMA(1;2)";CareReports.Csv(Path.Combine(folder,"formula.csv"),p,parsed);check(File.ReadAllText(Path.Combine(folder,"formula.csv")).Contains("'=SOMA"),"CSV impede formula injetada no nome da planta");p.Name="Jade do teste";
        CareReports.Pdf(Path.Combine(folder,"vazio.pdf"),p,new DailySnapshot(),Now);check(new FileInfo(Path.Combine(folder,"vazio.pdf")).Length>1000,"relatorio vazio explica falta de dados sem inventar curva");
        var month=DailyHistory.Read(store.HistoryPath,p.LocalId,p.Care.Waterings,new DateTime[0],30,Now);for(int i=0;i<80;i++)month.Waterings.Add(new WateringRecord{FinishedUtc=Now.AddMinutes(-i),VolumeUnknown=i%2==0,TotalMl=120,Origin="Teste isolado"});CareReports.Pdf(Path.Combine(folder,"mensal.pdf"),p,month,Now);check(new FileInfo(Path.Combine(folder,"mensal.pdf")).Length>10000,"PDF mensal pagina lista longa de regas sem descartar registros");
        Transfer(check,store,root);Ui(check,store);
    }
    static void Transfer(Action<bool,string> check,Store store,string root){
        string password="senha-ficticia-de-teste-123",key="chave-ficticia-nao-e-real";string token="123456789:abcdefghijklmnopqrstuvwxyz_123456";
        store.SaveKey(key);store.SaveTelegramSecrets(new TelegramSecrets{Token=token,BotId=123456789,ChatId=5000000001,UserId=5000000001,PairedUtc=Now});store.SaveWhatsAppSecrets(new WhatsAppSecrets{AccessToken="segredo-whatsapp-ficticio"});store.Settings.Telegram.Enabled=true;store.Settings.Telegram.PollEnabled=true;
        var p=store.Settings.Plants[0];p.Care.LightReferenceRaw=240;p.Care.Session=new WaterSession{StartedUtc=Now};p.Care.Waterings.Add(new WateringRecord{FinishedUtc=Now,TotalMl=123,DrainageConfirmed=true});store.Settings.Notifications.Add(new NotificationEvent{CreatedUtc=Now,State="pending",PlantId=p.LocalId});store.Save();
        var settingsBefore=File.ReadAllBytes(Path.Combine(store.Folder,"configuracao.json"));string path=Path.Combine(root,"transferencia.raiz");DataTransfer.Export(store,path,password);var encrypted=File.ReadAllBytes(path);
        check(!Encoding.UTF8.GetString(encrypted).Contains(token)&&!Encoding.UTF8.GetString(encrypted).Contains(key),"transferencia cifra credenciais em vez de copiar DPAPI");
        var data=DataTransfer.Read(path,password);check(data.Settings.Plants[0].Care.Waterings[0].TotalMl==123&&data.PerenualKey==key&&data.Telegram.Token==token,"arquivo protegido faz ida e volta com plantas regas e conexoes");
        check(Reject(()=>DataTransfer.Read(path,"senha-incorreta-123")),"senha incorreta nao abre nem importa dados");
        encrypted[60]^=1;string tampered=Path.Combine(root,"alterado.raiz");File.WriteAllBytes(tampered,encrypted);check(Reject(()=>DataTransfer.Read(tampered,password)),"autenticacao rejeita arquivo alterado antes de descriptografar");
        check(Reject(()=>DataTransfer.Export(store,path,"curta")),"senha curta nao sobrescreve exportacao existente");
        DataTransfer.Read(path,password);check(true,"falha de exportacao preserva arquivo anterior");
        string imported=DataTransfer.ImportToNewFolder(data,root);var destination=new Store(imported);
        check(destination.Settings.Plants[0].LocalId==p.LocalId&&destination.Settings.Calibration.Dry==392,"importacao preserva identidades e calibracao");
        check(File.ReadAllBytes(Path.Combine(store.Folder,"configuracao.json")).SequenceEqual(settingsBefore),"importacao preserva pasta original byte por byte");
        check(destination.LoadKey()==key&&destination.LoadTelegramSecrets().Token==token&&destination.LoadWhatsAppSecrets().AccessToken=="segredo-whatsapp-ficticio","credenciais importadas sao reprotegidas para usuario Windows");
        check(!destination.Settings.Telegram.Enabled&&!destination.Settings.Telegram.PollEnabled&&!destination.Settings.WhatsApp.Enabled&&!destination.Settings.WhatsApp.ReceiverEnabled,"nova instalacao nao consulta nem envia avisos automaticamente");
        check(destination.Settings.Notifications.All(n=>n.ClosedUtc.HasValue)&&destination.Settings.Plants[0].Care.Session==null,"importacao nao reenvia evento nem retoma rega interrompida");
        check(destination.Settings.Plants[0].Care.LightReferenceRaw==null&&destination.Settings.Plants[0].Care.LearningResetUtc.HasValue,"luz e aprendizado precisam validar nova montagem");
        check(File.ReadAllBytes(destination.HistoryPath).SequenceEqual(File.ReadAllBytes(store.HistoryPath)),"historico de medicoes transferido integralmente");
        var initial=new InitialCredentialBundle{Telegram=data.Telegram,TelegramAlertsEnabled=true};InitialCredentials.Apply(destination,initial);check(!destination.Settings.Telegram.PollEnabled,"credenciais iniciais nao reativam avisos pausados da importacao");
        string bootstrap=Path.Combine(root,"bootstrap-ficticio.json");File.WriteAllText(bootstrap,Json.Write(initial));check(!InitialCredentials.ImportFile(destination,bootstrap),"transferencia impede importar automaticamente credenciais extras do pacote no reinicio");
        data.Settings.Plants.Add(data.Settings.Plants[0]);check(Reject(()=>DataTransfer.ImportToNewFolder(data,root)),"plantas duplicadas rejeitadas antes de criar importacao");
    }
    static void Ui(Action<bool,string> check,Store store){
        using(var form=new MainForm(store.Folder,false)){var flags=BindingFlags.NonPublic|BindingFlags.Instance;((Timer)typeof(MainForm).GetField("timer",flags).GetValue(form)).Stop();typeof(Control).GetMethod("SetState",flags).Invoke(form,new object[]{2,true});form.CreateControl();
            foreach(var page in new[]{"Histórico","Configurações","Arduino e calibração"}){typeof(MainForm).GetMethod("Navigate",flags).Invoke(form,new object[]{page});form.PerformLayout();
                if(page=="Histórico")check(All(form).OfType<Button>().Count(b=>b.Text=="Relatório PDF"||b.Text=="Resumo e regas CSV")==2,"historico oferece relatorios PDF e CSV");
                if(page=="Configurações"){var toggle=All(form).OfType<Button>().Single(b=>b.Text.Contains("Transferir dados"));toggle.PerformClick();form.PerformLayout();check(All(form).OfType<Button>().Any(b=>b.Text=="Importar transferência"&&b.Visible),"transferencia fica acessivel em secao recolhivel");}
                if(page=="Arduino e calibração")check(All(form).OfType<ReadableCard>().Any(),"diagnostico integrado a pagina de sensores");
            }
        }
    }
    static IEnumerable<Control> All(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in All(child))yield return nested;}}
}
