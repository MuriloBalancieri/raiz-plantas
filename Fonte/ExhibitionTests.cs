using System;using System.IO;using System.Linq;using System.Drawing;using System.Collections.Generic;using System.Reflection;using System.Threading.Tasks;using System.Windows.Forms;using Raiz;
public static class ExhibitionTests {
    static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control c in root.Controls)foreach(var child in All(c))yield return child;}
    static void Layout(Control c){c.PerformLayout();foreach(Control child in c.Controls)Layout(child);}
    static object Field(object o,string name){return o.GetType().GetField(name,F).GetValue(o);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,F).Invoke(o,args);}
    public static void Run(Action<bool,string> check,string folder){
        using(var empty=new MainForm(Path.Combine(folder,"mostra-sem-cadastro"),false)){((Timer)Field(empty,"timer")).Stop();empty.OpenPresentation();check((string)Field(empty,"page")=="Apresentação","entrada direta no roteiro funciona sem planta ou chave cadastrada");}
        var now=DateTime.UtcNow;var c=new Calibration{Dry=392,Wet=281,Dark=22,Bright=316};var s=new Sample{Utc=now,Soil=281,Light=316};
        var live=Exhibition.Reading(true,0,s,c,false,now);check(!live.Simulated&&live.Soil==100&&live.Light==100,"apresentacao real usa calibracao da montagem");
        check(Exhibition.Reading(true,0,s,c,false,now.AddSeconds(13)).Soil==null,"apresentacao nao exibe leitura antiga como atual");
        check(Exhibition.Reading(true,0,s,c,true,now).Soil==null,"modo geral ficticio nao se passa por sensor real");
        check(Exhibition.Reading(true,0,s,new Calibration(),false,now).Soil==null,"sem calibracao o roteiro nao inventa percentuais");
        check(Exhibition.Reading(false,1,null,c,false,now).Soil==8&&Exhibition.Reading(false,1,null,c,false,now).Simulated,"solo seco e explicitamente ilustrativo");
        check(Exhibition.Reading(false,2,null,c,false,now).Light==15,"cenario de sombra altera apenas leitura ilustrativa");
        check(Exhibition.Reading(false,3,null,c,false,now).Soil==null,"cenario sem sinal nao vira zero de umidade");
        check(Exhibition.Explain(2,Exhibition.Reading(false,0,null,c,false,now),false).Contains("não é uma meta"),"roteiro explica que 100 por cento nao e meta de rega");
        check(Exhibition.Explain(4,live,true).Contains("Nenhuma rega real"),"confirmacao didatica informa ausencia de envio e registro");
        string path=Path.Combine(folder,"mostra");var store=new Store(path);var p=new Plant{Name="Jade de teste",Scientific="Crassula ovata"};store.Settings.Plants.Add(p);store.Settings.ActiveId=p.LocalId;store.Settings.Calibration=c;store.Settings.Telegram.Enabled=true;store.Settings.Telegram.PollEnabled=true;store.Save();
        var before=File.ReadAllBytes(Path.Combine(path,"configuracao.json"));
        using(var form=new MainForm(path,false)){
            ((Timer)Field(form,"timer")).Stop();typeof(Control).GetMethod("SetState",F).Invoke(form,new object[]{2,true});form.CreateControl();var memoryStore=(Store)Field(form,"store");string beforeMemory=Json.Write(memoryStore.Settings);
            foreach(int width in new[]{1080,1280}){
                form.Size=new Size(width,800);Call(form,"Navigate","Apresentação");
                for(int step=0;step<5;step++){
                    typeof(MainForm).GetField("exhibitStep",F).SetValue(form,step);Call(form,"Navigate","Apresentação");Layout(form);Layout(form);
                    check(All(form).OfType<ActionRow>().Where(row=>row.Visible).All(row=>row.Controls.Cast<Control>().Where(x=>x.Visible).All(x=>x.Right<=row.Width&&x.Bottom<=row.Height)),"controles didaticos cabem na etapa "+(step+1)+" em "+width+"px");
                    check(All(form).OfType<Button>().Any(b=>b.Text=="Mostrar histórico da planta"&&b.Visible),"historico pode ser aberto diretamente na etapa "+(step+1));
                    check(All(form).OfType<LearningJourney>().Any(x=>x.Visible)==(step==0),"fluxo fica na abertura e libera espaco nas demais etapas "+(step+1));
                }
            }
            check(!((Control)Field(form,"dockPlotter")).Visible,"apresentacao libera espaco do plotter sem parar coleta");
            ((Task)Call(form,"PollTelegram")).GetAwaiter().GetResult();((Task)Call(form,"PollWhatsApp")).GetAwaiter().GetResult();check(memoryStore.Settings.Notifications.Count==0,"avisos automaticos nao geram eventos na apresentacao");
            Call(form,"ChooseExhibitScenario",1);Call(form,"ChooseExhibitScenario",2);Call(form,"ChooseExhibitScenario",3);Call(form,"ChooseExhibitScenario",0);
            All(form).OfType<Button>().Single(b=>b.Text=="Simular Já reguei").PerformClick();check((bool)Field(form,"exhibitConfirmed"),"confirmacao ilustrativa responde ao clique");
            check(memoryStore.Settings.Plants[0].Care.Waterings.Count==0&&!File.Exists(memoryStore.HistoryPath),"exemplos e confirmacao nao gravam regas nem medicoes");
            check(Json.Write(memoryStore.Settings)==beforeMemory,"roteiro nao modifica configuracoes ou calibrações do usuario");
            check(File.ReadAllBytes(Path.Combine(path,"configuracao.json")).SequenceEqual(before),"arquivo original permanece intacto depois do roteiro");
            Call(form,"MoveExhibit",1);check((int)Field(form,"exhibitStep")==4,"navegacao nao avanca alem da ultima etapa");
            Call(form,"SetExhibitFullscreen",true);check(form.FormBorderStyle==FormBorderStyle.None,"tela cheia remove moldura para projetor");
            Call(form,"Navigate","Visão geral");Layout(form);check(!(bool)Field(form,"exhibitFullscreen")&&((Control)Field(form,"dockPlotter")).Visible,"sair restaura a janela e o plotter normais");
            check(memoryStore.Settings.Telegram.Enabled&&memoryStore.Settings.Telegram.PollEnabled,"pausa da apresentacao preserva preferencia original dos avisos");
        }
    }
}
