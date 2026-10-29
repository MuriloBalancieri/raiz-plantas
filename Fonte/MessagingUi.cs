using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace Raiz {
public partial class MainForm {
    WebhookServer webhook;readonly WhatsAppTransport whatsappTransport=new WhatsAppTransport();
    WhatsAppSecrets whatsappSecrets=new WhatsAppSecrets();volatile string receiverFingerprint="";string receiverStatus="Receptor desativado";
    bool whatsappBusy;volatile bool closing;DecisionCard whatsappCard;DataGridView notificationGrid,wateringGrid;string notificationSignature="";
    void RestartWhatsAppReceiver() {
        StopWhatsAppReceiver();whatsappSecrets=store.LoadWhatsAppSecrets();
        if(!store.Settings.WhatsApp.ReceiverEnabled){receiverStatus="Receptor desativado";return;}
        try {
            WhatsAppRules.ValidateReceiver(store.Settings.WhatsApp,whatsappSecrets);
            receiverFingerprint=WhatsAppRules.Fingerprint(store.Settings.WhatsApp,whatsappSecrets);
            string fingerprint=receiverFingerprint;var secret=whatsappSecrets;
            webhook=new WebhookServer(store.Settings.WhatsApp.LocalPort,r=>RouteWebhook(r,secret,fingerprint));webhook.Start();
            receiverStatus="Receptor local ativo em 127.0.0.1:"+webhook.Port+"/whatsapp";
        }catch(ApiException e){receiverStatus=e.Message;webhook=null;receiverFingerprint="";}
        catch{receiverStatus="Não foi possível abrir a porta local. Confira se outro app a está usando.";webhook=null;receiverFingerprint="";}
    }
    void StopWhatsAppReceiver(){receiverFingerprint="";if(webhook!=null){webhook.Dispose();webhook=null;}}
    Task<WebhookReply> PostWebhookUi(Func<WebhookReply> action,string fingerprint) {
        var completion=new TaskCompletionSource<WebhookReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(closing||IsDisposed||!IsHandleCreated){completion.SetResult(new WebhookReply(503,"Offline"));return completion.Task;}
        try {BeginInvoke((Action)(()=> {
            if(closing||receiverFingerprint!=fingerprint){completion.TrySetResult(new WebhookReply(503,"Offline"));return;}
            try{completion.TrySetResult(action());}catch{completion.TrySetResult(new WebhookReply(503,"Save unavailable"));}
        }));}catch{completion.TrySetResult(new WebhookReply(503,"Offline"));}
        return completion.Task;
    }
    Task<WebhookReply> RouteWebhook(WebhookRequest request,WhatsAppSecrets secret,string fingerprint) {
        if(closing||fingerprint!=receiverFingerprint)return Task.FromResult(new WebhookReply(503,"Offline"));
        string path=request.Target.Split('?')[0];if(path!="/whatsapp")return Task.FromResult(new WebhookReply(404,"Not found"));
        if(request.Method=="GET") {
            string challenge=WhatsAppRules.Challenge(request.Target,secret.VerifyToken);if(challenge==null)return Task.FromResult(new WebhookReply(403,"Forbidden"));
            return PostWebhookUi(()=>{store.Settings.WhatsApp.VerifiedUtc=DateTime.UtcNow;store.Settings.WhatsApp.VerifiedConfig=fingerprint;store.Save();Status("Webhook do WhatsApp validado. Selecione messages nas inscrições da Meta.");UpdateWhatsAppPage();return new WebhookReply(200,challenge);},fingerprint);
        }
        string signature;request.Headers.TryGetValue("X-Hub-Signature-256",out signature);
        if(!WhatsAppRules.VerifySignature(request.Body,signature,secret.AppSecret))return Task.FromResult(new WebhookReply(403,"Forbidden"));
        System.Collections.Generic.List<InboundEvent> events;
        try{events=WhatsAppRules.ParseWebhook(request.Body,signature,secret);}catch{return Task.FromResult(new WebhookReply(400,"Invalid payload"));}
        return PostWebhookUi(()=> {
            int added=NotificationEngine.Apply(store.Settings,events,last,store.Settings.Calibration,DateTime.UtcNow);
            store.Save();if(added>0){Status("Já reguei recebido pelo WhatsApp. Registro salvo no histórico de regas.");UpdateCareCards();RefreshWateringGrid();}
            UpdateWhatsAppPage();return new WebhookReply(200,"OK");
        },fingerprint);
    }
    bool WhatsAppReady() {
        try{WhatsAppRules.ValidateSend(store.Settings.WhatsApp,whatsappSecrets);}catch{return false;}
        return webhook!=null&&store.Settings.WhatsApp.ReceiverEnabled&&store.Settings.WhatsApp.VerifiedUtc.HasValue&&store.Settings.WhatsApp.VerifiedConfig==WhatsAppRules.Fingerprint(store.Settings.WhatsApp,whatsappSecrets);
    }
    async Task PollWhatsApp() {
        if(closing||demo||Active==null)return;
        if(NotificationEngine.ResolveIfMoist(store.Settings,Active,store.Settings.Calibration,samples,DateTime.UtcNow))Save();
        if(whatsappBusy||!store.Settings.WhatsApp.Enabled||!WhatsAppReady())return;
        var report=CurrentCareReport();if(!report.CanStartWatering)return;
        var e=NotificationEngine.Active(store.Settings,Active.LocalId);
        if(e==null){e=NotificationEngine.Prepare(store.Settings,Active,whatsappSecrets,DateTime.UtcNow);try{store.Save();}catch{Status("Não foi possível salvar o alerta. Envio não iniciado.");return;}}
        if(!NotificationEngine.CanAttempt(e,DateTime.UtcNow))return;
        await DispatchWhatsApp(e,report);
    }
    async Task DispatchWhatsApp(NotificationEvent e,CareReport report) {
        whatsappBusy=true;
        try {
            e.Attempts++;e.LastAttemptUtc=DateTime.UtcNow;e.State="sending";e.Detail="Envio em andamento.";store.Save();UpdateWhatsAppPage();
            double? index=last==null?null:store.Settings.Calibration.Soil(last.Soil);
            var result=await whatsappTransport.Send(store.Settings.WhatsApp,whatsappSecrets,e,index,report==null?null:report.EstimatedMl);
            if(closing)return;
            NotificationEngine.AcceptAliases(e,result);
            if(!string.IsNullOrEmpty(result.MessageId))e.MetaMessageId=result.MessageId;
            if(!e.ConfirmedUtc.HasValue&&!e.ClosedUtc.HasValue){e.State=result.State;e.Detail=result.Detail;e.RetryAllowed=result.RetryAllowed;}
            store.Save();UpdateWhatsAppPage();Status(e.IsTest?"Teste de WhatsApp: "+e.Detail:"Alerta de WhatsApp: "+e.Detail);
        }catch(ApiException ex){e.State="failed";e.Detail=ex.Message;e.RetryAllowed=false;Save();Status(ex.Message);}
        catch{e.State="uncertain";e.Detail="Falha ao salvar ou concluir o envio. Confira o celular antes de repetir.";Save();}
        finally{whatsappBusy=false;}
    }
    void WhatsAppPage(TableLayoutPanel stack) {
        Heading(stack,"WhatsApp e avisos","Um aviso por ciclo de secagem, com o botão Já reguei na mensagem do celular.");
        whatsappCard=new DecisionCard();Add(stack,whatsappCard,145);
        Add(stack,Buttons(Theme.Button("Configurar WhatsApp",(s,e)=>WhatsAppConfigDialog(),true),Theme.Button("Reiniciar receptor",(s,e)=>{RestartWhatsAppReceiver();UpdateWhatsAppPage();}),Theme.Button("Pausar envios",(s,e)=>{store.Settings.WhatsApp.Enabled=false;Save();UpdateWhatsAppPage();})),50);
        Add(stack,Buttons(Theme.Button("Enviar mensagem de teste",async(s,e)=> {
            if(demo){MessageBox.Show(this,"Saia da demonstração para testar mensagens reais.");return;}
            if(Active==null||whatsappBusy){MessageBox.Show(this,"Escolha a planta e aguarde qualquer envio em andamento.");return;}
            if(!WhatsAppReady()){MessageBox.Show(this,"Conclua a configuração da Meta e valide o webhook HTTPS antes de enviar o teste.");return;}
            var test=NotificationEngine.Prepare(store.Settings,Active,whatsappSecrets,DateTime.UtcNow,true);await DispatchWhatsApp(test,null);
        })),50);
        Note(stack,"O botão está no WhatsApp","O modelo aprovado na Meta precisa ter o botão de resposta rápida Já reguei. O clique retorna ao receptor configurado, identifica a planta e registra a confirmação. A mesma confirmação recebida mais de uma vez não duplica a rega. Sem quantidade informada, o log mostra volume não informado e não ensina uma dose ao app.",142);
        notificationGrid=Grid(new[]{"Quando","Planta","Estado","Detalhe"});Add(stack,notificationGrid,245);
        Note(stack,"Para o clique chegar ao computador","O app precisa permanecer aberto, conectado à internet e com um endereço HTTPS público encaminhando apenas /whatsapp para o receptor local. O receptor escuta somente neste computador. O pacote inclui o guia de configuração; não foi publicado um endereço público automaticamente.",132);
        Add(stack,Buttons(Theme.Button("Abrir guia do WhatsApp",(s,e)=>OpenWhatsAppGuide()),Theme.Button("Painel Meta",(s,e)=>OpenUrl("https://developers.facebook.com/apps/"))),48);
        notificationSignature="";UpdateWhatsAppPage();
    }
    static DataGridView Grid(string[] columns) {
        var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,Font=Theme.F(9)};
        foreach(string name in columns){int i=grid.Columns.Add(name,name);grid.Columns[i].SortMode=DataGridViewColumnSortMode.NotSortable;}return grid;
    }
    void UpdateWhatsAppPage() {
        if(whatsappCard==null||whatsappCard.IsDisposed)return;
        var o=store.Settings.WhatsApp;
        string title=!o.Enabled?"Envios automáticos desativados":WhatsAppReady()?"WhatsApp pronto para avisos de rega":"Configuração do WhatsApp pendente";
        whatsappCard.Set(new Advice(title,receiverStatus+". "+(WhatsAppReady()?"Webhook validado. Um envio aceito pela Meta ainda não comprova entrega; o estado abaixo é atualizado pelos retornos recebidos.":"Configure credenciais, modelo aprovado e endereço HTTPS. O clique só será registrado quando chegar ao receptor do app."),o.Enabled&&WhatsAppReady()?"ok":"pending"));
        if(notificationGrid==null||notificationGrid.IsDisposed)return;
        var list=store.Settings.Notifications.Where(n=>NotificationEngine.Channel(n)=="WhatsApp").OrderByDescending(x=>x.CreatedUtc).Take(50).ToList();string signature=Json.Write(list);if(signature==notificationSignature)return;notificationSignature=signature;
        notificationGrid.Rows.Clear();foreach(var n in list)notificationGrid.Rows.Add(n.CreatedUtc.ToLocalTime().ToString("dd/MM HH:mm:ss"),(n.IsTest?"[TESTE] ":"")+n.PlantName,StateText(n.State),n.Detail);
    }
    static string StateText(string state){switch(state){case "superseded":return "Substituído";case "prepared":return "Preparado";case "sending":return "Enviando";case "accepted":return "Aceito pela Meta";case "sent":return "Enviado";case "delivered":return "Entregue";case "read":return "Lido";case "confirmed":return "Já reguei confirmado";case "resolved":return "Solo voltou a ficar úmido";case "uncertain":return "Resultado indefinido";case "failed":return "Falha";default:return state;}}
    void WhatsAppConfigDialog() {
        var current=store.Settings.WhatsApp;var o=Json.Read<WhatsAppOptions>(Json.Write(current));var secret=store.LoadWhatsAppSecrets();
        using(var d=Dialog("Configuração oficial do WhatsApp",790,760)) {
            var stack=Stack(d);Heading(stack,"Conectar o WhatsApp","Preencha a partir do seu painel Meta. Não envie tokens pelo chat.");
            var form=new TableLayoutPanel{ColumnCount=2,AutoSize=true,Dock=DockStyle.Top,Padding=new Padding(16,0,16,0)};form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,235));form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));int row=0;
            Action<string,Control> field=(name,control)=>{form.RowStyles.Add(new RowStyle(SizeType.Absolute,42));form.Controls.Add(Theme.Label(name,9),0,row);form.Controls.Add(control,1,row);row++;};
            var phone=Theme.Input(secret.Recipient,420);field("Seu celular (país + DDD + número)",phone);
            var sender=Theme.Input(secret.PhoneNumberId,420);field("Phone Number ID do remetente",sender);
            var access=Theme.Input(secret.AccessToken,420);access.UseSystemPasswordChar=true;field("Token de acesso da Meta",access);
            var appSecret=Theme.Input(secret.AppSecret,420);appSecret.UseSystemPasswordChar=true;field("App Secret da Meta",appSecret);
            var verify=Theme.Input(secret.VerifyToken,420);verify.UseSystemPasswordChar=true;field("Token de verificação do webhook",verify);
            var tokenTools=Buttons(Theme.Button("Gerar token",(s,e)=>verify.Text=WhatsAppRules.NewVerifyToken()),Theme.Button("Copiar token",(s,e)=>{if(verify.Text!="")Clipboard.SetText(verify.Text);}));field("Para cadastrar o webhook na Meta",tokenTools);
            var version=Theme.Input(o.ApiVersion,150);field("Versão Graph API (do painel Meta)",version);
            var template=Theme.Input(o.TemplateName,420);field("Nome do modelo aprovado",template);
            var language=Theme.Input(o.Language,150);field("Idioma do modelo",language);
            var url=Theme.Input(o.PublicWebhookUrl,420);field("URL HTTPS pública /whatsapp",url);
            var port=new NumericUpDown{Minimum=1024,Maximum=65535,Value=Math.Max(1024,Math.Min(65535,o.LocalPort)),Width=150};field("Porta local do receptor",port);
            form.Height=row*42+10;Add(stack,form,row*42+10);
            var receive=new CheckBox{Text="Ativar receptor local para receber o botão Já reguei",Checked=o.ReceiverEnabled,AutoSize=true};Add(stack,receive,34);
            var enabled=new CheckBox{Text="Quero receber avisos de rega neste celular (ativar envios)",Checked=o.Enabled,AutoSize=true};Add(stack,enabled,34);
            Add(stack,Buttons(Theme.Button("Salvar configuração",(s,e)=> {
                try {
                    var changed=new WhatsAppSecrets{Recipient=phone.Text.Trim(),PhoneNumberId=sender.Text.Trim(),AccessToken=access.Text.Trim(),AppSecret=appSecret.Text.Trim(),VerifyToken=verify.Text.Trim()};
                    o.ReceiverEnabled=receive.Checked;o.Enabled=enabled.Checked;o.ApiVersion=version.Text.Trim();o.TemplateName=template.Text.Trim();o.Language=language.Text.Trim();o.PublicWebhookUrl=url.Text.Trim();o.LocalPort=(int)port.Value;
                    if(o.ReceiverEnabled||o.Enabled){changed.Recipient=WhatsAppRules.Phone(changed.Recipient);WhatsAppRules.ValidateReceiver(o,changed);}
                    if(o.Enabled){if(!o.ReceiverEnabled)throw new ApiException("Ative também o receptor para o botão do celular funcionar.");WhatsAppRules.ValidateSend(o,changed);}
                    if(WhatsAppRules.Fingerprint(o,changed)!=current.VerifiedConfig){o.VerifiedUtc=null;o.VerifiedConfig=null;}else{o.VerifiedUtc=current.VerifiedUtc;o.VerifiedConfig=current.VerifiedConfig;}
                    store.SaveWhatsAppSecrets(changed);store.Settings.WhatsApp=o;NotificationEngine.ResetFailed(store.Settings,DateTime.UtcNow);store.Save();RestartWhatsAppReceiver();d.Close();Navigate("WhatsApp e avisos");
                }catch(Exception ex){ShowError(ex);}
            },true)),50);
            Note(stack,"Cadastro necessário na Meta","O modelo raiz_rega deve conter três variáveis no corpo e um botão de resposta rápida Já reguei. O endereço público precisa ser HTTPS e encaminhar /whatsapp para 127.0.0.1 na porta configurada. Depois, valide o webhook na Meta e assine messages. Veja o guia incluído para o corpo exato do modelo.",142);
            d.ShowDialog(this);
        }
    }
    void AlreadyWatered() {
        if(!RealCareAllowed())return;
        if(MessageBox.Show(this,"Registrar que você já regou este vaso?\n\nSem uma quantidade previamente registrada, o volume ficará como não informado. Essa confirmação não ensina uma dose de água ao app.","Já reguei",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        try{CareEngine.ConfirmAlreadyWatered(Active,last,store.Settings.Calibration,"App","app:"+Guid.NewGuid().ToString("N"),DateTime.UtcNow,DateTime.UtcNow);WateringSaved();Status("Rega confirmada no app e salva nos logs.");}catch(Exception e){ShowError(e);}
    }
    void OpenWhatsAppGuide() {
        var path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"WHATSAPP.md");
        if(!System.IO.File.Exists(path)){MessageBox.Show(this,"Extraia o pacote completo para acessar o guia WHATSAPP.md.");return;}
        using(var d=Dialog("Guia de configuração do WhatsApp",850,690)) {
            var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Font=Theme.F(10),Text=System.IO.File.ReadAllText(path,System.Text.Encoding.UTF8),BackColor=Color.White,BorderStyle=BorderStyle.None};d.Controls.Add(text);d.ShowDialog(this);
        }
    }
    void WateringSaved(){NotificationEngine.AcknowledgeLocal(store.Settings,Active,DateTime.UtcNow);store.Save();UpdateCareCards();RefreshWateringGrid();UpdateWhatsAppPage();UpdateTelegramPage();}
    void AddWateringLog(TableLayoutPanel stack) {
        Add(stack,Theme.Label("Logs de rega",15,null,true),35);wateringGrid=Grid(new[]{"Quando","Planta","Origem","Quantidade","Drenagem"});Add(stack,wateringGrid,260);RefreshWateringGrid();
        Add(stack,Buttons(Theme.Button("Exportar logs de rega CSV",(s,e)=>ExportWateringLog(),true),Theme.Button("Já reguei",(s,e)=>AlreadyWatered())),50);
    }
    void RefreshWateringGrid() {
        if(wateringGrid==null||wateringGrid.IsDisposed)return;wateringGrid.Rows.Clear();
        var rows=store.Settings.Plants.SelectMany(p=>p.Care.Waterings.Select(r=>new{Plant=p.Name,Record=r})).OrderByDescending(x=>x.Record.FinishedUtc).Take(300);
        foreach(var x in rows){var r=x.Record;wateringGrid.Rows.Add(r.FinishedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),r.PlantName??x.Plant,r.Origin??"Guiada",r.VolumeUnknown?"Não informada":r.TotalMl+" mL",r.DrainageConfirmed?"Confirmada":"Não informada");}
    }
    void ExportWateringLog() {
        try{store.SaveWateringLog();}catch(Exception e){ShowError(e);return;}
        using(var d=new SaveFileDialog{Filter="Planilha CSV|*.csv",FileName="Raiz_regas_"+DateTime.Now.ToString("yyyy-MM-dd")+".csv"})if(d.ShowDialog(this)==DialogResult.OK)try{if(System.IO.Path.GetFullPath(d.FileName)!=System.IO.Path.GetFullPath(store.WateringLogPath))System.IO.File.Copy(store.WateringLogPath,d.FileName,true);Status("Logs de rega exportados.");}catch(Exception e){ShowError(e);}
    }
}
}
