using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Raiz {
public class ActionRow:FlowLayoutPanel {
    bool sizing;
    public ActionRow(){WrapContents=true;Margin=new Padding(0,4,0,12);Padding=Padding.Empty;Dock=DockStyle.Top;MinimumSize=new Size(0,52);}
    protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);if(!(e.Control is Button)){var m=e.Control.Margin;m.Right=Math.Max(12,m.Right);m.Top=Math.Max(m.Top,Math.Max(0,(44-e.Control.Height)/2));e.Control.Margin=m;}}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(sizing)return;sizing=true;try{int bottom=Controls.Cast<Control>().Where(c=>c.Visible||!Visible).Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(0).Max();Height=Math.Max(MinimumSize.Height,bottom+4);}finally{sizing=false;}}
}
public class ReadableCard:Panel {
    protected readonly Label title=new Label(),body=new Label();protected readonly Panel stripe=new Panel();bool sizing;
    public ReadableCard(){DoubleBuffered=true;BackColor=Color.White;Margin=new Padding(0,0,0,14);Padding=new Padding(22);MinimumSize=new Size(0,112);
        title.Font=Theme.F(13,FontStyle.Bold);title.ForeColor=Theme.Ink;body.Font=Theme.F(10);body.ForeColor=Theme.Muted;title.AutoSize=false;body.AutoSize=false;
        stripe.Dock=DockStyle.None;stripe.Width=4;stripe.BackColor=Theme.Line;Controls.AddRange(new Control[]{stripe,title,body});Resize+=(s,e)=>Reflow();}
    public void SetText(string heading,string detail){if(title.Text==heading&&body.Text==detail)return;title.Text=heading;body.Text=detail;Reflow();}
    public void Reflow(){if(sizing||ClientSize.Width<50)return;sizing=true;try{int width=Math.Max(40,ClientSize.Width-48);var flags=TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding;
        int a=Math.Max(title.Font.Height,TextRenderer.MeasureText(title.Text,title.Font,new Size(width,10000),flags).Height);
        int b=Math.Max(body.Font.Height,TextRenderer.MeasureText(body.Text,body.Font,new Size(width,10000),flags).Height);
        title.SetBounds(24,20,width,a+4);body.SetBounds(24,20+a+14,width,b+6);Height=Math.Max(MinimumSize.Height,body.Bottom+22);stripe.SetBounds(0,0,4,Height);
    }finally{sizing=false;}}
}
public class PageHeading:ReadableCard {
    public PageHeading(string text,string subtitle){BackColor=Theme.Bg;stripe.Visible=false;title.Font=Theme.F(25,FontStyle.Bold);Padding=Padding.Empty;MinimumSize=new Size(0,100);SetText(text,subtitle);}
}
public partial class MainForm {
    void Disclosure(TableLayoutPanel parent,string caption,Action<TableLayoutPanel> build,bool expanded=false){
        var shell=new TableLayoutPanel{ColumnCount=1,RowCount=2,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Margin=new Padding(0,4,0,14)};
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var toggle=Theme.Button((expanded?"−  ":"+  ")+caption,(s,e)=>{});toggle.Dock=DockStyle.Top;toggle.TextAlign=ContentAlignment.MiddleLeft;toggle.Margin=new Padding(0,0,0,8);
        var bodyStack=new TableLayoutPanel{ColumnCount=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Margin=Padding.Empty,Padding=new Padding(12,8,0,0)};
        bodyStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.Controls.Add(toggle,0,0);shell.Controls.Add(bodyStack,0,1);build(bodyStack);bodyStack.Visible=expanded;
        toggle.Click+=(s,e)=>{bodyStack.Visible=!bodyStack.Visible;toggle.Text=(bodyStack.Visible?"−  ":"+  ")+caption;shell.PerformLayout();parent.PerformLayout();};
        Add(parent,shell,52);
    }
}
}
