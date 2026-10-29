using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Raiz {
public class DecisionCard:ReadableCard {
    public void Set(Advice advice){SetText(advice.Title,advice.Body);var color=advice.Severity=="attention"?Theme.Amber:advice.Severity=="ok"?Theme.Green:Theme.Muted;title.ForeColor=color;stripe.BackColor=color;}
}
public partial class MainForm {
    DecisionCard careSoil,careWater,careLight;Label careSessionLabel;
    CareReport CurrentCareReport() {
        var report=CareEngine.Evaluate(Active,last,store.Settings.Weather,store.Settings.Calibration,samples,DateTime.UtcNow);
        if(demo){report.CanStartWatering=false;report.EstimatedMl=null;report.Water=new Advice("Demonstração: não aplicar água","Os dados são fictícios. Regas e referências não podem ser registradas neste modo.");}
        return report;
    }
    void CarePage(TableLayoutPanel stack) {
        Heading(stack,"Cuidados da planta",Active==null?"Escolha uma planta para começar.":Active.Name+" · "+(Active.Outdoor?"Ambiente externo":"Dentro de casa"));
        Add(stack,Buttons(Theme.Button("Já reguei",(s,e)=>AlreadyWatered(),true),Theme.Button("Histórico de regas",(s,e)=>Navigate("Histórico"))),56);
        careSoil=new DecisionCard();careWater=new DecisionCard();careLight=new DecisionCard();
        Add(stack,careSoil,130);Add(stack,careWater,140);Add(stack,careLight,130);
        careSessionLabel=Theme.Label("",10,Theme.Muted);careSessionLabel.AutoSize=false;Add(stack,careSessionLabel,88);
        Disclosure(stack,"Rega guiada · registrar quantidade",guided=>{
            Note(guided,"Aprender a quantidade deste vaso","Use quando for regar. Registre só a água realmente aplicada e finalize ao começar a drenar. Sem histórico, 30 mL é uma primeira porção experimental; o total ainda é desconhecido.");
            Add(guided,Buttons(Theme.Button("Iniciar rega",(s,e)=>StartWatering(),true),Theme.Button("Registrar porção",(s,e)=>PortionDialog())),56);
            Add(guided,Buttons(Theme.Button("Começou a drenar: finalizar",(s,e)=>FinishWatering()),Theme.Button("Encerrar sem aprender",(s,e)=>CancelWatering())),56);
        },Active!=null&&Active.Care.Session!=null);
        Disclosure(stack,"Luz e ajustes deste vaso",settings=>{
            Add(settings,Buttons(Theme.Button("Registrar referência de luz",(s,e)=>LightReferenceDialog(),true),Theme.Button("Ajustar aviso de rega",(s,e)=>DryTriggerDialog())),56);
            if(Active!=null&&Active.Care.LightReferenceRaw.HasValue)Note(settings,"Referência atual",Active.Care.LightReferenceNote);
            Note(settings,"Luz no local da planta",SpeciesCare.LightGuide(Active)+" A comparação usa sua referência local, com tolerância de 20% e persistência de 60 segundos. À noite, a avaliação fica pausada.");
            Add(settings,Buttons(Theme.Button("Troquei o vaso ou substrato",(s,e)=>ResetWaterLearning())),56);
        });
        Disclosure(stack,"Orientações da espécie e API",details=>SpeciesControls(details));
        UpdateCareCards();
    }
    void UpdateCareCards() {
        if(careSoil==null||careSoil.IsDisposed)return;
        var r=CurrentCareReport();careSoil.Set(r.Soil);careWater.Set(r.Water);careLight.Set(r.Light);
        var p=Active;var session=p==null||p.Care==null?null:p.Care.Session;
        if(session!=null)careSessionLabel.Text="Sessão iniciada em "+session.StartedUtc.ToLocalTime().ToString("HH:mm")+" · Total informado: "+session.TotalMl+" mL.\nRegistre somente porções já aplicadas. Se começou a drenar, pare de colocar água e finalize.\nSe trocar a calibração ou perder a sessão, encerre sem aprender.";
        else if(p!=null&&p.Care!=null&&p.Care.Waterings.Count>0) {
            var recent=p.Care.Waterings.OrderByDescending(x=>x.FinishedUtc).First();
            careSessionLabel.Text="Última rega registrada: "+recent.FinishedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")+" · "+(recent.VolumeUnknown?"volume não informado":recent.TotalMl+" mL")+" · "+(recent.Origin??"Guiada")+(recent.DrainageConfirmed?" · drenagem confirmada":" · sem aprendizado")+".\nO solo deve secar antes da próxima rega. Uma dose aprendida não é uma quantidade obrigatória.";
        } else careSessionLabel.Text="Sem regas registradas neste vaso. A primeira porção não é uma estimativa do total necessário.\nAs ações registram cuidados manuais; o aplicativo não aciona bomba ou irrigação.";
    }
    bool RealCareAllowed() {
        if(demo){MessageBox.Show(this,"Saia da demonstração para registrar cuidados reais.");return false;}
        if(Active==null){MessageBox.Show(this,"Escolha uma planta primeiro.");return false;}
        return true;
    }
    void StartWatering() {
        if(!RealCareAllowed())return;
        var r=CurrentCareReport();
        if(!r.CanStartWatering){MessageBox.Show(this,r.Soil.Title+"\n\n"+r.Soil.Body,"Antes de regar");return;}
        if(MessageBox.Show(this,"A leitura atingiu o gatilho local desta planta. Você conferiu que o substrato precisa de água?\n\nAo iniciar, aplique lentamente, pare ao começar a drenar e registre apenas o volume colocado.","Confirmar condição do vaso",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        try{CareEngine.Start(Active,last,store.Settings.Weather,store.Settings.Calibration,samples,DateTime.UtcNow);Save();UpdateCareCards();Status("Sessão de rega iniciada. Registre as porções que realmente aplicar.");}catch(Exception e){ShowError(e);}
    }
    void PortionDialog() {
        if(!RealCareAllowed())return;
        if(Active.Care==null||Active.Care.Session==null){MessageBox.Show(this,"Inicie a rega guiada antes de aplicar e registrar a primeira porção.");return;}
        using(var d=Dialog("Registrar água aplicada",570,310)) {
            var hint=Theme.Label("Informe apenas o que já colocou neste vaso.\nA porção inicial de 30 mL é experimental, não a dose total.\nSe começou a drenar, pare de colocar água e finalize a rega.",10,Theme.Muted);hint.Location=new Point(22,20);
            var label=Theme.Label("Porção aplicada (mL)");label.Location=new Point(22,117);
            var ml=new NumericUpDown{Location=new Point(240,112),Width=120,Minimum=1,Maximum=10000,Value=CareEngine.FirstPortionMl,Font=Theme.F(12)};
            var save=Theme.Button("Registrar porção",(s,e)=>{try{CareEngine.AddPortion(Active,(int)ml.Value,DateTime.UtcNow);Save();d.Close();UpdateCareCards();}catch(Exception ex){ShowError(ex);}},true);save.Location=new Point(22,183);
            d.Controls.AddRange(new Control[]{hint,label,ml,save});d.ShowDialog(this);
        }
    }
    void FinishWatering() {
        if(!RealCareAllowed())return;
        var session=Active.Care==null?null:Active.Care.Session;
        if(session==null){MessageBox.Show(this,"Nenhuma sessão de rega em andamento.");return;}
        if(MessageBox.Show(this,"Você registrou "+session.TotalMl+" mL. A água começou a sair pelos furos?\n\nSó confirme se observou a drenagem. Não acrescente água apenas para fazer o índice subir. Esvazie o pratinho.","Concluir registro desta rega",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        try{CareEngine.Finish(Active,last,store.Settings.Calibration,true,DateTime.UtcNow);WateringSaved();Status("Rega registrada. O app aprendeu a quantidade observada neste vaso.");}catch(Exception e){ShowError(e);}
    }
    void CancelWatering() {
        if(!RealCareAllowed())return;
        if(Active.Care==null||Active.Care.Session==null)return;
        bool applied=Active.Care.Session.TotalMl>0;CareEngine.Cancel(Active,DateTime.UtcNow);try{if(applied)WateringSaved();else Save();UpdateCareCards();Status("Sessão encerrada. Volume informado preservado, sem usar no aprendizado.");}catch(Exception e){ShowError(e);}
    }
    void LightReferenceDialog() {
        if(!RealCareAllowed())return;
        using(var d=Dialog("Referência de iluminação da planta",720,470)) {
            var info=Theme.Label("Coloque o LDR junto da planta, sem a sombra da sua mão ou dos fios.\nEscolha uma iluminação que você confirmou ser apropriada para a espécie.\n"+SpeciesCare.LightGuide(Active)+"\nO app fará uma comparação com esse ponto, não uma medição em lux.",10,Theme.Muted);info.AutoSize=false;info.Size=new Size(656,160);info.Location=new Point(22,20);
            var label=Theme.Label("Descreva o local");label.Location=new Point(22,190);
            var note=Theme.Input("Local da "+Active.Name,420);note.Location=new Point(22,222);
            var confirmed=new CheckBox{Text="Conferi que este local oferece iluminação adequada à planta",AutoSize=true,Location=new Point(22,270)};
            var save=Theme.Button("Usar luz atual como referência",(s,e)=> {
                if(!confirmed.Checked){MessageBox.Show(d,"Confira a iluminação do local antes de usá-lo como referência.");return;}
                try{CareEngine.RegisterLight(Active,last,store.Settings.Weather,store.Settings.Calibration,note.Text,DateTime.UtcNow);Save();d.Close();Navigate("Cuidados e rega");}catch(Exception ex){ShowError(ex);}
            },true);save.Location=new Point(22,330);
            d.Controls.AddRange(new Control[]{info,label,note,confirmed,save});d.ShowDialog(this);
        }
    }
    void DryTriggerDialog() {
        if(!RealCareAllowed())return;
        var p=Active;
        if(!CareEngine.Supported(p)){MessageBox.Show(this,"Para esta espécie, cadastre uma faixa com referência em Minha planta.");return;}
        if(p.Soil!=null&&p.Soil.Enabled){MessageBox.Show(this,"A faixa de solo cadastrada em Minha planta tem prioridade. Edite-a lá para mudar o aviso.");return;}
        using(var d=Dialog("Aviso de solo seco da planta",620,330)) {
            var info=Theme.Label("O aviso atual usa "+CareEngine.Trigger(p).ToString("0")+"% do índice relativo local.\nEsse número é uma aproximação local, não uma umidade ideal da espécie.\nAjuste somente após comparar a leitura com a secagem real do substrato.",10,Theme.Muted);info.Location=new Point(22,20);
            var label=Theme.Label("Avisar ao atingir ou ficar abaixo de (%)");label.Location=new Point(22,127);
            var value=new NumericUpDown{Location=new Point(360,122),Width=100,Minimum=0,Maximum=60,Value=(decimal)CareEngine.Trigger(p),Font=Theme.F(12)};
            var save=Theme.Button("Salvar aviso",(s,e)=>{p.Care.NearDryIndex=(double)value.Value;p.Care.DryTriggerCustom=true;samples.Clear();Save();d.Close();UpdateCareCards();},true);save.Location=new Point(22,205);
            var automatic=Theme.Button("Usar preset da espécie",(s,e)=>{p.Care.NearDryIndex=10;p.Care.DryTriggerCustom=false;samples.Clear();Save();d.Close();Navigate("Cuidados e rega");});automatic.Location=new Point(250,205);
            d.Controls.AddRange(new Control[]{info,label,value,save,automatic});d.ShowDialog(this);
        }
    }
    void ResetWaterLearning() {
        if(!RealCareAllowed())return;
        if(Active.Care.Session!=null){MessageBox.Show(this,"Conclua ou encerre a sessão atual antes de reiniciar o aprendizado.");return;}
        if(MessageBox.Show(this,"O app deixará de usar as quantidades das regas anteriores na estimativa. Os registros continuarão guardados. Recalibre o solo ao trocar o substrato.","Reiniciar aprendizado para este vaso",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
        Active.Care.LearningResetUtc=DateTime.UtcNow;Save();UpdateCareCards();
    }
}
}
