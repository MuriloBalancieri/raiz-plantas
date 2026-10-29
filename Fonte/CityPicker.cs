using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Raiz {
public class CityPicker:TableLayoutPanel {
    readonly TextBox query;readonly Button search,apply;readonly ListBox results;readonly Label feedback;readonly ReadableCard selected;
    readonly Func<string,Task<List<Location>>> find;readonly Func<Location,Task> choose;bool busy;
    public CityPicker(Location current,Func<string,Task<List<Location>>> find,Func<Location,Task> choose){
        this.find=find;this.choose=choose;ColumnCount=1;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Dock=DockStyle.Top;Margin=new Padding(0,4,0,16);ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var prompt=Theme.Label("Buscar outra cidade",13,null,true);prompt.Margin=new Padding(0,8,0,12);Row(prompt,SizeType.AutoSize,0);
        query=Theme.Input(current==null?"":current.Name,310);query.Name="cityQuery";query.AccessibleName="Nome da cidade";
        search=Theme.Button("Buscar cidade",async(s,e)=>await Search(),true);search.Name="citySearch";
        var actions=new ActionRow();actions.Controls.AddRange(new Control[]{query,search});Row(actions,SizeType.AutoSize,0);
        feedback=Theme.Label("Digite apenas o nome da cidade. Confira o estado e o país nos resultados.",10,Theme.Muted);feedback.AutoSize=false;feedback.Height=54;feedback.Dock=DockStyle.Top;feedback.Name="cityFeedback";Row(feedback,SizeType.Absolute,60);
        results=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=58,Font=Theme.F(11),BorderStyle=BorderStyle.FixedSingle,BackColor=Color.White,Name="cityResults",AccessibleName="Cidades encontradas, com estado e país",Margin=new Padding(0,0,0,16)};
        results.DrawItem+=DrawCity;results.SelectedIndexChanged+=(s,e)=>SelectCity();Row(results,SizeType.Absolute,200);
        selected=new ReadableCard{Visible=false};Row(selected,SizeType.AutoSize,0);
        apply=Theme.Button("Usar esta cidade",async(s,e)=>await Apply(),true);apply.Enabled=false;apply.Name="cityApply";
        var saveRow=new ActionRow();saveRow.Controls.Add(apply);Row(saveRow,SizeType.AutoSize,0);
        query.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Search();}};
        results.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Apply();}};
    }
    void Row(Control c,SizeType type,float height){int r=RowCount++;RowStyles.Add(new RowStyle(type,height));if(!(c is ActionRow)&&!(c is ReadableCard))c.Dock=DockStyle.Fill;Controls.Add(c,0,r);}
    void SelectCity(){var city=results.SelectedItem as Location;apply.Enabled=!busy&&city!=null;selected.Visible=city!=null;if(city!=null){selected.SetText("Você selecionou: "+city.Name,city.Region+" · "+city.Country+"\nClique em Usar esta cidade para confirmar a troca.");}PerformLayout();}
    public async Task Search(){
        if(busy||IsDisposed)return;string text=query.Text.Trim();
        if(text.Length<2){feedback.Text="Digite pelo menos duas letras do nome da cidade.";query.Focus();return;}
        busy=true;query.Enabled=false;search.Enabled=false;apply.Enabled=false;results.Enabled=false;results.Items.Clear();selected.Visible=false;feedback.Text="Buscando cidades…";
        try{var cities=await find(text);if(IsDisposed)return;results.Items.AddRange((cities??new List<Location>()).Where(x=>x!=null).Cast<object>().ToArray());results.SelectedIndex=-1;
            feedback.Text=results.Items.Count==0?"Nenhuma cidade encontrada. Tente apenas o nome, sem sigla do estado, ou confira a escrita.":results.Items.Count+" resultado(s). Selecione a cidade pelo estado e país; depois confirme abaixo.";
        }catch(Exception ex){if(!IsDisposed)feedback.Text=ex is ApiException?ex.Message:"Não foi possível buscar cidades. Confira a internet e tente novamente.";}
        finally{if(!IsDisposed){busy=false;query.Enabled=true;search.Enabled=true;results.Enabled=true;SelectCity();}}
    }
    public async Task Apply(){
        var city=results.SelectedItem as Location;if(busy||city==null||IsDisposed)return;
        busy=true;apply.Enabled=false;search.Enabled=false;query.Enabled=false;results.Enabled=false;feedback.Text="Salvando "+city.Name+" e atualizando o clima…";
        try{await choose(city);if(!IsDisposed)feedback.Text="Cidade salva: "+city.Name+" · "+city.Region+" · "+city.Country+".";}
        catch(Exception ex){if(!IsDisposed)feedback.Text=ex is ApiException?ex.Message:"Não foi possível salvar a cidade. Sua localização anterior foi preservada.";}
        finally{if(!IsDisposed){busy=false;apply.Enabled=true;search.Enabled=true;query.Enabled=true;results.Enabled=true;}}
    }
    void DrawCity(object sender,DrawItemEventArgs e){
        e.DrawBackground();if(e.Index<0||e.Index>=results.Items.Count)return;var city=(Location)results.Items[e.Index];bool active=(e.State&DrawItemState.Selected)!=0;
        using(var brush=new SolidBrush(active?Theme.Green:Color.White))e.Graphics.FillRectangle(brush,e.Bounds);
        Color ink=active?Color.White:Theme.Ink,muted=active?Color.White:Theme.Muted;
        using(var font=Theme.F(11,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,city.Name,font,new Rectangle(e.Bounds.X+14,e.Bounds.Y+7,Math.Max(1,e.Bounds.Width-28),24),ink,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        using(var font=Theme.F(9))TextRenderer.DrawText(e.Graphics,city.Region+" · "+city.Country,font,new Rectangle(e.Bounds.X+14,e.Bounds.Y+32,Math.Max(1,e.Bounds.Width-28),20),muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
        e.DrawFocusRectangle();
    }
}
public partial class MainForm {
    void WeatherPage(TableLayoutPanel stack){
        Heading(stack,"Clima da região","Escolha a cidade usada nas consultas de temperatura e umidade externas.");
        var loc=store.Settings.Location;var w=store.Settings.Weather;
        string current=loc==null?"Nenhuma cidade selecionada":loc.Name+" · "+loc.Region+" · "+loc.Country;
        string detail=w==null?"O clima será consultado para esta localização.":"Temperatura: "+(w.Temperature.HasValue?w.Temperature.Value.ToString("0.#")+" °C":"indisponível")+"    ·    Umidade: "+(w.Humidity.HasValue?w.Humidity.Value.ToString("0")+"%":"indisponível")+"\n"+(w.Fresh(DateTime.UtcNow)?"Dados atuais":"Última consulta antiga")+" · "+w.ModelTime.Replace("T"," ");
        Note(stack,"Localização atual: "+current,detail);
        if(weatherError!="")Note(stack,"Clima indisponível",weatherError);
        var picker=new CityPicker(loc,Api.Cities,async city=>{
            if(weatherBusy)throw new ApiException("Aguarde a atualização do clima terminar e confirme novamente.");
            var before=store.Settings.Location;var previousWeather=store.Settings.Weather;
            locationVersion++;store.Settings.Location=city;store.Settings.Weather=null;
            try{store.SaveConfiguration();}catch{store.Settings.Location=before;store.Settings.Weather=previousWeather;locationVersion++;throw new ApiException("Não foi possível salvar. A localização anterior foi preservada; confira a pasta de dados.");}
            weatherError="";await RefreshWeather();if(!closing&&page=="Clima da região")Navigate(page);
        });Add(stack,picker,400);
        Add(stack,Buttons(Theme.Button("Atualizar clima atual",async(s,e)=>{await RefreshWeather();if(!closing&&page=="Clima da região")Navigate(page);}),Theme.Button("Voltar para Hoje",(s,e)=>Navigate("Visão geral"))),56);
        Disclosure(stack,"Sobre os dados de clima",details=>{
            Note(details,"Clima externo","Temperatura e umidade são estimativas da região, não medições dentro de casa ou no vaso. Trocar a cidade não altera a calibração dos sensores.");
            Add(details,Buttons(Theme.Button("Fonte: Open-Meteo",(s,e)=>OpenUrl("https://open-meteo.com/"))),56);
        });
    }
}
}
