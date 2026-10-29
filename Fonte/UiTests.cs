using System;using System.Drawing;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Windows.Forms;using Raiz;
using System.Threading.Tasks;
public static class UiTests {
    class ProbeCard:ReadableCard {public Rectangle HeadingBounds{get{return title.Bounds;}}public Rectangle BodyBounds{get{return body.Bounds;}}}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control c in root.Controls)foreach(var child in All(c))yield return child;}
    static void Layout(Control c){c.PerformLayout();foreach(Control child in c.Controls)Layout(child);}
    public static void Run(Action<bool,string> check,string folder){
        using(var row=new ActionRow{Width=410}){
            row.Controls.Add(Theme.Button("Iniciar rega guiada",(s,e)=>{}));row.Controls.Add(Theme.Button("Registrar quantidade aplicada",(s,e)=>{}));row.Controls.Add(Theme.Button("Finalizar ao começar a drenar",(s,e)=>{}));row.PerformLayout();
            check(row.Height>100,"fileira cresce quando botoes quebram em varias linhas");
            check(row.Controls.Cast<Control>().All(c=>c.Bottom+c.Margin.Bottom<=row.Height),"nenhum botao quebrado fica cortado na altura da fileira");
            check(row.Controls.Cast<Control>().All(c=>c.Width>=105&&c.Height>=44),"acoes principais tem area de clique de pelo menos 44 pixels");
        }
        using(var card=new ProbeCard{Width=320}){
            card.SetText("Aviso de luminosidade com titulo longo que precisa quebrar",string.Join(" ",Enumerable.Repeat("Confira a iluminação e a posição da planta.",8)));int narrow=card.Height;
            check(card.BodyBounds.Top>card.HeadingBounds.Bottom&&card.BodyBounds.Bottom<card.Height,"titulo e texto longos ocupam areas separadas sem sobrepor");
            card.Width=900;card.Reflow();check(card.Height<narrow,"cartao recalcula altura conforme a largura disponivel");
        }
        string isolated=Path.Combine(folder,"interface");var store=new Store(isolated);var p=new Plant{Name="Jade de teste",Scientific="Crassula ovata"};store.Settings.Plants.Add(p);store.Settings.ActiveId=p.LocalId;store.Save();
        using(var form=new MainForm(isolated,false)){
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;((Timer)typeof(MainForm).GetField("timer",flags).GetValue(form)).Stop();
            typeof(Control).GetMethod("SetState",flags).Invoke(form,new object[]{2,true});form.CreateControl();
            foreach(int width in new[]{1080,1240}){
                form.Size=new Size(width,820);
                foreach(string page in new[]{"Visão geral","Cuidados e rega","Telegram e avisos","Minha planta","Arduino e calibração","Configurações","Histórico","Clima da região"}){
                    typeof(MainForm).GetMethod("Navigate",flags).Invoke(form,new object[]{page});Layout(form);Layout(form);
                    var rows=All(form).OfType<ActionRow>().Where(r=>r.Visible).ToList();
                    check(rows.All(r=>r.Controls.Cast<Control>().Where(c=>c.Visible).All(c=>c.Bottom<=r.Height&&c.Right<=r.ClientSize.Width)),"acoes visiveis cabem na tela "+page+" em "+width+"px");
                }
            }
            typeof(MainForm).GetMethod("Navigate",flags).Invoke(form,new object[]{"Cuidados e rega"});Layout(form);
            var expand=All(form).OfType<Button>().First(b=>b.Text.Contains("Rega guiada"));expand.PerformClick();Layout(form);
            check(All(form).OfType<Button>().Any(b=>b.Text=="Registrar porção"&&b.Visible),"rega guiada abre os controles secundarios quando solicitada");
            expand.PerformClick();Layout(form);check(!All(form).OfType<Button>().Any(b=>b.Text=="Registrar porção"&&b.Visible),"secoes recolhidas retiram controles secundarios da tela");
            check(All(form).OfType<PlotterPanel>().Any(c=>c.Visible),"plotter permanece visivel ao recolher configuracoes");
            typeof(MainForm).GetMethod("Navigate",flags).Invoke(form,new object[]{"Arduino e calibração"});Layout(form);
            check(!All(form).OfType<CheckBox>().Any(c=>c.Text.Contains("DHT")),"sensores nao exigem confirmacao de remocao do DHT");
        }
        CityPickerTests(check);
    }
    static void CityPickerTests(Action<bool,string> check){
        int calls=0,saves=0;Location chosen=null;
        var cities=new List<Location>{new Location{Name="Santa Maria",Region="Rio Grande do Sul",Country="Brasil",Latitude=-29},new Location{Name="Santa Maria",Region="California",Country="Estados Unidos",Latitude=34}};
        using(var picker=new CityPicker(new Location{Name="Guarulhos"},q=>{
            calls++;if(q=="erro"){var failure=new TaskCompletionSource<List<Location>>();failure.SetException(new ApiException("Consulta indisponível para teste."));return failure.Task;}
            return Task.FromResult(q=="vazio"?new List<Location>():cities);
        },city=>{chosen=city;saves++;return Task.FromResult(true);})){picker.Width=760;Layout(picker);
            var query=All(picker).OfType<TextBox>().Single(c=>c.Name=="cityQuery");var results=All(picker).OfType<ListBox>().Single(c=>c.Name=="cityResults");var apply=All(picker).OfType<Button>().Single(c=>c.Name=="cityApply");var feedback=All(picker).OfType<Label>().Single(c=>c.Name=="cityFeedback");
            check(query.Text=="Guarulhos","busca de cidade inicia com nome atual sem sigla de estado");
            query.Text="";picker.Search().GetAwaiter().GetResult();check(calls==0&&!apply.Enabled,"busca vazia nao consulta API nem habilita troca");
            query.Text="Santa Maria";picker.Search().GetAwaiter().GetResult();check(results.Items.Count==2&&results.SelectedIndex==-1&&!apply.Enabled,"cidades homonimas ficam listadas sem selecao automatica");
            check(((Location)results.Items[0]).Region!=((Location)results.Items[1]).Region&&((Location)results.Items[1]).Country=="Estados Unidos","resultados preservam estado e pais para distinguir cidades");
            results.SelectedIndex=1;Layout(picker);check(apply.Enabled&&saves==0,"selecionar cidade exige confirmacao antes de salvar");
            picker.Apply().GetAwaiter().GetResult();check(saves==1&&chosen==cities[1]&&chosen.Latitude==34,"confirmacao usa a cidade e coordenadas escolhidas");
            query.Text="vazio";picker.Search().GetAwaiter().GetResult();check(results.Items.Count==0&&!apply.Enabled&&feedback.Text.Contains("Nenhuma"),"busca sem resultado limpa selecao antiga e mostra orientacao");
            query.Text="erro";picker.Search().GetAwaiter().GetResult();check(feedback.Text.Contains("indisponível")&&query.Enabled&&!apply.Enabled,"erro de busca aparece na tela e permite tentar novamente");
        }
    }
}
