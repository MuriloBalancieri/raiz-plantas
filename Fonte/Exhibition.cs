using System;using System.Drawing;using System.Linq;using System.Windows.Forms;using System.Collections.Generic;using System.Threading.Tasks;
namespace Raiz {
public static class ExhibitNotifications {
    public static List<Tuple<TelegramUpdate,TelegramApplyResult>> ApplyTests(Settings settings,TelegramSecrets secret,IEnumerable<TelegramUpdate> updates,ISet<long> seen,DateTime now){
        var result=new List<Tuple<TelegramUpdate,TelegramApplyResult>>();
        foreach(var u in updates){
            if(seen.Contains(u.UpdateId)||u.Kind!="callback"||string.IsNullOrEmpty(u.Data)||!u.Data.StartsWith(TelegramRules.Prefix))continue;
            var id=u.Data.Substring(TelegramRules.Prefix.Length);var e=settings.Notifications.LastOrDefault(n=>n.Id==id&&n.Channel=="Telegram"&&n.IsTest&&n.Kind=="exhibition");
            if(e==null)continue;
            var applied=TelegramRules.Apply(settings,secret,u,null,settings.Calibration,now);
            if(applied.Answer)result.Add(Tuple.Create(u,applied));
        }return result;
    }
}
public class TeachingCard:ReadableCard {
    public TeachingCard(){title.Font=Theme.F(16,FontStyle.Bold);body.Font=Theme.F(13);}
}
public class LearningJourney:Control {
    public LearningJourney(){DoubleBuffered=true;Height=108;BackColor=Theme.Bg;Margin=new Padding(0,0,0,12);AccessibleName="Sensores medem; Arduino envia; Raiz interpreta; pessoa cuida";}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var titles=new[]{"1. MEDIR","2. ENVIAR","3. ENTENDER","4. CUIDAR"};var texts=new[]{"Solo e luz\nSensores no vaso","Leituras pela USB\nArduino","Espécie e contexto\nAplicativo Raiz","Aviso e rega manual\nPessoa + celular"};
        int gap=20,w=(Width-gap*3)/4;if(w<80)return;
        using(var heading=Theme.F(11,FontStyle.Bold))using(var body=Theme.F(10))using(var ink=new SolidBrush(Theme.Ink))using(var muted=new SolidBrush(Theme.Muted))using(var border=new Pen(Theme.Line))using(var arrow=new Pen(Theme.Green,2)){
            arrow.EndCap=System.Drawing.Drawing2D.LineCap.ArrowAnchor;
            for(int i=0;i<4;i++){int x=i*(w+gap);e.Graphics.FillRectangle(Brushes.White,x,0,w,Height-2);e.Graphics.DrawRectangle(border,x,0,w-1,Height-3);e.Graphics.DrawString(titles[i],heading,ink,new RectangleF(x+10,12,w-20,26));e.Graphics.DrawString(texts[i],body,muted,new RectangleF(x+10,43,w-20,60));if(i<3)e.Graphics.DrawLine(arrow,x+w+3,Height/2,x+w+gap-3,Height/2);}
        }
    }
}
public class ExhibitReading {
    public bool Simulated;public double? Soil,Light;public string Detail;
}
public static class Exhibition {
    public static readonly string[] Titles={"Por que acompanhar uma planta?","O que os sensores conseguem medir?","Como uma leitura vira uma orientação?","Como as APIs ajudam?","Como o cuidado fica registrado?"};
    public static ExhibitReading Reading(bool live,int scenario,Sample last,Calibration c,bool generalDemo,DateTime now){
        if(!live){var soil=new double?[]{65,8,65,null};var light=new double?[]{75,75,15,null};scenario=Math.Max(0,Math.Min(3,scenario));return new ExhibitReading{Simulated=true,Soil=soil[scenario],Light=light[scenario],Detail="EXEMPLO SIMULADO · valores ilustrativos, sem gravação"};}
        if(generalDemo)return new ExhibitReading{Detail="O modo de demonstração geral está ativo. Encerre-o em Configurações para usar sensores reais."};
        if(last==null||!last.Fresh(now))return new ExhibitReading{Detail="DADOS REAIS · aguardando leituras atuais. Conecte o Arduino em Sensores ou escolha Exemplos simulados."};
        return new ExhibitReading{Soil=c.Soil(last.Soil),Light=c.Light(last.Light),Detail="DADOS REAIS · solo bruto "+last.Soil+" / luz bruta "+last.Light+" · recebido às "+last.Utc.ToLocalTime().ToString("HH:mm:ss")+". Percentuais dependem da calibração."};
    }
    public static string Explain(int step,ExhibitReading r,bool confirmed){
        switch(step){
            case 0:return "Uma planta precisa de cuidados que mudam conforme a espécie e o ambiente. O Raiz reúne medições do vaso e informações externas para ajudar a decidir quando conferir o solo e a luz.\n\nA rega é feita pela pessoa. O projeto acompanha e explica o cuidado.";
            case 1:return "O sensor capacitivo responde à condição do substrato. O LDR responde à luz que chega ao vaso. O Arduino transforma esses sinais em números de 0 a 1023 e envia os pares pela USB.\n\nNa montagem real, faça uma sombra breve no LDR e observe a mudança. Mantenha a eletrônica do sensor de solo fora da terra e seca.";
            case 2:
                if(!r.Soil.HasValue||!r.Light.HasValue)return "Sem leituras calibradas e atuais, o sistema informa que faltam dados. Ele precisa de duas referências locais para cada sensor: seco/úmido e escuro/claro.\n\nEscolha outro exemplo para ver a interpretação ou confira os sensores reais.";
                if(!r.Simulated)return "Os percentuais comparam as leituras atuais com as referências desta montagem. Solo: "+r.Soil.Value.ToString("0")+"%; luz: "+r.Light.Value.ToString("0")+"%.\n\nA decisão real considera a espécie, referências válidas e continuidade das leituras. Um percentual isolado não basta para decidir a rega.";
                if(r.Soil<10)return "Neste exemplo, o solo está próximo da referência seca. O app pediria para conferir o substrato antes de regar, após confirmar que a condição persiste.\n\nO valor de 8% é ilustrativo. O gatilho real varia com o perfil e os ajustes do vaso.";
                if(r.Light<20)return "Neste exemplo, a luz caiu bastante. O app compara a leitura com uma referência de iluminação validada para a planta e considera se é dia.\n\nUma sombra breve não deve gerar um aviso de pouca luz. A condição precisa persistir.";
                return "Neste exemplo, o solo está acima da referência seca e há mais luz do que no cenário de sombra. A orientação seria acompanhar o vaso, conferindo o perfil da espécie.\n\n100% é a referência local escolhida: não é uma meta de umidade, e o LDR não mede luz em lux.";
            case 3:return "Uma API é um serviço que outro programa consulta pela internet. A Perenual fornece orientações da espécie; a Open-Meteo fornece o clima externo da cidade.\n\nO clima da cidade não mede o ar dentro de casa. As categorias de rega e sol não informam uma dose exata em mL ou uma luz ideal em lux.";
            default:return confirmed?"EXEMPLO: a pessoa confirmou Já reguei. Em uso normal, a confirmação entra no histórico; ela pode informar a quantidade medida e se observou drenagem.\n\nA confirmação local é apenas visual. Nenhuma rega real foi salva. Use Enviar demonstração ao celular para mostrar uma mensagem real de teste.":"Quando uma condição real persiste, o Raiz pode avisar no celular pelo Telegram. A pessoa confere a planta, rega se necessário e confirma Já reguei.\n\nO histórico permite acompanhar a resposta do vaso. Experimente a confirmação ilustrativa abaixo.";
        }
    }
}
public partial class MainForm {
    int exhibitStep,exhibitScenario;bool exhibitLive,exhibitConfirmed,exhibitFullscreen;
    TeachingCard exhibitExplanation,exhibitIntegration;Metric exhibitSoil,exhibitLight;Label exhibitStamp;Button exhibitPrevious,exhibitNext,exhibitFullscreenButton;FormBorderStyle exhibitOldBorder;FormWindowState exhibitOldState;Rectangle exhibitOldBounds;
    TableLayoutPanel exhibitShell;Control exhibitMeters,exhibitScenarioRow;
    Button exhibitSendButton;Label exhibitBotLabel;string exhibitTestId,exhibitBotStatus;bool exhibitTestReading;DateTime exhibitNextTestRead=DateTime.MinValue,exhibitLastSend=DateTime.MinValue;readonly HashSet<long> exhibitSeenUpdates=new HashSet<long>();
    void ExhibitionTransition(string target){
        bool entering=target=="Apresentação"||(target=="Histórico"&&historyFiction);
        if(target!="Apresentação"&&exhibitFullscreen)SetExhibitFullscreen(false);
        if(exhibitShell==null)exhibitShell=content.Parent as TableLayoutPanel;
        if(exhibitShell!=null){exhibitShell.RowStyles[1].Height=entering?0:200;exhibitShell.RowStyles[2].Height=entering?0:38;}
        if(dockPlotter!=null)dockPlotter.Visible=!entering;footer.Visible=!entering;
    }
    void ExhibitionPage(TableLayoutPanel stack){
        var header=new Panel{Height=76,Margin=new Padding(0,0,0,8)};var title=Theme.Label("Raiz · da leitura ao cuidado",22,null,true);title.Location=new Point(0,0);header.Controls.Add(title);var subtitle=Theme.Label("Apresentação interativa · avisos automáticos pausados nesta tela",10,Theme.Muted);subtitle.Location=new Point(0,44);header.Controls.Add(subtitle);Add(stack,header,76);
        exhibitPrevious=Theme.Button("← Anterior",(s,e)=>MoveExhibit(-1));exhibitNext=Theme.Button("Próxima →",(s,e)=>MoveExhibit(1),true);
        exhibitFullscreenButton=Theme.Button(exhibitFullscreen?"Sair da tela cheia":"Tela cheia · F11",(s,e)=>SetExhibitFullscreen(!exhibitFullscreen));
        Add(stack,Buttons(exhibitPrevious,exhibitNext,Theme.Button("Recomeçar",(s,e)=>{exhibitStep=0;exhibitConfirmed=false;Navigate("Apresentação");}),Theme.Button("Mostrar histórico da planta",(s,e)=>ShowDemoHistory()),exhibitFullscreenButton,Theme.Button("Voltar ao app",(s,e)=>Navigate("Visão geral"))),54);
        var journey=new LearningJourney{Visible=exhibitStep==0};Add(stack,journey,108);stack.RowStyles[stack.GetRow(journey)].SizeType=SizeType.AutoSize;journey.Dock=DockStyle.Top;
        var source=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=220,AccessibleName="Fonte das leituras na apresentação"};source.Items.AddRange(new object[]{"Exemplos simulados","Sensores reais"});source.SelectedIndex=exhibitLive?1:0;source.SelectedIndexChanged+=(s,e)=>{exhibitLive=source.SelectedIndex==1;exhibitConfirmed=false;UpdateExhibition();};
        exhibitScenarioRow=Buttons(source,ExhibitScenarioButton("Solo úmido",0),ExhibitScenarioButton("Solo seco",1),ExhibitScenarioButton("Pouca luz",2),ExhibitScenarioButton("Sem sinal",3));Add(stack,exhibitScenarioRow,54);
        exhibitStamp=Theme.Label("",10,Theme.Green,true);exhibitStamp.AutoSize=false;Add(stack,exhibitStamp,42);stack.RowStyles[stack.GetRow(exhibitStamp)].SizeType=SizeType.AutoSize;exhibitStamp.Height=42;exhibitStamp.Dock=DockStyle.Top;
        exhibitExplanation=new TeachingCard();Add(stack,exhibitExplanation,160);
        exhibitMeters=new TableLayoutPanel{ColumnCount=2,Height=140,Margin=new Padding(0,0,0,12)};((TableLayoutPanel)exhibitMeters).ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));((TableLayoutPanel)exhibitMeters).ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        exhibitSoil=new Metric("SOLO · REFERÊNCIA LOCAL","Comparação com seco e úmido",Theme.Green);exhibitLight=new Metric("LUZ · REFERÊNCIA LOCAL","Comparação com escuro e claro",Theme.Amber);((TableLayoutPanel)exhibitMeters).Controls.Add(exhibitSoil,0,0);((TableLayoutPanel)exhibitMeters).Controls.Add(exhibitLight,1,0);exhibitMeters.AutoSize=false;Add(stack,exhibitMeters,140);
        // Auto-size the wrapper so hidden examples do not leave an empty row.
        var meterRow=stack.GetRow(exhibitMeters);stack.RowStyles[meterRow].SizeType=SizeType.AutoSize;exhibitMeters.Dock=DockStyle.Top;
        exhibitIntegration=new TeachingCard();Add(stack,exhibitIntegration,140);
        exhibitSendButton=Theme.Button("Enviar demonstração ao celular",async(s,e)=>await SendExhibitTest(),true);
        var confirmRow=Buttons(exhibitSendButton,Theme.Button("Simular Já reguei",(s,e)=>{exhibitConfirmed=true;UpdateExhibition();}),Theme.Button("Configurar meu bot",(s,e)=>Navigate("Telegram e avisos")));confirmRow.Tag="exhibit-confirm";Add(stack,confirmRow,54);confirmRow.Visible=exhibitStep==4;
        exhibitBotLabel=Theme.Label("",10,Theme.Muted);exhibitBotLabel.AutoSize=false;exhibitBotLabel.Height=80;exhibitBotLabel.Visible=exhibitStep==4;Add(stack,exhibitBotLabel,80);stack.RowStyles[stack.GetRow(exhibitBotLabel)].SizeType=SizeType.AutoSize;exhibitBotLabel.Dock=DockStyle.Top;
        UpdateExhibition();
    }
    Button ExhibitScenarioButton(string text,int scenario){var button=Theme.Button(text,(s,e)=>ChooseExhibitScenario(scenario));button.Tag=scenario;return button;}
    public void OpenPresentation(){Navigate("Apresentação");}
    void ChooseExhibitScenario(int scenario){exhibitLive=false;exhibitScenario=scenario;exhibitConfirmed=false;Navigate("Apresentação");}
    void MoveExhibit(int delta){exhibitStep=Math.Max(0,Math.Min(4,exhibitStep+delta));Navigate("Apresentação");}
    void UpdateExhibition(){
        if(page!="Apresentação"||exhibitExplanation==null||exhibitExplanation.IsDisposed)return;var r=Exhibition.Reading(exhibitLive,exhibitScenario,last,store.Settings.Calibration,demo,DateTime.UtcNow);
        if(exhibitSendButton!=null&&!exhibitSendButton.IsDisposed){exhibitSendButton.Enabled=telegramSecrets.Paired&&Active!=null&&!telegramSending&&!telegramConfiguring&&DateTime.UtcNow-exhibitLastSend>=TimeSpan.FromSeconds(30);exhibitBotLabel.Text=exhibitBotStatus??(!telegramSecrets.Paired?"Vincule seu celular em Configurar meu bot para enviar uma demonstração real pelo Telegram.":Active==null?"Escolha uma planta em Minha planta antes de enviar ao celular.":"Bot vinculado. O botão envia uma mensagem real de DEMONSTRAÇÃO. Os avisos automáticos continuam pausados.");}
        exhibitPrevious.Enabled=exhibitStep>0;exhibitNext.Enabled=exhibitStep<4;exhibitStamp.Text=r.Detail;
        exhibitExplanation.SetText((exhibitStep+1)+" de 5 · "+Exhibition.Titles[exhibitStep],Exhibition.Explain(exhibitStep,r,exhibitConfirmed));
        exhibitMeters.Visible=exhibitStep==1||exhibitStep==2;exhibitScenarioRow.Visible=exhibitStep==1||exhibitStep==2;exhibitStamp.Visible=exhibitStep==1||exhibitStep==2;
        foreach(var button in exhibitScenarioRow.Controls.OfType<Button>()){bool selected=!exhibitLive&&button.Tag is int&&(int)button.Tag==exhibitScenario;button.BackColor=selected?Theme.Green:Color.White;button.ForeColor=selected?Color.White:Theme.Ink;}
        exhibitSoil.ValueLabel.Text=r.Soil.HasValue?r.Soil.Value.ToString("0")+"%":"—";exhibitLight.ValueLabel.Text=r.Light.HasValue?r.Light.Value.ToString("0")+"%":"—";
        exhibitIntegration.Visible=exhibitStep==3;
        if(exhibitStep==3){var p=Active;string species=p==null?"Cadastre uma planta em Minha planta para mostrar a orientação salva da espécie.":"Cadastro salvo: "+p.Name+". Rega: "+(p.Watering??"não informada")+". Luz: "+(p.Sunlight??"não informada")+".";var w=store.Settings.Weather;string climate=w!=null&&w.Fresh(DateTime.UtcNow)?"Clima externo consultado: "+(w.Temperature.HasValue?w.Temperature.Value.ToString("0.0")+" °C":"temperatura indisponível")+" / "+(w.Humidity.HasValue?w.Humidity.Value.ToString("0")+"% de umidade do ar":"umidade indisponível")+". Cidade: "+(store.Settings.Location==null?"não informada":store.Settings.Location.ToString())+".":"Clima externo sem consulta atual. O roteiro continua disponível mesmo sem internet.";exhibitIntegration.SetText("As informações disponíveis no app",species+"\n\n"+climate);}
    }
    async Task SendExhibitTest(){
        if(page!="Apresentação"||!telegramSecrets.Paired||Active==null||telegramSending||telegramConfiguring||DateTime.UtcNow-exhibitLastSend<TimeSpan.FromSeconds(30))return;
        exhibitLastSend=DateTime.UtcNow;exhibitBotStatus="Enviando demonstração ao seu celular…";UpdateExhibition();
        var test=TelegramRules.Prepare(store.Settings,Active,telegramSecrets,DateTime.UtcNow,true);test.Kind="exhibition";exhibitTestId=test.Id;
        await DispatchTelegram(test,null);
        if(closing)return;exhibitBotStatus=test.State=="sent"?"Mensagem criada no Telegram. Confira o celular e toque em Confirmar demonstração. Novo envio disponível em 30 segundos.":test.Detail;exhibitNextTestRead=DateTime.MinValue;UpdateExhibition();
    }
    async Task PollExhibitTest(){
        if(closing||!ExhibitPausesNotifications||exhibitTestReading||telegramReading||telegramConfiguring||!telegramSecrets.Paired||DateTime.UtcNow<exhibitNextTestRead||string.IsNullOrEmpty(exhibitTestId))return;
        var test=store.Settings.Notifications.LastOrDefault(n=>n.Id==exhibitTestId&&n.IsTest&&n.Kind=="exhibition");if(test==null||test.State!="sent"||test.ConfirmedUtc.HasValue||DateTime.UtcNow-test.CreatedUtc>TimeSpan.FromMinutes(10))return;
        exhibitTestReading=true;int generation=telegramGeneration;var secret=telegramSecrets;
        try{
            // Keep the normal offset unchanged, preserving unrelated responses for normal use.
            var updates=await telegramTransport.Updates(secret,store.Settings.Telegram.NextOffset);
            if(closing||!ExhibitPausesNotifications||generation!=telegramGeneration)return;
            var results=ExhibitNotifications.ApplyTests(store.Settings,secret,updates,exhibitSeenUpdates,DateTime.UtcNow);
            if(results.Count>0){store.SaveConfiguration();foreach(var item in results){exhibitSeenUpdates.Add(item.Item1.UpdateId);try{await telegramTransport.Answer(secret,item.Item1.CallbackId,item.Item2.Feedback);}catch{}}}
            if(test.ConfirmedUtc.HasValue)exhibitBotStatus="Confirmação recebida do celular! A demonstração foi concluída sem registrar uma rega real.";
            else if(updates.Count>=50){exhibitBotStatus="Há muitas respostas anteriores pendentes. Saia da apresentação para processá-las e depois volte para testar a confirmação.";exhibitNextTestRead=DateTime.UtcNow.AddMinutes(10);}
            UpdateExhibition();
        }catch(TelegramApiException ex){exhibitBotStatus=ex.Message;exhibitNextTestRead=DateTime.UtcNow.AddSeconds(Math.Max(30,ex.RetryAfter));UpdateExhibition();}
        catch{exhibitBotStatus="Não foi possível conferir a resposta agora. O teste não registra rega real.";exhibitNextTestRead=DateTime.UtcNow.AddSeconds(30);UpdateExhibition();}
        finally{exhibitTestReading=false;if(exhibitNextTestRead<DateTime.UtcNow)exhibitNextTestRead=DateTime.UtcNow.AddSeconds(2);}
    }
    void SetExhibitFullscreen(bool enabled){
        if(exhibitFullscreen==enabled)return;
        if(exhibitShell==null)exhibitShell=content.Parent as TableLayoutPanel;
        if(enabled){exhibitOldState=WindowState;exhibitOldBorder=FormBorderStyle;exhibitOldBounds=Bounds;WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;side.Visible=false;if(exhibitShell!=null)exhibitShell.ColumnStyles[0].Width=0;}
        else{WindowState=FormWindowState.Normal;FormBorderStyle=exhibitOldBorder;Bounds=exhibitOldBounds;WindowState=exhibitOldState;side.Visible=true;if(exhibitShell!=null)exhibitShell.ColumnStyles[0].Width=205;}
        exhibitFullscreen=enabled;if(exhibitFullscreenButton!=null&&!exhibitFullscreenButton.IsDisposed)exhibitFullscreenButton.Text=enabled?"Sair da tela cheia":"Tela cheia · F11";
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
        if(page=="Apresentação"){
            if(keyData==Keys.F11){SetExhibitFullscreen(!exhibitFullscreen);return true;}
            if(keyData==Keys.Escape){if(exhibitFullscreen)SetExhibitFullscreen(false);else Navigate("Visão geral");return true;}
            if(keyData==Keys.Right){MoveExhibit(1);return true;}if(keyData==Keys.Left){MoveExhibit(-1);return true;}
        }return base.ProcessCmdKey(ref msg,keyData);
    }
}
}
