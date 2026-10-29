using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Raiz {
public static class PlotData {
    public static List<Sample> Visible(IList<Sample> all,DateTime anchor,int seconds){return all.Where(s=>s.Utc<=anchor&&s.Utc>=anchor.AddSeconds(-seconds)).ToList();}
    public static bool Join(Sample a,Sample b){return b.Utc>=a.Utc&&b.Utc-a.Utc<TimeSpan.FromSeconds(12);}
    public static double? Value(Sample s,Calibration c,bool soil,bool normalized){return normalized?(soil?c.Soil(s.Soil):c.Light(s.Light)):(double?)(soil?s.Soil:s.Light);}
}
public class SerialPlot : Control {
    public List<Sample> Data=new List<Sample>();public Calibration Calibration=new Calibration();
    public bool ShowSoil=true,ShowLight=true,Normalized;public int Seconds=120;public DateTime? FrozenUtc;
    public string HoverText {get;private set;}
    public event EventHandler HoverChanged;
    public SerialPlot(){DoubleBuffered=true;BackColor=Color.White;Dock=DockStyle.Fill;MouseMove+=Hover;MouseLeave+=(s,e)=>{HoverText="";if(HoverChanged!=null)HoverChanged(this,EventArgs.Empty);};}
    public DateTime TimeAnchor {get {return FrozenUtc??DateTime.UtcNow;}}
    void Hover(object sender,MouseEventArgs e) {
        var visible=PlotData.Visible(Data,TimeAnchor,Seconds);if(visible.Count==0)return;
        double fraction=Math.Max(0,Math.Min(1,(e.X-45.0)/Math.Max(1,Width-65)));
        var time=TimeAnchor.AddSeconds(-Seconds+Seconds*fraction);var nearest=visible.OrderBy(x=>Math.Abs((x.Utc-time).TotalMilliseconds)).First();
        HoverText=nearest.Utc.ToLocalTime().ToString("HH:mm:ss")+" · solo "+nearest.Soil+" · luz "+nearest.Light;
        if(Normalized)HoverText+=" · índices "+Format(Calibration.Soil(nearest.Soil))+" / "+Format(Calibration.Light(nearest.Light));
        if(HoverChanged!=null)HoverChanged(this,EventArgs.Empty);
    }
    static string Format(double? n){return n.HasValue?n.Value.ToString("0.0")+"%":"sem calibração";}
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        int left=45,right=Width-18,top=10,bottom=Height-27;if(right<=left||bottom<=top)return;
        DateTime anchor=TimeAnchor;var list=PlotData.Visible(Data,anchor,Seconds);double max=Normalized?100:1023;
        using(var grid=new Pen(Theme.Line))using(var font=Theme.F(8))using(var brush=new SolidBrush(Theme.Muted)) {
            for(int i=0;i<=4;i++){int y=bottom-(bottom-top)*i/4;g.DrawLine(grid,left,y,right,y);g.DrawString((max*i/4).ToString("0"),font,brush,2,y-6);}
            for(int i=0;i<=4;i++){float x=left+(right-left)*i/4f;g.DrawLine(grid,x,top,x,bottom);string text=anchor.AddSeconds(-Seconds+Seconds*i/4.0).ToLocalTime().ToString("HH:mm:ss");g.DrawString(text,font,brush,Math.Min(right-45,Math.Max(left,x-20)),bottom+8);}
            if(list.Count==0){g.DrawString("Aguardando medições. Conecte o Arduino no app; feche o Monitor Serial da IDE.",font,brush,left+12,top+22);return;}
            DrawSeries(g,list,anchor,true,ShowSoil,Theme.Green,left,right,top,bottom,max);
            DrawSeries(g,list,anchor,false,ShowLight,Color.FromArgb(190,134,45),left,right,top,bottom,max);
            if(Normalized&&!list.Any(s=>Calibration.Soil(s.Soil).HasValue||Calibration.Light(s.Light).HasValue))g.DrawString("Calibre os sensores para visualizar índices relativos.",font,brush,left+12,top+22);
        }
    }
    void DrawSeries(Graphics g,List<Sample> list,DateTime anchor,bool soil,bool show,Color color,int left,int right,int top,int bottom,double max) {
        if(!show)return;
        using(var pen=new Pen(color,1.8f))using(var brush=new SolidBrush(color)) {
            PointF? previous=null;Sample old=null;
            foreach(var s in list) {
                var value=PlotData.Value(s,Calibration,soil,Normalized);if(!value.HasValue){previous=null;old=null;continue;}
                float x=left+(right-left)*(float)((s.Utc-anchor.AddSeconds(-Seconds)).TotalSeconds/Seconds);
                float y=bottom-(bottom-top)*(float)(value.Value/max);var point=new PointF(x,y);
                if(previous.HasValue&&PlotData.Join(old,s))g.DrawLine(pen,previous.Value,point);
                else g.FillEllipse(brush,x-2,y-2,4,4);
                previous=point;old=s;
            }
        }
    }
}
public class PlotterPanel : UserControl {
    readonly SerialPlot plot=new SerialPlot();readonly Label status,hover;readonly Button pause;
    List<Sample> live=new List<Sample>();Calibration calibration=new Calibration();bool frozen;string previousScope;bool previousDemo;
    public event EventHandler OpenRequested;
    public PlotterPanel(bool popout) {
        Dock=DockStyle.Fill;BackColor=Color.White;Padding=new Padding(5);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1,Margin=Padding.Empty};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,22));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,22));Controls.Add(layout);
        var tools=new ActionRow{Margin=Padding.Empty,MinimumSize=new Size(0,42)};
        tools.Controls.Add(new Label{Text="Plotter serial",Font=Theme.F(10,FontStyle.Bold),ForeColor=Theme.Ink,AutoSize=true,Margin=new Padding(3,5,14,0)});
        var soil=new CheckBox{Text="Solo",Checked=true,AutoSize=true,ForeColor=Theme.Green,Margin=new Padding(0,4,10,0)};
        var light=new CheckBox{Text="Luz",Checked=true,AutoSize=true,ForeColor=Theme.Amber,Margin=new Padding(0,4,12,0)};
        var mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=138,Font=Theme.F(9)};mode.Items.AddRange(new object[]{"Valores brutos","Índices relativos"});mode.SelectedIndex=0;
        var period=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=83,Font=Theme.F(9)};period.Items.AddRange(new object[]{"30 s","2 min","5 min","15 min","1 hora"});period.SelectedIndex=1;
        pause=new Button{Text="Pausar",Width=82,Height=34,FlatStyle=FlatStyle.Flat,Font=Theme.F(9)};pause.FlatAppearance.BorderColor=Theme.Line;
        soil.CheckedChanged+=(s,e)=>{plot.ShowSoil=soil.Checked;plot.Invalidate();};light.CheckedChanged+=(s,e)=>{plot.ShowLight=light.Checked;plot.Invalidate();};
        mode.SelectedIndexChanged+=(s,e)=>{plot.Normalized=mode.SelectedIndex==1;plot.Invalidate();};
        period.SelectedIndexChanged+=(s,e)=>{plot.Seconds=new[]{30,120,300,900,3600}[period.SelectedIndex];plot.Invalidate();};
        pause.Click+=(s,e)=>{frozen=!frozen;pause.Text=frozen?"Retomar":"Pausar";plot.FrozenUtc=frozen?(DateTime?)DateTime.UtcNow:null;plot.Data=frozen?live.ToList():live;plot.Invalidate();};
        tools.Controls.AddRange(new Control[]{soil,light,mode,period,pause});
        if(!popout){var open=new Button{Text="Ampliar",Width=82,Height=34,FlatStyle=FlatStyle.Flat,Font=Theme.F(9)};open.FlatAppearance.BorderColor=Theme.Line;open.Click+=(s,e)=>{if(OpenRequested!=null)OpenRequested(this,EventArgs.Empty);};tools.Controls.Add(open);}
        status=new Label{Dock=DockStyle.Fill,Font=Theme.F(8),ForeColor=Theme.Muted,Padding=new Padding(5,0,0,0)};
        hover=new Label{Dock=DockStyle.Fill,Font=Theme.F(8),ForeColor=Theme.Muted,Padding=new Padding(5,0,0,0)};
        plot.HoverChanged+=(s,e)=>hover.Text=string.IsNullOrEmpty(plot.HoverText)?"Passe o mouse nas curvas para consultar os valores. Pausar congela só o desenho; a coleta continua.":plot.HoverText;
        hover.Text="Passe o mouse nas curvas para consultar os valores. Pausar congela só o desenho; a coleta continua.";
        layout.Controls.Add(tools,0,0);layout.Controls.Add(status,0,1);layout.Controls.Add(plot,0,2);layout.Controls.Add(hover,0,3);
    }
    public void RefreshData(List<Sample> samples,Calibration c,string plantName,string scope,bool demo) {
        if(scope!=previousScope||demo!=previousDemo){frozen=false;plot.FrozenUtc=null;pause.Text="Pausar";previousScope=scope;previousDemo=demo;}
        live=samples;calibration=c;plot.Calibration=calibration;if(!frozen)plot.Data=live;
        var last=live.LastOrDefault();
        status.Text=(demo?"DEMONSTRAÇÃO · ":"")+(plantName??"Sem planta")+" · "+(last==null?"aguardando dados":(last.Fresh(DateTime.UtcNow)?"ao vivo":"SEM LEITURA ATUAL")+" · solo "+last.Soil+" · luz "+last.Light+" · "+last.Utc.ToLocalTime().ToString("HH:mm:ss"))+(frozen?" · gráfico pausado":"");
        plot.Invalidate();
    }
}
public partial class MainForm {
    PlotterPanel dockPlotter,largePlotter;Form plotterWindow;
    void UpdatePlotters(){string scope=(store.Settings.ActiveId??"")+"|"+Json.Write(store.Settings.Calibration);if(dockPlotter!=null&&!dockPlotter.IsDisposed)dockPlotter.RefreshData(samples,store.Settings.Calibration,Active==null?null:Active.Name,scope,demo);if(largePlotter!=null&&!largePlotter.IsDisposed)largePlotter.RefreshData(samples,store.Settings.Calibration,Active==null?null:Active.Name,scope,demo);}
    void OpenPlotter() {
        if(plotterWindow!=null&&!plotterWindow.IsDisposed){plotterWindow.Show();plotterWindow.Activate();return;}
        plotterWindow=new Form{Text="Raiz · Plotter serial ao vivo",Size=new Size(1060,620),MinimumSize=new Size(850,390),StartPosition=FormStartPosition.CenterParent,BackColor=Color.White};
        largePlotter=new PlotterPanel(true);plotterWindow.Controls.Add(largePlotter);plotterWindow.FormClosed+=(s,e)=>{largePlotter=null;plotterWindow=null;};plotterWindow.Show(this);UpdatePlotters();
    }
}
}
