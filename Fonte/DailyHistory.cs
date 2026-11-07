using System;using System.Collections.Generic;using System.Drawing;using System.Globalization;using System.IO;using System.Linq;using System.Text;using System.Threading.Tasks;using System.Windows.Forms;
namespace Raiz {
public class HistoryPoint {public DateTime Utc;public double? Soil,Light;public int Segment;}
public class HistoryDay {
    public DateTime Date;public int Count,Waterings,UnknownVolumes;public int KnownMl;public double CoveredSeconds,ElapsedSeconds;public double? SoilMin,SoilMax,LastSoil;
    public double MissingMinutes {get{return Math.Max(0,ElapsedSeconds-CoveredSeconds)/60;}}
}
public class DailySnapshot {public bool Simulated;public List<HistoryPoint> Points=new List<HistoryPoint>();public List<HistoryDay> Days=new List<HistoryDay>();public List<WateringRecord> Waterings=new List<WateringRecord>();public List<LightHour> LightHours=Enumerable.Range(0,24).Select(h=>new LightHour{Hour=h}).ToList();public DateTime? LastCalibrationUtc;public bool Truncated;public int Invalid;}
public static class DailyHistory {
    public static string[] Csv(string line){
        if(line==null||line.Length>8192)return null;var fields=new List<string>();var b=new StringBuilder();bool quoted=false;
        for(int i=0;i<line.Length;i++){char c=line[i];if(c=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){b.Append('"');i++;}else quoted=!quoted;}else if(c==','&&!quoted){fields.Add(b.ToString());b.Clear();}else b.Append(c);}
        if(quoted)return null;fields.Add(b.ToString());return fields.ToArray();
    }
    static double? Number(string s){double n;return double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0&&n<=100?(double?)n:null;}
    public static DailySnapshot Read(string path,string plantId,IEnumerable<WateringRecord> waterings,IEnumerable<DateTime> calibrationChanges,int days,DateTime now,bool simulated=false){
        days=Math.Max(1,Math.Min(30,days));var result=new DailySnapshot{Simulated=simulated};DateTime first=now.ToLocalTime().Date.AddDays(1-days),from=first.ToUniversalTime();
        var map=new Dictionary<DateTime,HistoryDay>();for(int i=0;i<days;i++){var date=first.AddDays(i);var day=new HistoryDay{Date=date,ElapsedSeconds=Math.Max(0,((now<date.AddDays(1).ToUniversalTime()?now:date.AddDays(1).ToUniversalTime())-date.ToUniversalTime()).TotalSeconds)};map.Add(date,day);result.Days.Add(day);}
        result.Waterings=(waterings??new WateringRecord[0]).Where(x=>x.FinishedUtc>=from&&x.FinishedUtc<=now).ToList();
        foreach(var r in result.Waterings){HistoryDay day;if(map.TryGetValue(r.FinishedUtc.ToLocalTime().Date,out day)){day.Waterings++;if(r.VolumeUnknown)day.UnknownVolumes++;else day.KnownMl+=r.TotalMl;}}
        if(!File.Exists(path))return result;
        var changes=(calibrationChanges??new DateTime[0]).OrderBy(x=>x).ToArray();result.LastCalibrationUtc=changes.Where(x=>x<=now).Select(x=>(DateTime?)x).LastOrDefault();DateTime? previous=null;double? previousLight=null,previousSoil=null;int segment=0;long lastBin=-1;
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){
            if(file.Length>80000000){file.Seek(-80000000,SeekOrigin.End);result.Truncated=true;}
            using(var reader=new StreamReader(file,Encoding.UTF8,true)){if(result.Truncated)reader.ReadLine();string line;
                while((line=reader.ReadLine())!=null){var f=Csv(line);DateTime utc;if(f==null||f.Length<13){result.Invalid++;continue;}if(f[0]=="utc")continue;
                    if(f[1]!=plantId||f[12]!=(simulated?"simulado":"real"))continue;
                    if(!DateTime.TryParse(f[0],CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out utc)){result.Invalid++;continue;}
                    if(utc<from||utc>now)continue;if(previous.HasValue&&utc<previous.Value){result.Invalid++;continue;}
                    var day=map[utc.ToLocalTime().Date];var soil=Number(f[5]);var light=Number(f[6]);day.Count++;
                    if(soil.HasValue){day.SoilMin=!day.SoilMin.HasValue?soil:Math.Min(day.SoilMin.Value,soil.Value);day.SoilMax=!day.SoilMax.HasValue?soil:Math.Max(day.SoilMax.Value,soil.Value);day.LastSoil=soil;}
                    bool split=!previous.HasValue||utc-previous.Value>=TimeSpan.FromSeconds(12)||changes.Any(x=>previous.Value<x&&x<=utc);
                    if(split)segment++;else {
                        if(previous.Value.ToLocalTime().Date==day.Date)day.CoveredSeconds+=(utc-previous.Value).TotalSeconds;
                        if(light.HasValue&&previousLight.HasValue&&(!result.LastCalibrationUtc.HasValue||previous.Value>=result.LastCalibrationUtc.Value)){var cursor=previous.Value;while(cursor<utc){var local=cursor.ToLocalTime();long ticks=Math.Min((utc-cursor).Ticks,TimeSpan.TicksPerHour-local.TimeOfDay.Ticks%TimeSpan.TicksPerHour);double seconds=ticks/(double)TimeSpan.TicksPerSecond;var hour=result.LightHours[local.Hour];hour.Seconds+=seconds;hour.WeightedIndex+=seconds*(light.Value+previousLight.Value)/2;cursor=cursor.AddTicks(ticks);}}
                        // Missing calibrated values must also interrupt the corresponding trend.
                        if(soil.HasValue!=previousSoil.HasValue||light.HasValue!=previousLight.HasValue)segment++;
                    }
                    long bin=utc.Ticks/TimeSpan.FromMinutes(5).Ticks;
                    var point=new HistoryPoint{Utc=utc,Soil=soil,Light=light,Segment=segment};
                    if(result.Points.Count>0&&bin==lastBin&&result.Points.Last().Segment==segment)result.Points[result.Points.Count-1]=point;else result.Points.Add(point);
                    lastBin=bin;previous=utc;previousLight=light;previousSoil=soil;
                }
            }
        }return result;
    }
}
public class DailyChart:Control {
    public DailySnapshot Snapshot;public DateTime Day=DateTime.Today;public bool WholePeriod;
    public DailyChart(){DoubleBuffered=true;BackColor=Color.White;Dock=DockStyle.Fill;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;int left=46,right=Width-20,top=38,bottom=Height-38;if(right<=left||bottom<=top)return;
        using(var font=Theme.F(9))using(var brush=new SolidBrush(Theme.Muted))using(var grid=new Pen(Theme.Line)){
            g.DrawString("Solo / luz · índice relativo de 0 a 100",font,brush,left,10);
            var first=WholePeriod&&Snapshot!=null&&Snapshot.Days.Count>0?Snapshot.Days.First().Date:Day.Date;var end=WholePeriod&&Snapshot!=null&&Snapshot.Days.Count>0?Snapshot.Days.Last().Date.AddDays(1):Day.Date.AddDays(1);
            for(int n=0;n<=4;n++){float y=bottom-(bottom-top)*n/4f;g.DrawLine(grid,left,y,right,y);g.DrawString((n*25).ToString(),font,brush,8,y-7);string caption=WholePeriod?first.AddTicks((end-first).Ticks*n/4).ToString("dd/MM"):(n*6).ToString("00")+":00";g.DrawString(caption,font,brush,left+(right-left)*n/4f-15,bottom+9);}
            var data=Snapshot==null?new List<HistoryPoint>():Snapshot.Points.Where(x=>x.Utc>=first.ToUniversalTime()&&x.Utc<end.ToUniversalTime()).ToList();
            if(data.Count==0){g.DrawString("Sem medições neste dia. Nenhum valor foi presumido.",font,brush,left+16,top+30);}else if(data.All(x=>!x.Soil.HasValue&&!x.Light.HasValue))g.DrawString("Há leituras brutas, mas faltam índices calibrados neste dia.",font,brush,left+16,top+30);
            Func<DateTime,float> xcoord=utc=>left+(right-left)*(float)((utc-first.ToUniversalTime()).TotalSeconds/(end.ToUniversalTime()-first.ToUniversalTime()).TotalSeconds);
            using(var soilPen=new Pen(Theme.Green,2))using(var lightPen=new Pen(Theme.Amber,2)){
                for(int i=0;i<data.Count;i++){var b=data[i];float x=xcoord(b.Utc);if(b.Soil.HasValue)g.FillEllipse(Brushes.SeaGreen,x-1,bottom-(float)b.Soil.Value*(bottom-top)/100-1,3,3);
                    if(b.Light.HasValue)g.FillEllipse(Brushes.Goldenrod,x-1,bottom-(float)b.Light.Value*(bottom-top)/100-1,3,3);
                    if(i==0||data[i-1].Segment!=b.Segment)continue;var a=data[i-1];
                    if(a.Soil.HasValue&&b.Soil.HasValue)g.DrawLine(soilPen,xcoord(a.Utc),bottom-(float)a.Soil.Value*(bottom-top)/100,x,bottom-(float)b.Soil.Value*(bottom-top)/100);
                    if(a.Light.HasValue&&b.Light.HasValue)g.DrawLine(lightPen,xcoord(a.Utc),bottom-(float)a.Light.Value*(bottom-top)/100,x,bottom-(float)b.Light.Value*(bottom-top)/100);
                }
            }
            if(Snapshot!=null)using(var waterPen=new Pen(Color.SteelBlue,1)){waterPen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;foreach(var r in Snapshot.Waterings.Where(x=>x.FinishedUtc>=first.ToUniversalTime()&&x.FinishedUtc<end.ToUniversalTime())){float x=xcoord(r.FinishedUtc);g.DrawLine(waterPen,x,top,x,bottom);g.DrawString("rega",font,Brushes.SteelBlue,x+3,top+5);}}
        }
    }
}
public partial class MainForm {
    int historyGeneration;bool historyLoading,historyReloadRequested;DailyChart dailyChart;DataGridView dailyGrid;ReadableCard dailySummary,evolutionCard;ComboBox historyPeriod;
    void HistoryPage(TableLayoutPanel stack){
        Heading(stack,historyFiction?"Histórico de "+(Active==null?"sua planta":Active.Name):"Histórico diário",historyFiction?"Dados simulados · sete dias completos · verde: solo · dourado: luz · azul: rega":Active==null?"Escolha uma planta para consultar seu histórico.":Active.Name+" · verde: solo · dourado: luz · azul tracejado: rega");
        var source=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=230,AccessibleName="Fonte do histórico"};source.Items.AddRange(new object[]{"Meus dados reais","Simulação de 7 dias"});source.SelectedIndex=historyFiction?1:0;source.SelectedIndexChanged+=(s,e)=>{bool previous=ExhibitPausesNotifications;historyFiction=source.SelectedIndex==1;if(previous!=ExhibitPausesNotifications)ChangeExhibitPause();Navigate("Histórico");};var sourceRow=Buttons(source,Theme.Button("Voltar à apresentação",(s,e)=>Navigate("Apresentação")));if(historyFiction)sourceRow.Controls.Add(Theme.Button("Abrir arquivos do histórico",(s,e)=>OpenDemoFiles()));Add(stack,sourceRow,54);
        historyPeriod=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=140};historyPeriod.Items.AddRange(new object[]{"Últimos 7 dias","Últimos 14 dias","Últimos 30 dias"});historyPeriod.SelectedIndex=0;
        var scope=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=180,AccessibleName="Visão do gráfico"};scope.Items.AddRange(new object[]{"Semana completa","Dia selecionado"});scope.SelectedIndex=0;var periodRow=Buttons(historyPeriod,Theme.Button("Atualizar histórico",(s,e)=>LoadDailyHistory(),true));if(historyFiction){historyPeriod.Enabled=false;periodRow.Controls.Add(scope);}Add(stack,periodRow,56);
        dailySummary=new ReadableCard();dailySummary.SetText("Carregando histórico","Consultando as medições locais desta planta…");Add(stack,dailySummary,112);
        evolutionCard=new ReadableCard();evolutionCard.SetText("O que mudou neste vaso?","Aguardando o histórico da planta.");
        dailyGrid=Grid(new[]{"Dia","Solo mín.–máx.","Último solo","Regas","Sem cobertura"});dailyGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        dailyChart=new DailyChart{WholePeriod=historyFiction};scope.SelectedIndexChanged+=(s,e)=>{if(dailyChart!=null&&!dailyChart.IsDisposed){dailyChart.WholePeriod=scope.SelectedIndex==0;dailyChart.Invalidate();}};if(historyFiction){Add(stack,dailyChart,290);Add(stack,evolutionCard,150);Add(stack,dailyGrid,180);}else{Add(stack,evolutionCard,150);Add(stack,dailyGrid,180);Add(stack,dailyChart,290);}
        Note(stack,"Como ler o gráfico","Selecione um dia na tabela. Trechos sem curva indicam falta de leituras; não são umidade zero. A curva usa o último ponto de cada trecho contínuo de cinco minutos, enquanto a tabela considera todas as leituras. Horários deste computador. Índices pertencem à calibração da época; mudanças registradas de calibração interrompem a curva.");
        dailyGrid.SelectionChanged+=(s,e)=>{if(!historyLoading&&dailyGrid.CurrentRow!=null&&dailyGrid.CurrentRow.Tag is DateTime){dailyChart.Day=(DateTime)dailyGrid.CurrentRow.Tag;if(historyFiction)scope.SelectedIndex=1;dailyChart.Invalidate();}};
        historyPeriod.SelectedIndexChanged+=(s,e)=>LoadDailyHistory();
        if(!historyFiction)Disclosure(stack,"Regas registradas",details=>AddWateringLog(details));
        Add(stack,Buttons(Theme.Button("Relatório PDF",(s,e)=>ExportCareReport(true),true),Theme.Button("Resumo e regas CSV",(s,e)=>ExportCareReport(false)),Theme.Button("Medições brutas CSV",(s,e)=>ExportHistory()),Theme.Button(historyFiction?"Abrir arquivos do histórico":"Abrir pasta de dados",(s,e)=>{if(historyFiction)OpenDemoFiles();else OpenFolder(store.Folder);})),56);LoadDailyHistory();
    }
    async void LoadDailyHistory(){
        if(page!="Histórico"||dailyChart==null||dailyChart.IsDisposed)return;
        if(historyLoading){historyReloadRequested=true;historyGeneration++;return;}historyReloadRequested=false;
        var chart=dailyChart;var grid=dailyGrid;var summary=dailySummary;int generation=historyGeneration;bool fiction=historyFiction;var plant=fiction?new Plant{Name="Demonstração fictícia"}:Active;
        if(plant==null){summary.SetText("Escolha uma planta","O histórico diário é separado por vaso.");return;}
        int days=new[]{7,14,30}[historyPeriod.SelectedIndex];var water=Json.Read<System.Collections.Generic.List<WateringRecord>>(Json.Write(plant.Care.Waterings));var changes=store.Settings.CalibrationChanges.ToArray();string historyPath=store.HistoryPath;var now=DateTime.UtcNow;historyLoading=true;
        try{if(fiction){summary.SetText("Preparando o histórico da planta","Gerando sete dias de exemplo em uma pasta separada. Seus dados reais continuam preservados.");var data=await GetDemoHistory();plant=data.Plant;water=plant.Care.Waterings;historyPath=data.HistoryPath;now=data.EndUtc;changes=new DateTime[0];}var snapshot=await Task.Run(()=>DailyHistory.Read(historyPath,plant.LocalId,water,changes,days,now,fiction));if(closing||generation!=historyGeneration||chart.IsDisposed)return;
            var selectedDay=chart.Day;chart.Snapshot=snapshot;if(evolutionCard!=null&&!evolutionCard.IsDisposed)evolutionCard.SetText("O que mudou neste vaso?",Evolution.Describe(snapshot,plant));grid.Rows.Clear();foreach(var d in snapshot.Days.OrderByDescending(x=>x.Date)){int row=grid.Rows.Add(d.Date.ToString("dd/MM"),d.SoilMin.HasValue?d.SoilMin.Value.ToString("0")+"–"+d.SoilMax.Value.ToString("0")+"%":"—",d.LastSoil.HasValue?d.LastSoil.Value.ToString("0")+"%":"—",d.Waterings+" · "+d.KnownMl+" mL"+(d.UnknownVolumes>0?" + volume desconhecido":""),Math.Round(d.MissingMinutes)+" min");grid.Rows[row].Tag=d.Date;}
            var selectedRow=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>r.Tag is DateTime&&(DateTime)r.Tag==selectedDay.Date);if(selectedRow!=null)grid.CurrentCell=selectedRow.Cells[0];else if(grid.Rows.Count>0)grid.CurrentCell=grid.Rows[0].Cells[0];
            if(grid.CurrentRow!=null&&grid.CurrentRow.Tag is DateTime)chart.Day=(DateTime)grid.CurrentRow.Tag;
            summary.SetText(fiction?"Sete dias de acompanhamento":"Evolução e regas por dia",snapshot.Days.Sum(x=>x.Count)+(fiction?" leituras de "+plant.Name+". Período: "+snapshot.Days.First().Date.ToString("dd/MM")+" a "+snapshot.Days.Last().Date.ToString("dd/MM")+". Selecione um dia na tabela para comparar a evolução.":" leituras reais no período. A coluna sem cobertura mostra o tempo não observado por leituras contínuas; inclui computador desligado, desconexão e intervalos sem coleta.")+(snapshot.Truncated?" Leitura limitada aos últimos 80 MB do arquivo.":"")+(snapshot.Invalid>0?" Linhas incompletas ou inválidas ignoradas: "+snapshot.Invalid+".":""));chart.Invalidate();
        }catch{if(!summary.IsDisposed)summary.SetText("Histórico indisponível","Não foi possível ler o arquivo agora. Tente atualizar; os dados não foram alterados.");}
        finally{historyLoading=false;if(historyReloadRequested&&!closing&&page=="Histórico")LoadDailyHistory();}
    }
}
}
