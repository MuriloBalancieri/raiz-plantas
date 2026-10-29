using System;using System.Drawing;using System.Linq;using System.Windows.Forms;
namespace Raiz {
public class SetupStep {public string Name {get;set;} public bool Done {get;set;} public string Page {get;set;}}
public static class SetupChecklist {
    public static SetupStep[] Get(Settings settings,bool live,bool telegramPaired){
        var c=settings.Calibration;var p=settings.Plants.Find(x=>x.LocalId==settings.ActiveId);bool light=p!=null&&((p.Light!=null&&p.Light.Enabled)||(p.Care.LightReferenceRaw.HasValue&&p.Care.ReferenceDark==c.Dark&&p.Care.ReferenceBright==c.Bright&&(p.Care.LightSpeciesKey==null||p.Care.LightSpeciesKey==SpeciesCare.LightKey(p))));return new[]{
            new SetupStep{Name="1. Escolher a planta",Done=settings.Plants.Any(x=>x.LocalId==settings.ActiveId),Page="Minha planta"},
            new SetupStep{Name="2. Confirmar a cidade",Done=settings.Location!=null,Page="Clima da região"},
            new SetupStep{Name="3. Conectar o Arduino",Done=live,Page="Arduino e calibração"},
            new SetupStep{Name="4. Calibrar solo e luz",Done=c.Soil(500).HasValue&&c.Light(500).HasValue,Page="Arduino e calibração"},
            new SetupStep{Name="5. Validar a luz do local",Done=light,Page="Cuidados e rega"},
            new SetupStep{Name="6. Vincular o Telegram (opcional)",Done=telegramPaired,Page="Telegram e avisos"}};
    }
}
public partial class MainForm {
    DecisionCard mainRecommendation;Label setupProgress;DataGridView setupGrid;
    NotifyIcon backgroundIcon;bool exitRequested,backgroundInitialized,backgroundNotified;
    void InitializeBackground(){
        if(backgroundInitialized)return;backgroundInitialized=true;
        backgroundIcon=new NotifyIcon{Text="Raiz · sensores e avisos",Icon=Icon??SystemIcons.Application,Visible=true};
        var menu=new ContextMenuStrip();menu.Items.Add("Abrir Raiz",null,(s,e)=>RestoreFromBackground());menu.Items.Add("Encerrar Raiz",null,(s,e)=>{exitRequested=true;Close();});backgroundIcon.ContextMenuStrip=menu;
        backgroundIcon.DoubleClick+=(s,e)=>RestoreFromBackground();
    }
    void RestoreFromBackground(){Show();WindowState=FormWindowState.Normal;Activate();}
    bool KeepInBackground(FormClosingEventArgs e){
        if(exitRequested||e.CloseReason!=CloseReason.UserClosing||!backgroundInitialized)return false;
        if(!store.Settings.BackgroundChoiceMade){
            var choice=MessageBox.Show(this,"Deseja continuar recebendo medições e avisos perto do relógio do Windows?\n\nSim: mantém o Raiz em segundo plano.\nNão: encerra o aplicativo e a coleta.\nCancelar: volta para a janela.","Fechar o Raiz",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(choice==DialogResult.Cancel){e.Cancel=true;return true;}
            store.Settings.CloseToTray=choice==DialogResult.Yes;store.Settings.BackgroundChoiceMade=true;Save();
        }
        if(!store.Settings.CloseToTray)return false;
        e.Cancel=true;Hide();if(plotterWindow!=null&&!plotterWindow.IsDisposed)plotterWindow.Hide();
        if(!backgroundNotified){backgroundNotified=true;backgroundIcon.ShowBalloonTip(4000,"Raiz continua aberto","Medições e avisos continuam ativos. Clique duas vezes no ícone para abrir; use Encerrar para sair.",ToolTipIcon.Info);}return true;
    }
    void SetupPage(TableLayoutPanel stack){
        Heading(stack,"Vamos preparar sua planta","Siga os passos; suas escolhas ficam salvas e podem ser retomadas depois.");
        setupProgress=Theme.Label("",12,null,true);setupProgress.AutoSize=false;Add(stack,setupProgress,46);
        setupGrid=Grid(new[]{"Etapa","Situação"});setupGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;Add(stack,setupGrid,220);
        Add(stack,Buttons(Theme.Button("Abrir etapa selecionada",(s,e)=>{
            if(setupGrid.CurrentRow==null)return;var step=setupGrid.CurrentRow.Tag as SetupStep;if(step!=null)Navigate(step.Page);
        },true),Theme.Button("Ir para Hoje",(s,e)=>{store.Settings.SetupDismissed=true;Save();Navigate("Visão geral");})),56);
        Note(stack,"Como retomar","Use Primeiros passos em Hoje ou Configurações. O Arduino precisa enviar uma leitura atual para confirmar a conexão. O Telegram é opcional; sem ele, os avisos continuam no app.");UpdateSetup();
    }
    void UpdateSetup(){
        if(setupGrid==null||setupGrid.IsDisposed)return;
        int selected=setupGrid.CurrentRow==null?0:setupGrid.CurrentRow.Index;
        var steps=SetupChecklist.Get(store.Settings,!demo&&last!=null&&last.Fresh(DateTime.UtcNow),telegramSecrets.Paired);setupProgress.Text=steps.Count(x=>x.Done)+" de "+steps.Length+" etapas prontas";
        setupGrid.Rows.Clear();foreach(var step in steps){int row=setupGrid.Rows.Add(step.Name,step.Done?"Pronto":"Pendente");setupGrid.Rows[row].Tag=step;}
        if(setupGrid.Rows.Count>0)setupGrid.CurrentCell=setupGrid.Rows[Math.Min(selected,steps.Length-1)].Cells[0];
    }
}
}
