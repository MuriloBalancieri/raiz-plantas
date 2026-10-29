using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Raiz {
public class Metric : Panel {
    public Label ValueLabel, SubLabel;
    public Metric(string title,string unit,Color accent) {
        BackColor=Color.White;Margin=new Padding(0,0,12,0);Dock=DockStyle.Fill;Padding=new Padding(18);
        var label=new Label{Text=title,Location=new Point(18,16),AutoSize=true,ForeColor=Theme.Muted,Font=Theme.F(10)};
        ValueLabel=new Label{Text="—",Location=new Point(16,39),AutoSize=true,ForeColor=accent,Font=Theme.F(29,FontStyle.Bold)};
        SubLabel=new Label{Text=unit,Location=new Point(19,94),AutoSize=false,Height=38,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Width=170,ForeColor=Theme.Muted,Font=Theme.F(9)};
        Controls.AddRange(new Control[]{label,ValueLabel,SubLabel});
        Resize+=(s,e)=>SubLabel.Width=Width-36;
        Paint+=(s,e)=>{using(var p=new Pen(Theme.Line)) e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);};
    }
}
public static class Theme {
    public static Color Bg=ColorTranslator.FromHtml("#F3F5F0"), Ink=ColorTranslator.FromHtml("#203A2D"),Muted=ColorTranslator.FromHtml("#617368"),Green=ColorTranslator.FromHtml("#277553"),Side=ColorTranslator.FromHtml("#193C2D"),Line=ColorTranslator.FromHtml("#DCE4DA"),Amber=ColorTranslator.FromHtml("#97621D");
    public static Font F(float size,FontStyle style=FontStyle.Regular){return new Font("Segoe UI",size,style);}
    public static Label Label(string text,float size=10,Color? color=null,bool bold=false) {return new Label{Text=text,Font=F(size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color??Ink,AutoSize=true,Margin=new Padding(0,0,0,8)};}
    public static Button Button(string text,EventHandler action,bool primary=false) {
        var b=new Button{Text=text,AutoSize=true,Height=44,MinimumSize=new Size(105,44),FlatStyle=FlatStyle.Flat,BackColor=primary?Green:Color.White,ForeColor=primary?Color.White:Ink,Font=F(10),Padding=new Padding(16,8,16,8),Cursor=Cursors.Hand,Margin=new Padding(0,0,12,10)};
        b.FlatAppearance.BorderColor=primary?Green:Line;b.Click+=action;return b;
    }
    public static TextBox Input(string value="",int width=250) {return new TextBox{Text=value??"",Width=width,Font=F(11),BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,0,0,10)};}
    public static Panel Card(int height=100) {
        var p=new Panel{BackColor=Color.White,Height=height,Dock=DockStyle.Top,Padding=new Padding(18),Margin=new Padding(0,0,0,12)};
        p.Paint+=(s,e)=>{using(var pen=new Pen(Line))e.Graphics.DrawRectangle(pen,0,0,p.Width-1,p.Height-1);};return p;
    }
}
public class Trend : Control {
    public List<Sample> Samples=new List<Sample>();
    public Trend(){DoubleBuffered=true;BackColor=Color.White;Height=170;Dock=DockStyle.Fill;}
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        int left=42,right=Width-20,top=25,bottom=Height-35;
        using(var grid=new Pen(Theme.Line))using(var font=Theme.F(8))using(var brush=new SolidBrush(Theme.Muted)) {
            for(int i=0;i<=4;i++){int y=bottom-(bottom-top)*i/4;g.DrawLine(grid,left,y,right,y);g.DrawString((1023*i/4).ToString(),font,brush,0,y-6);}
            if(Samples.Count<2){g.DrawString("O gráfico aparece após receber duas leituras.",Theme.F(10),brush,left+12,top+25);return;}
            var data=Samples.Skip(Math.Max(0,Samples.Count-100)).ToList();
            using(var p1=new Pen(Theme.Green,2))using(var p2=new Pen(Color.FromArgb(190,134,45),2)) {
                for(int i=1;i<data.Count;i++) {
                    float x1=left+(right-left)*(i-1f)/(data.Count-1),x2=left+(right-left)*i/(float)(data.Count-1);
                    g.DrawLine(p1,x1,bottom-(bottom-top)*data[i-1].Soil/1023f,x2,bottom-(bottom-top)*data[i].Soil/1023f);
                    g.DrawLine(p2,x1,bottom-(bottom-top)*data[i-1].Light/1023f,x2,bottom-(bottom-top)*data[i].Light/1023f);
                }
            }
            g.DrawString(data[0].Utc.ToLocalTime().ToString("HH:mm:ss"),font,brush,left,bottom+10);
            g.DrawString(data[data.Count-1].Utc.ToLocalTime().ToString("HH:mm:ss"),font,brush,right-55,bottom+10);
        }
    }
}
public partial class MainForm : Form {
    readonly Store store; readonly Panel content=new Panel(); readonly Label footer; readonly Panel side;
    string page="Visão geral"; List<Sample> samples=new List<Sample>();Sample last;
    SerialPort serial; volatile int serialGeneration; bool demo;int tick;
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=1000};
    Metric soilMetric,lightMetric,tempMetric,humidityMetric; Label contextLabel,connectionLabel,weatherStamp;
    FlowLayoutPanel adviceList;string adviceSignature="";Trend trend;
    string weatherError="";bool weatherBusy;DateTime nextWeather=DateTime.MinValue;int locationVersion;
    Plant Active {get {return store.Settings.Plants.Find(p=>p.LocalId==store.Settings.ActiveId);}}
    string initialCredentialWarning;
    public MainForm(string folder,bool importInitialCredentials=true) {
        store=new Store(folder);
        try{if(importInitialCredentials)InitialCredentials.ImportFile(store,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"configuracao-inicial.json"));}catch{initialCredentialWarning="Não foi possível importar a configuração inicial. Seus dados existentes foram preservados; confira Configurações e Avisos.";}
        Text="Raiz · Monitor de plantas";Size=new Size(1240,820);MinimumSize=new Size(1080,680);StartPosition=FormStartPosition.CenterScreen;
        try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
        Font=Theme.F(10);BackColor=Theme.Bg;AutoScaleMode=AutoScaleMode.Dpi;
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=Padding.Empty};
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,200));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,38));Controls.Add(shell);
        side=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Side,Margin=Padding.Empty};shell.Controls.Add(side,0,0);shell.SetRowSpan(side,3);
        side.Controls.Add(new Label{Text="raiz",Font=Theme.F(38,FontStyle.Bold),ForeColor=Color.White,AutoSize=true,Location=new Point(23,28)});
        side.Controls.Add(new Label{Text="CUIDADO COM CONTEXTO",Font=Theme.F(8),ForeColor=Color.FromArgb(166,197,172),AutoSize=true,Location=new Point(27,94)});
        int y=140;
        foreach(string name in new[]{"Visão geral","Minha planta","Cuidados e rega","Telegram e avisos","Arduino e calibração","Histórico","Configurações"}) {
            string captured=name;string label=name=="Visão geral"?"Hoje":name=="Minha planta"?"Minha planta":name=="Cuidados e rega"?"Cuidados":name=="Telegram e avisos"?"Avisos":name=="Arduino e calibração"?"Sensores":name;var b=new Button{Text=label,Location=new Point(14,y),Size=new Size(177,44),FlatStyle=FlatStyle.Flat,ForeColor=Color.White,BackColor=Theme.Side,TextAlign=ContentAlignment.MiddleLeft,Font=Theme.F(10),Cursor=Cursors.Hand,Tag=name};
            b.FlatAppearance.BorderSize=0;b.Click+=(s,e)=>Navigate(captured);side.Controls.Add(b);y+=46;
        }
        var bottom=new Label{Text="Um vaso por vez.\nSensores + cuidados + clima.\n\nVersão 2.1 · Windows",ForeColor=Color.FromArgb(169,196,175),Font=Theme.F(9),Dock=DockStyle.Bottom,Height=110,Padding=new Padding(24,12,10,0)};side.Controls.Add(bottom);
        content.Dock=DockStyle.Fill;content.Margin=Padding.Empty;content.Padding=new Padding(28,24,24,20);shell.Controls.Add(content,1,0);
        dockPlotter=new PlotterPanel(false){Margin=new Padding(28,0,25,5)};dockPlotter.OpenRequested+=(s,e)=>OpenPlotter();shell.Controls.Add(dockPlotter,1,1);
        footer=new Label{Dock=DockStyle.Fill,Text="Tudo começa com uma leitura confiável.",Font=Theme.F(9),ForeColor=Theme.Muted,Padding=new Padding(28,6,0,0),Margin=Padding.Empty};shell.Controls.Add(footer,1,2);
        Navigate("Visão geral");timer.Tick+=(s,e)=>OnTick();timer.Start();
        FormClosing+=(s,e)=>{if(KeepInBackground(e))return;closing=true;if(backgroundIcon!=null){backgroundIcon.Visible=false;backgroundIcon.ContextMenuStrip.Dispose();backgroundIcon.Dispose();}telegramGeneration++;timer.Stop();Disconnect();StopWhatsAppReceiver();whatsappTransport.Dispose();telegramTransport.Dispose();try{store.Save();}catch{}};
        Shown+=async(s,e)=> {
            if(initialCredentialWarning!=null)MessageBox.Show(this,initialCredentialWarning,"Configuração inicial");
            LoadTelegram();InitializeBackground();if(!store.Settings.SetupDismissed&&(Active==null||!store.Settings.Calibration.Soil(500).HasValue))Navigate("Primeiros passos");RestartWhatsAppReceiver();UpdatePlotters();try{store.SaveWateringLog();}catch{Status("Não foi possível atualizar o CSV de regas. Os registros da configuração foram preservados.");}
            if(store.LoadWarning!=null)MessageBox.Show(this,store.LoadWarning,"Configuração preservada");
            if(store.Settings.Location==null) {
                try {
                    var cities=await Api.Cities("Guarulhos");
                    var found=cities.Find(l=>l.Country=="Brasil" && l.Region=="São Paulo");
                    if(found!=null) {store.Settings.Location=found;store.Save();}
                    else weatherError="Escolha Guarulhos, SP em Clima da região.";
                }catch(Exception ex){weatherError=Friendly(ex);}
            }
            await RefreshWeather();if(page=="Visão geral")UpdateDashboard();
        };
    }
    static string Friendly(Exception e) {return e is ApiException?e.Message:"Não foi possível concluir. Confira os dados e a permissão de gravação da pasta do app.";}
    void ShowError(Exception e){MessageBox.Show(this,Friendly(e),"Raiz",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    void Save(){try{store.Save();}catch(Exception e){ShowError(e);}}
    void Status(string text){footer.Text=(demo?"DEMONSTRAÇÃO · ":"")+text;}
    TableLayoutPanel Stack(Control parent) {
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};parent.Controls.Add(scroll);
        var stack=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(0,0,18,0)};
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(stack);return stack;
    }
    void Add(TableLayoutPanel stack,Control c,int height) {
        int i=stack.RowCount++;bool flexible=c is ActionRow||c is ReadableCard||c.AutoSize;
        stack.RowStyles.Add(new RowStyle(flexible?SizeType.AutoSize:SizeType.Absolute,height));c.Dock=flexible?DockStyle.Top:DockStyle.Fill;stack.Controls.Add(c,0,i);
    }
    FlowLayoutPanel Buttons(params Control[] controls) {var f=new ActionRow();f.Controls.AddRange(controls);return f;}
    void Heading(TableLayoutPanel stack,string title,string subtitle) {Add(stack,new PageHeading(title,subtitle),100);}
    void Note(TableLayoutPanel stack,string title,string body,int height=100) {var card=new ReadableCard();card.SetText(title,body);Add(stack,card,height);}
    void Navigate(string name) {
        page=name;mainRecommendation=null;setupGrid=null;setupProgress=null;historyGeneration++;content.SuspendLayout();foreach(Control old in content.Controls.Cast<Control>().ToArray())old.Dispose();content.Controls.Clear();soilMetric=null;adviceList=null;trend=null;contextLabel=null;connectionLabel=null;weatherStamp=null;careSoil=null;careWater=null;careLight=null;careSessionLabel=null;whatsappCard=null;notificationGrid=null;wateringGrid=null;speciesCareLabel=null;telegramCard=null;telegramGrid=null;telegramConnectionLabel=null;
        foreach(Control c in side.Controls) if(c is Button)c.BackColor=(string)c.Tag==name?Color.FromArgb(46,86,63):Theme.Side;
        var stack=Stack(content);
        switch(name) {
            case "Primeiros passos":SetupPage(stack);break;case "Visão geral":Dashboard(stack);break;case "Minha planta":PlantsPage(stack);break;
            case "Cuidados e rega":CarePage(stack);break;
            case "Telegram e avisos":TelegramPage(stack);break;
            case "WhatsApp e avisos":WhatsAppPage(stack);break;
            case "Clima da região":WeatherPage(stack);break;case "Arduino e calibração":ArduinoPage(stack);break;
            case "Histórico":HistoryPage(stack);break;case "Configurações":SettingsPage(stack);break;
        }
        content.ResumeLayout(true);
    }
    void Dashboard(TableLayoutPanel stack) {
        Heading(stack,"Como está sua planta?","Solo, luz e o próximo cuidado — tudo em um só lugar.");
        mainRecommendation=new DecisionCard();Add(stack,mainRecommendation,130);
        AddSensorHealth(stack,true);
        var banner=Theme.Card();banner.BackColor=Color.FromArgb(228,239,224);
        contextLabel=Theme.Label("",15,null,true);contextLabel.Location=new Point(20,14);contextLabel.AutoEllipsis=true;contextLabel.AutoSize=false;contextLabel.Height=30;banner.Controls.Add(contextLabel);
        connectionLabel=Theme.Label("",9,Theme.Muted);connectionLabel.Location=new Point(20,49);connectionLabel.AutoSize=false;connectionLabel.Height=38;banner.Controls.Add(connectionLabel);
        banner.Resize+=(s,e)=>{contextLabel.Width=banner.Width-40;connectionLabel.Width=banner.Width-40;};Add(stack,banner,105);
        var row=new TableLayoutPanel{ColumnCount=2,Dock=DockStyle.Fill,Margin=new Padding(0,6,0,18)};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        soilMetric=new Metric("UMIDADE DO SOLO","Aguardando sensor",Theme.Green);lightMetric=new Metric("LUZ NA PLANTA","Aguardando sensor",Theme.Amber);
        row.Controls.Add(soilMetric,0,0);row.Controls.Add(lightMetric,1,0);Add(stack,row,160);
        Add(stack,Buttons(Theme.Button("Ver cuidados",(s,e)=>Navigate("Cuidados e rega"),true),Theme.Button("Já reguei",(s,e)=>AlreadyWatered()),Theme.Button("Conectar sensores",(s,e)=>Navigate("Arduino e calibração"))),56);
        adviceList=new ActionRow{FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Theme.Bg,Margin=Padding.Empty};
        adviceList.Resize+=(s,e)=>{foreach(Control c in adviceList.Controls)c.Width=Math.Max(100,adviceList.ClientSize.Width-2);};
        Disclosure(stack,"Detalhes de solo, água e luz",details=>Add(details,adviceList,310));adviceSignature="";
        Add(stack,Buttons(Theme.Button("Primeiros passos",(s,e)=>Navigate("Primeiros passos"))),56);
        Disclosure(stack,"Clima da região",details=>{
            var weatherRow=new TableLayoutPanel{ColumnCount=2,Margin=Padding.Empty};weatherRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));weatherRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            tempMetric=new Metric("TEMPERATURA EXTERNA","Clima da região",Theme.Ink);humidityMetric=new Metric("UMIDADE EXTERNA","Clima da região",Theme.Ink);weatherRow.Controls.Add(tempMetric,0,0);weatherRow.Controls.Add(humidityMetric,1,0);Add(details,weatherRow,155);
            weatherStamp=Theme.Label("",9,Theme.Muted);weatherStamp.AutoSize=false;Add(details,weatherStamp,58);
            Add(details,Buttons(Theme.Button("Mudar cidade",(s,e)=>Navigate("Clima da região")),Theme.Button("Atualizar clima",async(s,e)=>{await RefreshWeather();UpdateDashboard();})),56);
            Note(details,"Clima externo","Temperatura e umidade são estimativas da região. Dentro de casa, elas não medem o ar ao redor da planta.");
        });
        UpdateDashboard();
    }
    void UpdateDashboard() {
        if(soilMetric==null||soilMetric.IsDisposed)return;
        var p=Active;var w=store.Settings.Weather;var now=DateTime.UtcNow;
        UpdateSensorHealth();
        if(mainRecommendation!=null)mainRecommendation.Set(DailyRecommendation.Get(p,last,CurrentCareReport(),demo,now));
        contextLabel.Text=demo?"DEMONSTRAÇÃO · leituras simuladas":p==null?"Sua primeira planta começa aqui":p.Name;
        connectionLabel.Text=(demo?"Sensores fictícios · não gravados no histórico":last!=null&&last.Fresh(now)?"Sensores recebendo dados · "+last.Utc.ToLocalTime().ToString("HH:mm:ss"):"Sensores sem leitura atual")+"   |   "+(store.Settings.Location==null?"Guarulhos, SP · aguardando consulta":store.Settings.Location.ToString());
        if(last!=null) {
            var soil=store.Settings.Calibration.Soil(last.Soil);var light=store.Settings.Calibration.Light(last.Light);
            soilMetric.ValueLabel.Text=soil.HasValue?soil.Value.ToString("0")+"%":last.Soil.ToString();
            lightMetric.ValueLabel.Text=light.HasValue?light.Value.ToString("0")+"%":last.Light.ToString();
            string age=last.Fresh(now)?"":"ANTIGA · ";
            soilMetric.SubLabel.Text=age+(soil.HasValue?"Índice relativo · bruto "+last.Soil:"Bruto de 0 a 1023 · calibre");
            lightMetric.SubLabel.Text=age+(light.HasValue?"Índice relativo · bruto "+last.Light:"Bruto de 0 a 1023 · calibre");
        } else {soilMetric.ValueLabel.Text="—";lightMetric.ValueLabel.Text="—";soilMetric.SubLabel.Text="Aguardando sensor";lightMetric.SubLabel.Text="Aguardando sensor";}
        tempMetric.ValueLabel.Text=w!=null&&w.Temperature.HasValue?w.Temperature.Value.ToString("0.0")+"°C":"—";
        humidityMetric.ValueLabel.Text=w!=null&&w.Humidity.HasValue?w.Humidity.Value.ToString("0")+"%":"—";
        string state=w!=null?(w.Fresh(now)?"Clima regional · atual":"DADO ANTIGO · sem comparação"):"Selecione a cidade";
        tempMetric.SubLabel.Text=state;humidityMetric.SubLabel.Text=state;
        weatherStamp.Text=w==null?"Clima: "+(weatherError==""?"aguardando consulta":weatherError):"Open-Meteo · horário do modelo: "+w.ModelTime.Replace("T"," ")+" · "+w.Timezone+"\n"+(weatherError==""?"Atualização automática a cada 15 minutos.":"Falha na atualização: "+weatherError);
        var advice=Assessment.Evaluate(p,last,w,store.Settings.Calibration,now);
        if(p!=null) {
            advice=advice.Where(x=>!x.Title.StartsWith("Luz:")&&!x.Title.StartsWith("É noite")&&!x.Title.StartsWith("Calibre a luminosidade")&&!(CareEngine.Supported(p)&&(x.Title.StartsWith("Solo:")||x.Title.StartsWith("Calibre o sensor de solo")||x.Title=="Espécie + sensores + contexto"))).ToList();
            advice.InsertRange(0,CurrentCareReport().Advice);
        }
        if(p!=null)advice=advice.Take(3).ToList();
        if(demo)advice.Insert(0,new Advice("Modo de demonstração","Os números de solo e luz são fictícios. O clima, quando disponível, continua sendo da API. Saia deste modo para conectar o Arduino."));
        string signature=Json.Write(advice);
        if(signature!=adviceSignature) {
            adviceList.SuspendLayout();foreach(Control child in adviceList.Controls.Cast<Control>().ToArray())child.Dispose();
            foreach(var a in advice) {
                var card=new DecisionCard{Width=Math.Max(100,adviceList.ClientSize.Width-2)};card.Set(a);adviceList.Controls.Add(card);
            }
            adviceSignature=signature;adviceList.ResumeLayout();
        }
    }
    async Task RefreshWeather() {
        if(weatherBusy||store.Settings.Location==null)return;
        weatherBusy=true;int version=locationVersion;var location=store.Settings.Location;Status("Consultando clima de "+location.Name+"…");
        try {var w=await Api.Climate(location);if(version==locationVersion){store.Settings.Weather=w;weatherError="";Save();Status("Clima atualizado · "+DateTime.Now.ToString("HH:mm:ss"));}}
        catch(Exception e){if(version==locationVersion){weatherError=Friendly(e);Status("Clima: atualização indisponível. Último registro preservado.");}}
        finally{weatherBusy=false;nextWeather=DateTime.UtcNow.AddMinutes(15);}
    }
    void PlantsPage(TableLayoutPanel stack) {
        Heading(stack,"Minha planta","Uma espécie, suas orientações e as faixas que você validou para o vaso.");
        var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=470,Font=Theme.F(11)};
        combo.Items.AddRange(store.Settings.Plants.Cast<object>().ToArray());combo.SelectedItem=Active;
        combo.SelectedIndexChanged+=(s,e)=>{var p=combo.SelectedItem as Plant;if(p!=null){store.Settings.ActiveId=p.LocalId;last=null;samples.Clear();Save();Navigate("Minha planta");}};
        Add(stack,Buttons(combo,Theme.Button("Buscar na API",(s,e)=>SearchPlantDialog(),true),Theme.Button("Perfil manual",(s,e)=>EditPlant(new Plant{Name="Minha planta"},true))),52);
        var plant=Active;
        if(plant==null){Note(stack,"Escolha uma espécie","Use a busca da Perenual para importar orientações de rega e luz. Sem chave, você pode criar um perfil manual com uma referência própria.",106);}
        else {
            Note(stack,plant.Name,plant.Scientific+"\nOrigem: "+plant.Source+(plant.ImportedUtc.HasValue?" · importado em "+plant.ImportedUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"):""),105);
            Disclosure(stack,"Adaptação pela API",details=>SpeciesControls(details));
            Note(stack,"Orientações da espécie","Rega: "+OrMissing(plant.Watering)+"\nLuz: "+OrMissing(plant.Sunlight)+"\nSubstrato: "+OrMissing(plant.SoilAdvice),130);
            if(!string.IsNullOrEmpty(plant.Description))Disclosure(stack,"Sobre a espécie",details=>Note(details,"Descrição da API",plant.Description));
            string ranges="Solo (índice relativo): "+RangeText(plant.Soil)+"\nLuz (índice relativo): "+RangeText(plant.Light)+"\nTemperatura externa: "+RangeText(plant.Temperature)+" °C    |    Umidade externa: "+RangeText(plant.Humidity)+" %\nAmbiente: "+(plant.Outdoor?"externo — comparação regional habilitada":"interno — clima apenas como contexto")+"\nReferência dos limites: "+OrMissing(plant.Reference);
            Disclosure(stack,"Faixas e referências deste vaso",details=>Note(details,"Faixas cadastradas",ranges));
            Add(stack,Buttons(Theme.Button("Cuidados e rega",(s,e)=>Navigate("Cuidados e rega"),true),Theme.Button("Editar planta e faixas",(s,e)=>EditPlant(plant,false)),Theme.Button("Calibrar sensores",(s,e)=>Navigate("Arduino e calibração"))),48);
        }
        
    }
    static string OrMissing(string s){return string.IsNullOrWhiteSpace(s)?"Não informado pela fonte":s;}
    static string RangeText(Range r){return r!=null&&r.Enabled?r.Min.Value.ToString("0.#")+" a "+r.Max.Value.ToString("0.#"):"Não definida";}
    Form Dialog(string title,int width,int height) {return new Form{Text=title,Size=new Size(width,height),MinimumSize=new Size(width,height),StartPosition=FormStartPosition.CenterParent,Font=Theme.F(10),BackColor=Theme.Bg,MinimizeBox=false,MaximizeBox=false,AutoScaleMode=AutoScaleMode.Dpi,ShowInTaskbar=false};}
    void SearchPlantDialog() {
        if(store.LoadKey()==""){MessageBox.Show(this,"Cadastre sua chave Perenual em Configurações. Enquanto isso, você pode criar um perfil manual.","Chave necessária");Navigate("Configurações");return;}
        using(var d=Dialog("Buscar espécie · Perenual",730,520)) {
            var q=Theme.Input("",450);q.Location=new Point(22,48);
            var hint=Theme.Label("Prefira o nome científico, por exemplo: Monstera deliciosa.",10,Theme.Muted);hint.Location=new Point(22,18);
            var results=new ListBox{Location=new Point(22,104),Size=new Size(666,255),Font=Theme.F(10),HorizontalScrollbar=true};
            var msg=Theme.Label("Digite o nome para buscar. São exibidos até 30 resultados.",9,Theme.Muted);msg.AutoSize=false;msg.Location=new Point(22,370);msg.Size=new Size(660,40);
            Button search=null;search=Theme.Button("Buscar",async(s,e)=> {
                if(q.Text.Trim().Length<3){msg.Text="Digite pelo menos 3 letras.";return;}
                search.Enabled=false;results.Items.Clear();msg.Text="Buscando espécies…";
                try{var list=await Api.SearchPlants(store.LoadKey(),q.Text.Trim());if(d.IsDisposed)return;results.Items.AddRange(list.Cast<object>().ToArray());msg.Text=list.Count==0?"Nenhuma espécie encontrada. Tente o nome científico ou em inglês.":"Selecione uma espécie para importar seus cuidados.";}
                catch(Exception ex){if(!d.IsDisposed)msg.Text=Friendly(ex);}finally{if(!d.IsDisposed)search.Enabled=true;}
            },true);search.Location=new Point(490,43);
            var use=Theme.Button("Importar espécie",async(s,e)=> {
                var selected=results.SelectedItem as Plant;if(selected==null)return;
                var b=(Button)s;b.Enabled=false;msg.Text="Consultando cuidados da espécie…";
                try {var p=await Api.Details(store.LoadKey(),selected.ApiId);if(d.IsDisposed)return;store.Settings.Plants.Add(p);store.Settings.ActiveId=p.LocalId;last=null;samples.Clear();Save();d.Close();Navigate("Minha planta");}
                catch(Exception ex){if(!d.IsDisposed)msg.Text=Friendly(ex);}finally{if(!d.IsDisposed)b.Enabled=true;}
            },true);use.Location=new Point(22,420);
            d.Controls.AddRange(new Control[]{hint,q,search,results,msg,use});d.AcceptButton=search;d.ShowDialog(this);
        }
    }
    void EditPlant(Plant original,bool isNew) {
        var p=Json.Read<Plant>(Json.Write(original));
        using(var d=Dialog("Planta e faixas de referência",740,690)) {
            var body=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22)};d.Controls.Add(body);
            var form=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3};form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,260));form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.Controls.Add(form);
            int r=0;Action<string,Control> field=(name,c)=>{form.RowStyles.Add(new RowStyle(SizeType.Absolute,43));form.Controls.Add(Theme.Label(name),0,r);form.Controls.Add(c,1,r);form.SetColumnSpan(c,2);r++;};
            var nameInput=Theme.Input(p.Name,350);field("Nome da planta / vaso",nameInput);
            var sci=Theme.Input(p.Scientific,350);field("Nome científico",sci);
            var outdoor=new CheckBox{Text="Externo: comparar com o clima da região",AutoSize=true,Checked=p.Outdoor};field("Ambiente do vaso",outdoor);
            var info=Theme.Label("Faixas são opcionais. Deixe os dois campos vazios para suspender a avaliação.\nSolo e luz usam índices locais calibrados, não porcentagem real de água ou lux.",9,Theme.Muted);info.AutoSize=false;info.Size=new Size(640,52);form.Controls.Add(info,0,r);form.SetColumnSpan(info,3);form.RowStyles.Add(new RowStyle(SizeType.Absolute,66));r++;
            form.Controls.Add(Theme.Label("Medida",10,null,true),0,r);form.Controls.Add(Theme.Label("Mínimo",10,null,true),1,r);form.Controls.Add(Theme.Label("Máximo",10,null,true),2,r);form.RowStyles.Add(new RowStyle(SizeType.Absolute,32));r++;
            var ranges=new[]{p.Soil,p.Light,p.Temperature,p.Humidity};var names=new[]{"Solo · índice relativo (0–100)","Luz · índice relativo (0–100)","Temperatura regional (°C)","Umidade regional (%)"};var boxes=new List<TextBox[]>();
            for(int i=0;i<4;i++) {
                var range=ranges[i]??new Range();var lo=Theme.Input(range.Min.HasValue?range.Min.Value.ToString():"",130);var hi=Theme.Input(range.Max.HasValue?range.Max.Value.ToString():"",130);
                boxes.Add(new[]{lo,hi});form.Controls.Add(Theme.Label(names[i]),0,r);form.Controls.Add(lo,1,r);form.Controls.Add(hi,2,r);form.RowStyles.Add(new RowStyle(SizeType.Absolute,48));r++;
            }
            var reference=Theme.Input(p.Reference,350);field("Fonte dos limites / observação",reference);
            var source=Theme.Label("Os limites são cadastrados por você. A importação da Perenual não preenche\nfaixas numéricas que a API não fornece.",9,Theme.Muted);source.AutoSize=false;source.Size=new Size(640,48);form.Controls.Add(source,0,r);form.SetColumnSpan(source,3);form.RowStyles.Add(new RowStyle(SizeType.Absolute,60));r++;
            var save=Theme.Button("Salvar planta",(s,e)=> {
                try {
                    if(string.IsNullOrWhiteSpace(nameInput.Text))throw new ApiException("Dê um nome à planta.");
                    var parsed=new List<Range>();for(int i=0;i<4;i++)parsed.Add(ParseRange(boxes[i][0].Text,boxes[i][1].Text,i==2?-50:0,i==2?60:100,names[i]));
                    if(parsed.Any(x=>x.Enabled)&&string.IsNullOrWhiteSpace(reference.Text))throw new ApiException("Informe a fonte ou observação que fundamenta os limites.");
                    p.Name=nameInput.Text.Trim();p.Scientific=sci.Text.Trim();p.Outdoor=outdoor.Checked;p.Reference=reference.Text.Trim();
                    p.Soil=parsed[0];p.Light=parsed[1];p.Temperature=parsed[2];p.Humidity=parsed[3];
                    if(isNew)store.Settings.Plants.Add(p);else store.Settings.Plants[store.Settings.Plants.FindIndex(x=>x.LocalId==p.LocalId)]=p;
                    store.Settings.ActiveId=p.LocalId;Save();d.Close();Navigate("Minha planta");
                }catch(Exception ex){ShowError(ex);}
            },true);form.Controls.Add(save,0,r);form.SetColumnSpan(save,2);form.RowStyles.Add(new RowStyle(SizeType.Absolute,50));d.ShowDialog(this);
        }
    }
    public static Range ParseRange(string lo,string hi,double min,double max,string name) {
        if(string.IsNullOrWhiteSpace(lo)&&string.IsNullOrWhiteSpace(hi))return new Range();
        double a,b;
        if(!double.TryParse(lo.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out a)||!double.TryParse(hi.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out b)||double.IsNaN(a)||double.IsNaN(b)||a<min||b>max||a>=b)
            throw new ApiException(name+": informe mínimo e máximo válidos entre "+min+" e "+max+", com mínimo menor que máximo.");
        return new Range(a,b);
    }
    void ArduinoPage(TableLayoutPanel stack) {
        Heading(stack,"Arduino e calibração","Conexão USB · 9600 baud · solo em A1 e luminosidade em A0.");
        Note(stack,"Conectar os sensores","Selecione a porta USB do Arduino e clique em Conectar. Feche o Monitor Serial e o Plotter da Arduino IDE se estiverem usando a mesma porta.");
        var ports=new ComboBox{Width=175,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.F(11)};
        Action reload=()=>{ports.Items.Clear();ports.Items.AddRange(SerialPort.GetPortNames().OrderBy(x=>x).Cast<object>().ToArray());if(ports.Items.Contains(store.Settings.Port))ports.SelectedItem=store.Settings.Port;else if(ports.Items.Count>0)ports.SelectedIndex=0;};reload();
        Add(stack,Buttons(ports,Theme.Button("Atualizar portas",(s,e)=>reload()),Theme.Button("Conectar",(s,e)=> {
            if(demo){MessageBox.Show(this,"Saia da demonstração na Visão geral antes de conectar.");return;}
            if(ports.SelectedItem==null){MessageBox.Show(this,"Nenhuma porta disponível. Conecte o USB e atualize as portas.");return;}
            Connect(ports.SelectedItem.ToString());
        },true),Theme.Button("Desconectar",(s,e)=>{Disconnect();Status("Arduino desconectado.");})),52);
        connectionLabel=Theme.Label("",11,Theme.Muted);Add(stack,connectionLabel,55);UpdateConnection();
        AddSensorHealth(stack);
        var c=store.Settings.Calibration;
        Note(stack,"Referências dos sensores","Solo: seco = "+(c.Dry.HasValue?c.Dry.ToString():"pendente")+" · úmido = "+(c.Wet.HasValue?c.Wet.ToString():"pendente")+"\nLuz: escuro = "+(c.Dark.HasValue?c.Dark.ToString():"pendente")+" · claro = "+(c.Bright.HasValue?c.Bright.ToString():"pendente")+"\nA calibração pertence a esta montagem. Repita ao mudar sensor ou substrato.",130);
        Add(stack,Buttons(Theme.Button("Calibrar solo e luz",(s,e)=>CalibrationDialog(),true),Theme.Button("Abrir pasta do programa Arduino",(s,e)=>OpenFolder(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Arduino")))),48);
        Disclosure(stack,"Ajuda com sensores e conexão",help=>{
        Note(help,"Como preparar o sensor de solo","Use o mesmo substrato do vaso: registre uma referência seca e outra úmida após regar e deixar drenar. Enterre apenas a parte sensora; mantenha a eletrônica e o conector secos. O app aceita sensores cujo valor sobe ou desce com a umidade.",120);
        Note(help,"Se a porta estiver ocupada","Feche o Monitor Serial e o Serial Plotter da Arduino IDE antes de conectar o app. O sketch anterior também é aceito para solo e luz; as leituras do DHT são ignoradas. Para retirar a dependência do DHT, envie o novo sketch incluído.",120);
        });
    }
    void UpdateConnection() {
        if(connectionLabel==null||connectionLabel.IsDisposed)return;
        connectionLabel.Text=(demo?"DEMONSTRAÇÃO · Arduino desconectado":serial!=null&&serial.IsOpen?"Conectado em "+serial.PortName:"Arduino desconectado")+(last==null?" · aguardando leituras":"\nÚltima leitura"+(demo?" SIMULADA":"")+": solo "+last.Soil+" · luz "+last.Light+" · "+last.Utc.ToLocalTime().ToString("HH:mm:ss")+(last.Fresh(DateTime.UtcNow)?"":" · ANTIGA"));
    }
    void Connect(string portName) {
        Disconnect();last=null;samples.Clear();invalidSensorUtc=null;
        try {
            var port=new SerialPort(portName,9600){ReadTimeout=1000,DtrEnable=true,Encoding=Encoding.ASCII};port.Open();serial=port;
            store.Settings.Port=portName;Save();int generation=serialGeneration;Status("Arduino conectado. Aguardando reinicialização e leituras…");
            Task.Run(()=> {
                var parser=new SensorParser();
                while(generation==serialGeneration) {
                    try {string line=port.ReadLine();var now=DateTime.UtcNow;var sample=parser.Parse(line,now);Ui(()=>{if(generation==serialGeneration){ObserveSensorLine(line,sample,now);if(sample!=null)Accept(sample);}});}
                    catch(TimeoutException){}catch {if(generation==serialGeneration)Ui(()=>{if(generation==serialGeneration){Disconnect();Status("Conexão USB interrompida. Confira o cabo e conecte novamente.");}});break;}
                }
            });UpdateConnection();
        }catch {Disconnect();MessageBox.Show(this,"Não foi possível abrir "+portName+". Feche o Monitor Serial da IDE e confira o cabo USB.","Conexão com Arduino");}
    }
    void Ui(Action a){try{if(!IsDisposed&&IsHandleCreated)BeginInvoke(a);}catch(InvalidOperationException){}}
    void Disconnect(){serialGeneration++;var p=serial;serial=null;if(p!=null){try{p.Close();p.Dispose();}catch{}}}
    void Accept(Sample sample) {
        var weather=store.Settings.Weather;sample.Daylight=weather!=null&&weather.Fresh(sample.Utc)?weather.IsDay:null;
        last=sample;samples.Add(sample);if(samples.Count>10000)samples.RemoveAt(0);
        if(!demo)try{store.Append(sample,Active,store.Settings.Weather,store.Settings.Location,store.Settings.Calibration);}catch{Status("Leitura recebida, mas não foi possível salvar o histórico. Confira a pasta Dados.");}
        if(page=="Visão geral")UpdateDashboard();if(page=="Arduino e calibração"){UpdateConnection();UpdateSensorHealth();}if(page=="Cuidados e rega")UpdateCareCards();if(trend!=null&&!trend.IsDisposed){trend.Samples=samples;trend.Invalidate();}UpdatePlotters();
    }
    void CalibrationDialog() {
        if(demo){MessageBox.Show(this,"Saia da demonstração para calibrar com leituras reais.");return;}
        using(var d=Dialog("Calibrar sensores",710,520)) {
            var explanation=Theme.Label("Registre valores brutos entre 0 e 1023. Mantenha o sensor conectado e\nuse o botão ao lado de cada referência quando a leitura estiver estável.\nNão mergulhe a eletrônica do sensor de solo.",10,Theme.Muted);explanation.Location=new Point(22,20);d.Controls.Add(explanation);
            var c=store.Settings.Calibration;var values=new[]{c.Dry,c.Wet,c.Dark,c.Bright};var names=new[]{"Substrato seco","Substrato úmido drenado","LDR no escuro","LDR no claro"};var boxes=new List<TextBox>();
            for(int i=0;i<4;i++) {
                int index=i;var label=Theme.Label(names[i]);label.Location=new Point(22,122+i*58);d.Controls.Add(label);
                var box=Theme.Input(values[i].HasValue?values[i].Value.ToString():"",110);box.Location=new Point(238,118+i*58);boxes.Add(box);d.Controls.Add(box);
                var capture=Theme.Button("Usar leitura atual",(s,e)=> {
                    if(last==null||!last.Fresh(DateTime.UtcNow)){MessageBox.Show(d,"Conecte o Arduino e aguarde uma leitura atual.");return;}
                    box.Text=(index<2?last.Soil:last.Light).ToString();
                });capture.Location=new Point(375,113+i*58);d.Controls.Add(capture);
            }
            var note=Theme.Label("Cada par precisa ter pelo menos 30 pontos de diferença.\nPara deixar um sensor sem calibração, apague as duas referências dele.",9,Theme.Muted);note.Location=new Point(22,358);d.Controls.Add(note);
            var save=Theme.Button("Salvar calibração",(s,e)=> {
                try {
                    var parsed=boxes.Select(b=>ParseAdc(b.Text)).ToArray();
                    for(int i=0;i<4;i+=2){if(parsed[i].HasValue!=parsed[i+1].HasValue)throw new ApiException("Preencha as duas referências de cada sensor ou deixe ambas vazias.");if(parsed[i].HasValue&&Math.Abs(parsed[i].Value-parsed[i+1].Value)<30)throw new ApiException("As referências estão próximas demais. Use situações mais distintas e aguarde estabilizar.");}
                    store.Settings.CalibrationChanges.Add(DateTime.UtcNow);store.Settings.Calibration=new Calibration{Dry=parsed[0],Wet=parsed[1],Dark=parsed[2],Bright=parsed[3]};last=null;samples.Clear();Save();d.Close();Navigate("Arduino e calibração");
                }catch(Exception ex){ShowError(ex);}
            },true);save.Location=new Point(22,411);d.Controls.Add(save);d.ShowDialog(this);
        }
    }
    static int? ParseAdc(string s){int n;if(string.IsNullOrWhiteSpace(s))return null;if(!int.TryParse(s,out n)||n<0||n>1023)throw new ApiException("Use valores inteiros entre 0 e 1023.");return n;}
    void ExportHistory() {
        if(!File.Exists(store.HistoryPath)){MessageBox.Show(this,"Ainda não há leituras reais salvas. Conecte o Arduino para iniciar o histórico.");return;}
        using(var dialog=new SaveFileDialog{Filter="Planilha CSV|*.csv",FileName="Raiz_historico_"+DateTime.Now.ToString("yyyy-MM-dd")+".csv"}) {
            if(dialog.ShowDialog(this)!=DialogResult.OK||!ExportPathAllowed(dialog.FileName))return;
            try{if(Path.GetFullPath(dialog.FileName)==Path.GetFullPath(store.HistoryPath))return;File.Copy(store.HistoryPath,dialog.FileName,true);Status("Histórico exportado.");}catch(Exception e){ShowError(e);}
        }
    }
    void SettingsPage(TableLayoutPanel stack) {
        Heading(stack,"Configurações","Conecte a fonte de plantas e entenda onde seus dados ficam.");
        Add(stack,Buttons(Theme.Button("Primeiros passos",(s,e)=>Navigate("Primeiros passos"))),56);
        var background=new CheckBox{Text="Ao fechar a janela, continuar perto do relógio do Windows",AutoSize=true,Checked=store.Settings.CloseToTray};background.CheckedChanged+=(s,e)=>{store.Settings.CloseToTray=background.Checked;store.Settings.BackgroundChoiceMade=true;Save();};Add(stack,Buttons(background),52);
        Add(stack,Buttons(Theme.Button("Configurar avisos no celular",(s,e)=>Navigate("Telegram e avisos"),true)),50);
        AddTransferSettings(stack);
        Disclosure(stack,"Chave da API de plantas",apiSetup=>{
        Note(apiSetup,"Perenual · API de plantas","Você ainda pode usar clima e sensores sem uma chave. Para buscar espécies e importar seus cuidados, crie sua própria chave na Perenual. A disponibilidade das espécies e dos detalhes depende do plano da sua conta.",118);
        Add(apiSetup,Buttons(Theme.Button("Abrir cadastro da Perenual",(s,e)=>OpenUrl("https://perenual.com/docs/api"),true)),48);
        var key=Theme.Input("",440);key.UseSystemPasswordChar=true;key.AccessibleName="Chave da API Perenual";
        var status=Theme.Label(store.LoadKey()==""?"Nenhuma chave cadastrada.":"Chave protegida já cadastrada. Digite somente se quiser substituí-la.",9,Theme.Muted);Add(apiSetup,status,35);
        Add(apiSetup,Buttons(key,Theme.Button("Guardar chave",(s,e)=> {
            if(key.Text.Trim().Length<8){MessageBox.Show(this,"Cole a chave completa obtida na sua conta Perenual.");return;}
            try{store.SaveKey(key.Text);key.Clear();status.Text="Chave protegida cadastrada. Use Minha planta para testar a busca.";Status("Chave guardada com proteção do usuário Windows.");}catch{MessageBox.Show(this,"Não foi possível proteger a chave neste usuário Windows.");}
        },true)),55);
        },store.LoadKey()=="");
        Add(stack,Buttons(Theme.Button("Modo demonstração",(s,e)=>ToggleDemo()),Theme.Button("Localização do clima",(s,e)=>Navigate("Clima da região"))),56);
        Disclosure(stack,"Fontes, privacidade e manutenção",details=>{
        Note(details,"Chave protegida no computador","A chave fica criptografada pelo Windows para este usuário. Ela é enviada apenas à Perenual nas consultas de plantas e não é incluída no histórico. Não envie a chave pelo chat nem distribua a pasta Dados.",110);
        Note(details,"Fontes e limites","Perenual: espécies, orientações de rega e luz. Open-Meteo: temperatura e umidade externas, com atribuição CC BY 4.0. Geocodificação: Open-Meteo / GeoNames. As consultas públicas de clima destinam-se ao uso não comercial conforme os termos do serviço.",126);
        Add(details,Buttons(Theme.Button("Documentação das plantas",(s,e)=>OpenUrl("https://perenual.com/docs/api")),Theme.Button("Documentação do clima",(s,e)=>OpenUrl("https://open-meteo.com/en/docs")),Theme.Button("Licença CC BY 4.0",(s,e)=>OpenUrl("https://creativecommons.org/licenses/by/4.0/"))),50);
        Note(details,"Dados e manutenção","Use a transferência protegida acima para levar seus dados e conexões a outro computador. Preserve o arquivo e a senha separadamente. A pasta Dados contém registros privados; os arquivos protegidos pelo Windows só abrem no usuário de origem. A rega continua manual.",116);
        });
    }
    static void OpenUrl(string url){try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}catch{MessageBox.Show("Não foi possível abrir o navegador.");}}
    static void OpenFolder(string path){try{if(Directory.Exists(path))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});else MessageBox.Show("Pasta não encontrada. Extraia o pacote completo do app.");}catch{MessageBox.Show("Não foi possível abrir a pasta.");}}
    void ToggleDemo(){Disconnect();demo=!demo;last=null;samples.Clear();Status(demo?"Demonstração ativa. Solo e luz são fictícios; nenhum dado simulado será salvo.":"Demonstração encerrada. Conecte os sensores para leituras reais.");Navigate("Visão geral");}
    async void OnTick() {
        tick++;
        if(demo&&tick%3==0) {
            var cal=store.Settings.Calibration;
            int soil=cal.Dry.HasValue&&cal.Wet.HasValue?(int)(cal.Dry.Value+(cal.Wet.Value-cal.Dry.Value)*(0.5+0.45*Math.Sin(tick/15.0))):(int)(510+80*Math.Sin(tick/15.0));
            int light=cal.Dark.HasValue&&cal.Bright.HasValue?(int)(cal.Dark.Value+(cal.Bright.Value-cal.Dark.Value)*(0.5+0.45*Math.Sin(tick/11.0))):(int)(460+160*Math.Sin(tick/11.0));
            Accept(new Sample{Soil=soil,Light=light,Utc=DateTime.UtcNow});
        }
        if(page=="Visão geral")UpdateDashboard();if(page=="Arduino e calibração"){UpdateConnection();UpdateSensorHealth();}if(page=="Cuidados e rega")UpdateCareCards();if(page=="WhatsApp e avisos")UpdateWhatsAppPage();UpdatePlotters();
        if(page=="Telegram e avisos")UpdateTelegramPage();if(page=="Primeiros passos")UpdateSetup();if(page=="Histórico"&&tick%60==0)LoadDailyHistory();
        await RefreshSpecies();await PollTelegram();await PollWhatsApp();
        if(DateTime.UtcNow>=nextWeather&&!weatherBusy&&store.Settings.Location!=null)await RefreshWeather();
    }
}
public static class Program {
    [STAThread] public static int Main(string[] args) {
        if(args.Contains("--api-check"))return Checks.Live();
        CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("pt-BR");
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try {
            string folder=AppPaths.DataFolder(AppDomain.CurrentDomain.BaseDirectory,Environment.GetEnvironmentVariable("RAIZ_DATA"));
            if(AppPaths.PreviousInstanceRunning(folder,Application.ExecutablePath)){MessageBox.Show("Feche a versão anterior do Raiz e abra esta versão novamente. Seus dados serão preservados.","Raiz já está aberto");return 0;}
            bool first;using(var mutex=new System.Threading.Mutex(true,AppPaths.MutexName(folder),out first)) {
                if(!first){MessageBox.Show("O Raiz já está usando esta pasta de dados. Continue na janela que já está aberta.","Raiz já está aberto");return 0;}
                Application.Run(new MainForm(folder));return 0;
            }
        }catch(ApiException e){MessageBox.Show(e.Message,"Raiz");return 1;}
        catch{MessageBox.Show("O Raiz não conseguiu iniciar. Extraia o pacote em uma pasta onde você possa salvar arquivos.","Raiz");return 1;}
    }
}
public static class Checks {
    public static int Live() {
        try{var cities=Api.Cities("Guarulhos").GetAwaiter().GetResult();var l=cities.Find(x=>x.Country=="Brasil"&&x.Region=="São Paulo");if(l==null)throw new Exception("Cidade não encontrada");var w=Api.Climate(l).GetAwaiter().GetResult();Console.WriteLine(Json.Write(new{city=l,weather=w}));return 0;}
        catch(Exception ex){Console.WriteLine(ex is ApiException?ex.Message:"Falha ao validar a API de clima.");return 1;}
    }
}
}
