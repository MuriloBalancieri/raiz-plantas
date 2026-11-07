using System;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Linq;using System.Text;using System.Threading.Tasks;using System.Windows.Forms;
namespace Raiz {
public class DemoHistoryBundle {public string Folder,HistoryPath;public Plant Plant;public DateTime CreatedUtc,EndUtc;}
public static class DemoHistory {
    public static DemoHistoryBundle Generate(string parent,DateTime now,Plant selected=null,Calibration references=null){
        var folder=Path.Combine(parent,"Exemplo-"+now.ToLocalTime().ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(folder);
        var plant=selected==null?new Plant{Name="Jade",Scientific="Crassula ovata",Source="Perfil ilustrativo",Watering="Pouca rega",Sunlight="Sol pleno"}:Json.Read<Plant>(Json.Write(selected));
        plant.LocalId="simulado-"+Guid.NewGuid().ToString("N");plant.Care=new CarePlan{AutomaticSpeciesCare=false,NearDryIndex=selected==null?10:CareEngine.Trigger(selected)};
        var cal=references==null?new Calibration{Dry=392,Wet=281,Dark=22,Bright=316}:Json.Read<Calibration>(Json.Write(references));if(!cal.Soil(330).HasValue){cal.Dry=392;cal.Wet=281;}if(!cal.Light(240).HasValue){cal.Dark=22;cal.Bright=316;}
        DateTime end=now.ToLocalTime().Date.ToUniversalTime(),last=end.AddTicks(-1),first=now.ToLocalTime().Date.AddDays(-7),cycleStart=first.AddHours(8);
        for(int day=0;day<=6;day+=2){var date=first.AddDays(day).AddHours(8).ToUniversalTime();plant.Care.Waterings.Add(new WateringRecord{RecordId="ficticia-"+day,PlantName=plant.Name,Origin="Registro simulado",FinishedUtc=date,TotalMl=120+day*5,Dry=cal.Dry,Wet=cal.Wet,InitialIndex=8,FinalIndex=85,DrainageConfirmed=true});}
        string history=Path.Combine(folder,"historico.csv");
        using(var writer=new StreamWriter(history,false,new UTF8Encoding(true))){
            writer.WriteLine("utc,planta_id,planta,solo_bruto,luz_bruta,solo_indice_relativo,luz_indice_relativo,temperatura_regional_c,umidade_regional_pct,horario_modelo,localidade,estado_clima,origem");
            for(var utc=first.ToUniversalTime();utc<end;utc=utc.AddSeconds(10)){
                var local=utc.ToLocalTime();var day=(local.Date-first).Days;double hour=local.TimeOfDay.TotalHours;
                if((day==3&&hour>=11&&hour<13)||(day==6&&hour>=11&&hour<11.75))continue;
                double phase=((local-cycleStart).TotalHours%48+48)%48;double soil=Math.Max(6,Math.Min(90,85-1.6*phase+Math.Sin(phase*1.7)*0.6));
                double light=Math.Max(0,Math.Sin((hour-6)*Math.PI/12))*84;if(day==2)light*=0.55;if(day==4&&hour>=13&&hour<13.5)light*=0.2;
                int soilRaw=(int)Math.Round(cal.Dry.Value+(cal.Wet.Value-cal.Dry.Value)*soil/100),lightRaw=(int)Math.Round(cal.Dark.Value+(cal.Bright.Value-cal.Dark.Value)*light/100);
                writer.WriteLine(string.Join(",",new[]{utc.ToString("o"),plant.LocalId,plant.Name,soilRaw.ToString(),lightRaw.ToString(),soil.ToString("0.0",CultureInfo.InvariantCulture),light.ToString("0.0",CultureInfo.InvariantCulture),"","","","Perfil da planta","simulado","simulado"}.Select(Store.CsvCell)));
            }
        }
        var settings=new Settings{Plants=new List<Plant>{plant},ActiveId=plant.LocalId,Calibration=cal,SetupDismissed=true,SkipInitialCredentialImport=true};
        File.WriteAllText(Path.Combine(folder,"configuracao-exemplo.json"),Json.Write(settings),new UTF8Encoding(false));
        using(var writer=new StreamWriter(Path.Combine(folder,"regas.csv"),false,new UTF8Encoding(true))){writer.WriteLine("utc,planta,volume_ml,origem");foreach(var r in plant.Care.Waterings)writer.WriteLine(string.Join(",",new[]{r.FinishedUtc.ToString("o"),plant.Name,r.TotalMl.ToString(),"simulado"}.Select(Store.CsvCell)));}
        var snapshot=DailyHistory.Read(history,plant.LocalId,plant.Care.Waterings,new DateTime[0],7,last,true);
        CareReports.Csv(Path.Combine(folder,"resumo.csv"),plant,snapshot);CareReports.Pdf(Path.Combine(folder,"relatorio.pdf"),plant,snapshot,now);
        File.WriteAllText(Path.Combine(folder,"LEIA-ME.txt"),"HISTÓRICO DA PLANTA\r\nDados simulados a partir do perfil cadastrado. Não são medições reais e não devem orientar a rega.\r\n\r\nO histórico tem sete dias, ciclos de secagem, regas ilustrativas, variação diária de luz, sombra e lacunas de coleta. A coluna origem dos CSV contém simulado.\r\n\r\nconfiguracao-exemplo.json: perfil da planta e referências locais, sem credenciais.\r\nhistorico.csv: medições ilustrativas no formato do app.\r\nregas.csv: volumes inventados e marcados como simulação.\r\nresumo.csv e relatorio.pdf: exemplos de exportação.\r\n\r\nO app não carrega este conteúdo no histórico real. Não renomeie nem importe estes arquivos como dados reais.\r\n",new UTF8Encoding(true));
        return new DemoHistoryBundle{Folder=folder,HistoryPath=history,Plant=plant,CreatedUtc=now,EndUtc=last};
    }
}
public partial class MainForm {
    bool historyFiction;Task<DemoHistoryBundle> demoHistoryTask;DemoHistoryBundle demoHistoryBundle;string demoHistoryKey;
    bool ExhibitPausesNotifications {get{return page=="Apresentação"||(page=="Histórico"&&historyFiction);}}
    void ChangeExhibitPause(){if(ExhibitPausesNotifications){StopWhatsAppReceiver();telegramGeneration++;}else if(backgroundInitialized)RestartWhatsAppReceiver();}
    async Task<DemoHistoryBundle> GetDemoHistory(){
        var selected=Active==null?null:Json.Read<Plant>(Json.Write(Active));var calibration=Json.Read<Calibration>(Json.Write(store.Settings.Calibration));var key=selected==null?"sem-planta":selected.LocalId+"|"+selected.Name+"|"+selected.Scientific+"|"+selected.Watering+"|"+selected.Sunlight;key+="|"+Json.Write(calibration)+"|"+DateTime.Today.ToString("yyyyMMdd");
        if(demoHistoryKey!=key){demoHistoryKey=key;demoHistoryBundle=null;demoHistoryTask=null;}if(demoHistoryBundle!=null)return demoHistoryBundle;
        if(demoHistoryTask==null)demoHistoryTask=Task.Run(()=>DemoHistory.Generate(Path.Combine(store.Folder,"Demonstracao"),DateTime.UtcNow,selected,calibration));var task=demoHistoryTask;
        try{var data=await task;if(key==demoHistoryKey)demoHistoryBundle=data;return data;}catch{if(task==demoHistoryTask)demoHistoryTask=null;throw;}
    }
    void ShowDemoHistory(){historyFiction=true;Navigate("Histórico");}
    async void OpenDemoFiles(){try{var data=await GetDemoHistory();if(!closing)OpenFolder(data.Folder);}catch(Exception ex){if(!closing)ShowError(ex);}}
    async void ExportDemoRaw(){try{var data=await GetDemoHistory();if(closing)return;using(var dialog=new SaveFileDialog{Filter="Medições CSV|*.csv",FileName="historico.csv"}){if(dialog.ShowDialog(this)!=DialogResult.OK||!ExportPathAllowed(dialog.FileName))return;File.Copy(data.HistoryPath,dialog.FileName,true);Status("Histórico exportado.");}}catch(Exception ex){if(!closing)ShowError(ex);}}
}
}
