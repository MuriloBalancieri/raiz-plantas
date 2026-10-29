using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Raiz {
public partial class MainForm {
    readonly TelegramTransport telegramTransport=new TelegramTransport();TelegramSecrets telegramSecrets=new TelegramSecrets();
    bool telegramReading,telegramSending,telegramConfiguring,telegramReadPaused;int telegramGeneration;
    DateTime telegramNextRead=DateTime.MinValue;string telegramStatus="Cadastre um bot exclusivo do Raiz.";
    DecisionCard telegramCard;DataGridView telegramGrid;string telegramGridSignature="";
    readonly TelegramConnectionState telegramConnection=new TelegramConnectionState();Label telegramConnectionLabel;
    void LoadTelegram(){telegramSecrets=store.LoadTelegramSecrets();telegramGeneration++;telegramReadPaused=false;telegramNextRead=DateTime.MinValue;telegramConnection.Reset();telegramStatus=telegramSecrets.Paired?"Seu celular está vinculado a este bot.":"Conclua o vínculo para receber os avisos.";}
    async Task PollTelegram() {
        if(closing||demo)return;
        if(Active!=null&&NotificationEngine.ResolveIfMoist(store.Settings,Active,store.Settings.Calibration,samples,DateTime.UtcNow))Save();
        if(!telegramReading&&!telegramConfiguring&&!telegramReadPaused&&store.Settings.Telegram.PollEnabled&&DateTime.UtcNow>=telegramNextRead)ReadTelegram();
        if(telegramSending||telegramConfiguring||telegramReadPaused||!store.Settings.Telegram.Enabled||!telegramSecrets.Paired||Active==null)return;
        var report=CurrentCareReport();if(!report.CanStartWatering)return;
        var e=NotificationEngine.Active(store.Settings,Active.LocalId,"Telegram");
        if(e==null){e=TelegramRules.Prepare(store.Settings,Active,telegramSecrets,DateTime.UtcNow);try{store.Save();}catch{Status("Não foi possível salvar o alerta. Envio ao Telegram não iniciado.");return;}}
        if(NotificationEngine.CanAttempt(e,DateTime.UtcNow))await DispatchTelegram(e,report);
    }
    async void ReadTelegram() {
        telegramReading=true;int generation=telegramGeneration;var secret=telegramSecrets;long previousOffset=store.Settings.Telegram.NextOffset;
        try {
            TelegramRules.ValidateToken(secret.Token);
            var updates=await telegramTransport.Updates(secret,store.Settings.Telegram.NextOffset);
            if(closing||generation!=telegramGeneration)return;
            telegramConnection.Success(DateTime.UtcNow);
            bool secretChanged=false,paired=false,watered=false;
            var answers=new System.Collections.Generic.List<System.Tuple<string,string>>();
            foreach(var u in updates) {
                var result=TelegramRules.Apply(store.Settings,secret,u,last,store.Settings.Calibration,DateTime.UtcNow);
                secretChanged|=result.SecretChanged;paired|=result.PairedNow;watered|=result.WateringAdded;
                if(result.Answer)answers.Add(System.Tuple.Create(u.CallbackId,result.Feedback));
                store.Settings.Telegram.NextOffset=Math.Max(store.Settings.Telegram.NextOffset,u.UpdateId+1);
            }
            if(secretChanged)store.SaveTelegramSecrets(secret);
            if(updates.Count>0){store.Settings.Telegram.LastUpdateUtc=DateTime.UtcNow;store.Save();}
            if(paired){telegramStatus="Seu celular foi vinculado. Envie um teste e ative os avisos no app.";Status("Telegram vinculado ao Raiz.");}
            else if(watered){telegramStatus="Já reguei recebido e salvo nos logs.";Status(telegramStatus);}
            else if(updates.Count>0)telegramStatus="Respostas do Telegram verificadas.";
            UpdateCareCards();RefreshWateringGrid();UpdateTelegramPage();UpdateWhatsAppPage();
            foreach(var answer in answers){try{await telegramTransport.Answer(secret,answer.Item1,answer.Item2);}catch{telegramStatus="Registro salvo; não foi possível mostrar a confirmação no Telegram.";}}
            if(paired)try{await telegramTransport.Call(secret.Token,"sendMessage",new{chat_id=secret.ChatId,text="Seu Telegram foi vinculado ao Raiz. Ative os avisos no app do computador para receber alertas de rega."});}catch{telegramStatus="Vínculo salvo. A mensagem de boas-vindas não pôde ser enviada.";}
            telegramNextRead=DateTime.UtcNow.AddSeconds(1);
        }catch(TelegramApiException ex){if(generation==telegramGeneration&&!closing){telegramConnection.Failure(ex.Code);telegramStatus=ex.Message;telegramNextRead=DateTime.UtcNow.AddSeconds(Math.Max(30,ex.RetryAfter));if(ex.Code==401||ex.Code==403||ex.Code==409)telegramReadPaused=true;UpdateTelegramPage();}}
        catch{if(generation==telegramGeneration&&!closing){store.Settings.Telegram.NextOffset=previousOffset;telegramReadPaused=true;telegramStatus="Não foi possível salvar as respostas. Confira a pasta de dados e clique em Receber respostas novamente.";UpdateTelegramPage();}}
        finally{telegramReading=false;}
    }
    async Task DispatchTelegram(NotificationEvent e,CareReport report) {
        telegramSending=true;int generation=telegramGeneration;var secret=telegramSecrets;
        try {
            e.Attempts++;e.LastAttemptUtc=DateTime.UtcNow;e.State="sending";e.Detail="Envio ao Telegram em andamento.";store.Save();UpdateTelegramPage();
            double? index=last==null?null:store.Settings.Calibration.Soil(last.Soil);
            var result=await telegramTransport.Send(secret,e,index,report==null?null:report.EstimatedMl);
            if(closing)return;
            if(generation==telegramGeneration&&result.State=="sent")telegramConnection.Success(DateTime.UtcNow);
            if(!string.IsNullOrEmpty(result.MessageId))e.MetaMessageId=result.MessageId;
            if(result.RetryAllowed)e.RetryAfterUtc=DateTime.UtcNow.AddSeconds(Math.Max(300,result.RetryAfterSeconds));
            if(!e.ConfirmedUtc.HasValue&&!e.ClosedUtc.HasValue){e.State=result.State;e.Detail=result.Detail;e.RetryAllowed=result.RetryAllowed;}
            store.Save();if(generation==telegramGeneration){telegramStatus=e.Detail;UpdateTelegramPage();Status("Telegram: "+e.Detail);}
        }catch{if(!e.ConfirmedUtc.HasValue&&!e.ClosedUtc.HasValue){e.State="uncertain";e.Detail="Envio ao Telegram com resultado indefinido. Confira o celular antes de repetir.";}Save();}
        finally{telegramSending=false;}
    }
    void TelegramPage(TableLayoutPanel stack) {
        Heading(stack,"Avisos no celular","Receba o alerta de rega e confirme pelo Telegram. Mantenha o computador e o Raiz ligados.");
        telegramConnectionLabel=Theme.Label("",10,null,true);telegramConnectionLabel.AutoSize=false;telegramConnectionLabel.Padding=new Padding(14,10,14,8);telegramConnectionLabel.AccessibleName="Conexão do bot";Add(stack,telegramConnectionLabel,62);
        telegramCard=new DecisionCard();Add(stack,telegramCard,145);
        Add(stack,Buttons(Theme.Button("Ativar avisos",(s,e)=>EnableTelegram(),true),Theme.Button("Pausar avisos",(s,e)=>{store.Settings.Telegram.Enabled=false;Save();UpdateTelegramPage();}),Theme.Button("Enviar teste",async(s,e)=> {
            if(demo||telegramSending||telegramConfiguring||!telegramSecrets.Paired||Active==null){MessageBox.Show(this,"Saia da demonstração, escolha a planta e conclua o vínculo antes de testar.");return;}
            var test=TelegramRules.Prepare(store.Settings,Active,telegramSecrets,DateTime.UtcNow,true);await DispatchTelegram(test,null);
        })),56);
        Disclosure(stack,telegramSecrets.Paired?"Conexão do Telegram · alterar ou reconectar":"Conectar meu Telegram",setup=>{
            Note(setup,"Conectar seu celular","No Telegram, abra @BotFather e envie /newbot. Cole o token abaixo e salve. Depois, envie ao seu bot o comando de vínculo mostrado no cartão acima. Não compartilhe seu token.");
        var token=Theme.Input("",340);token.UseSystemPasswordChar=true;token.AccessibleName="Token do bot Telegram";
        Add(setup,Buttons(token,Theme.Button("Salvar token e preparar vínculo",async(s,e)=> {
            if(demo){MessageBox.Show(this,"Saia da demonstração antes de vincular uma conta real.");return;}
            string value=token.Text.Trim();try{TelegramRules.ValidateToken(value);}catch(Exception ex){ShowError(ex);return;}
            telegramConfiguring=true;telegramGeneration++;telegramConnection.Reset();UpdateTelegramPage();var button=(Button)s;button.Enabled=false;
            try {
                var valid=await telegramTransport.ValidateBot(value);if(closing||token.IsDisposed)return;
                bool same=telegramSecrets.Token==value&&telegramSecrets.BotId==valid.BotId;
                if(same) {valid.ChatId=telegramSecrets.ChatId;valid.UserId=telegramSecrets.UserId;valid.PairedUtc=telegramSecrets.PairedUtc;}
                if(!valid.Paired)TelegramRules.BeginPairing(valid,DateTime.UtcNow);
                if(!same)foreach(var old in store.Settings.Notifications.Where(n=>NotificationEngine.Channel(n)=="Telegram"&&!n.ClosedUtc.HasValue)){old.ClosedUtc=DateTime.UtcNow;old.State="superseded";old.Detail="Bot alterado; aviso anterior encerrado.";}
                var oldOptions=store.Settings.Telegram;store.SaveTelegramSecrets(valid);telegramSecrets=valid;
                store.Settings.Telegram=new TelegramOptions{Enabled=same&&oldOptions.Enabled,PollEnabled=true,NextOffset=same?oldOptions.NextOffset:0,BotFingerprint=WhatsAppRules.Hash(value),LastUpdateUtc=same?oldOptions.LastUpdateUtc:null};
                NotificationEngine.ResetFailed(store.Settings,DateTime.UtcNow,"Telegram");store.Save();telegramReadPaused=false;telegramNextRead=DateTime.MinValue;telegramConnection.Success(DateTime.UtcNow);token.Clear();telegramStatus=valid.Paired?"Bot validado; vínculo anterior preservado.":"Bot validado. Envie o comando de vínculo no seu celular.";UpdateTelegramPage();
            }catch(Exception ex){telegramStatus=ex is ApiException?ex.Message:"Não foi possível salvar a configuração do Telegram.";ShowError(ex);UpdateTelegramPage();}
            finally{telegramConfiguring=false;if(!button.IsDisposed)button.Enabled=true;UpdateTelegramPage();}
        },true)),55);
        Add(setup,Buttons(Theme.Button("Abrir meu bot",(s,e)=>OpenTelegramBot()),Theme.Button("Copiar comando de vínculo",(s,e)=>CopyTelegramPairing()),Theme.Button("Gerar novo vínculo",(s,e)=>NewTelegramPairing())),50);

            Add(setup,Buttons(Theme.Button("Abrir BotFather",(s,e)=>OpenUrl("https://t.me/BotFather")),Theme.Button("Receber respostas novamente",(s,e)=>{try{store.SaveTelegramSecrets(telegramSecrets);store.Save();LoadTelegram();telegramStatus="Recepção reiniciada.";UpdateTelegramPage();}catch(Exception ex){ShowError(ex);}})),56);
        },!telegramSecrets.Paired);
        Disclosure(stack,"Histórico dos avisos",history=>{
            telegramGrid=Grid(new[]{"Quando","Planta","Estado","Detalhe"});Add(history,telegramGrid,260);telegramGridSignature="";
        });
        Disclosure(stack,"Como funcionam os avisos",details=>{
            Note(details,"Confirmação da rega","Toque em Já reguei depois de regar. A confirmação é salva uma única vez no histórico. Sem quantidade medida, o volume fica não informado. O computador, o Raiz e a internet precisam estar ativos. O Telegram guarda respostas por até 24 horas.");
            Note(details,"Vínculo protegido","Somente a conta vinculada pode confirmar. O código vale dez minutos, com até cinco erros. O token fica protegido pelo usuário Windows.");
            Add(details,Buttons(Theme.Button("Configuração anterior do WhatsApp",(s,e)=>Navigate("WhatsApp e avisos"))),56);
        });
        UpdateTelegramPage();
    }
    void UpdateTelegramPage() {
        if(telegramCard==null||telegramCard.IsDisposed)return;
        if(telegramConnectionLabel!=null&&!telegramConnectionLabel.IsDisposed){
            var connection=telegramConnection.Describe(!string.IsNullOrWhiteSpace(telegramSecrets.Token),store.Settings.Telegram.PollEnabled,telegramReadPaused,demo,telegramConfiguring,DateTime.UtcNow);
            telegramConnectionLabel.Text=connection.Title+"\n"+connection.Body;telegramConnectionLabel.ForeColor=connection.Severity=="ok"?Theme.Green:connection.Severity=="attention"?Theme.Amber:Theme.Muted;
            telegramConnectionLabel.BackColor=connection.Severity=="ok"?Color.FromArgb(228,239,224):connection.Severity=="attention"?Color.FromArgb(255,244,222):Color.FromArgb(234,238,232);
        }
        string title,body;var s=telegramSecrets;
        if(string.IsNullOrWhiteSpace(s.Token)){title="Telegram ainda não configurado";body="Crie seu bot no BotFather e cole o token no campo protegido abaixo.";}
        else if(s.Paired){title=store.Settings.Telegram.Enabled?"Alertas pelo Telegram ativados":"Celular vinculado · avisos pausados";body="Bot @"+s.BotUsername+". "+telegramStatus+" A resposta Já reguei será registrada nos logs.";}
        else if(s.PairExpiresUtc.HasValue&&DateTime.UtcNow<=s.PairExpiresUtc.Value&&!string.IsNullOrEmpty(s.PairCode)){title="No seu bot, envie: /start "+s.PairCode;body="Procure @"+s.BotUsername+" no Telegram do celular e envie o comando acima. Código válido até "+s.PairExpiresUtc.Value.ToLocalTime().ToString("HH:mm")+". "+telegramStatus;}
        else {title="Vínculo pendente: gere um novo código";body=telegramStatus+" Clique em Gerar novo vínculo para tentar novamente.";}
        if(telegramReadPaused){title="Recepção do Telegram precisa de atenção";body=telegramStatus;}
        telegramCard.Set(new Advice(title,body,s.Paired&&store.Settings.Telegram.Enabled&&!telegramReadPaused?"ok":"pending"));
        if(telegramGrid==null||telegramGrid.IsDisposed)return;var list=store.Settings.Notifications.Where(n=>NotificationEngine.Channel(n)=="Telegram").OrderByDescending(n=>n.CreatedUtc).Take(50).ToList();string sig=Json.Write(list);if(sig==telegramGridSignature)return;telegramGridSignature=sig;telegramGrid.Rows.Clear();
        foreach(var n in list)telegramGrid.Rows.Add(n.CreatedUtc.ToLocalTime().ToString("dd/MM HH:mm:ss"),(n.IsTest?"[TESTE] ":"")+n.PlantName,n.State=="sent"?"Criada no Telegram":StateText(n.State),n.Detail);
    }
    void OpenTelegramBot() {
        if(string.IsNullOrEmpty(telegramSecrets.BotUsername)){MessageBox.Show(this,"Salve e valide o token do bot primeiro.");return;}
        string link="https://t.me/"+telegramSecrets.BotUsername;if(!telegramSecrets.Paired&&!string.IsNullOrEmpty(telegramSecrets.PairCode)&&telegramSecrets.PairExpiresUtc>DateTime.UtcNow)link+="?start="+telegramSecrets.PairCode;
        OpenUrl(link);
    }
    void CopyTelegramPairing(){if(telegramSecrets.Paired){MessageBox.Show(this,"Seu celular já está vinculado.");return;}if(string.IsNullOrEmpty(telegramSecrets.PairCode)||!telegramSecrets.PairExpiresUtc.HasValue||telegramSecrets.PairExpiresUtc<DateTime.UtcNow){MessageBox.Show(this,"Gere um novo vínculo primeiro.");return;}Clipboard.SetText("/start "+telegramSecrets.PairCode);Status("Comando copiado. Envie-o ao seu bot no Telegram.");}
    void NewTelegramPairing() {
        if(demo){MessageBox.Show(this,"Saia da demonstração antes de vincular uma conta real.");return;}
        try{TelegramRules.ValidateToken(telegramSecrets.Token);telegramGeneration++;TelegramRules.BeginPairing(telegramSecrets,DateTime.UtcNow);store.Settings.Telegram.Enabled=false;store.Settings.Telegram.PollEnabled=true;store.SaveTelegramSecrets(telegramSecrets);store.Save();telegramReadPaused=false;telegramNextRead=DateTime.MinValue;telegramStatus="Novo código gerado. Envie-o em conversa privada ao seu bot.";UpdateTelegramPage();}catch(Exception ex){ShowError(ex);}
    }
    void EnableTelegram() {
        if(demo||!telegramSecrets.Paired){MessageBox.Show(this,"Saia da demonstração e vincule seu celular antes de ativar os avisos.");return;}
        store.Settings.Telegram.Enabled=true;store.Settings.Telegram.PollEnabled=true;store.Settings.WhatsApp.Enabled=false;telegramReadPaused=false;Save();telegramStatus="Telegram escolhido para os avisos; envio automático pelo WhatsApp pausado.";UpdateTelegramPage();
    }
}
}
