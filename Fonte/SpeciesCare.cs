using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Raiz {
public class SpeciesNeeds {
    public string WateringCode {get;set;}
    public string[] LightCodes {get;set;}
    public int? IntervalMinDays {get;set;} public int? IntervalMaxDays {get;set;}
    public DateTime FetchedUtc {get;set;}
    public SpeciesNeeds(){WateringCode="";LightCodes=new string[0];}
}
public static class SpeciesCare {
    public static string WaterCode(string value){switch((value??"").Trim().ToLowerInvariant()){
        case "minimum":case "pouca rega":return "minimum";case "average":case "rega moderada":return "average";
        case "frequent":case "rega frequente":return "frequent";case "none":case "sem indicação de rega":return "none";default:return "";}}
    public static string LightCode(string value){switch((value??"").Trim().ToLowerInvariant().Replace('_',' ')){
        case "full sun":case "sol pleno":return "full sun";case "part shade":case "partial shade":case "meia-sombra":return "part shade";
        case "full shade":case "sombra":return "full shade";case "sun-part shade":case "sol / meia-sombra":return "sun-part shade";default:return "";}}
    public static SpeciesNeeds Parse(Dictionary<string,object> data,DateTime now){
        var n=new SpeciesNeeds{WateringCode=WaterCode(Json.Str(Json.Get(data,"watering"))),LightCodes=Json.List(Json.Get(data,"sunlight")).Select(x=>LightCode(Json.Str(x))).Where(x=>x!="").Distinct().ToArray(),FetchedUtc=now};
        var b=Json.Map(Json.Get(data,"watering_general_benchmark"));string unit=Json.Str(Json.Get(b,"unit")).Trim().ToLowerInvariant();
        var match=Regex.Match(Json.Str(Json.Get(b,"value")).Trim(),@"^([0-9]{1,3})(?:\s*[-–]\s*([0-9]{1,3}))?$");
        if((unit=="day"||unit=="days")&&match.Success){int lo=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),hi=match.Groups[2].Success?int.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture):lo;if(lo>0&&hi>=lo&&hi<=365){n.IntervalMinDays=lo;n.IntervalMaxDays=hi;}}
        return n;
    }
    public static SpeciesNeeds Needs(Plant p){
        if(p==null)return new SpeciesNeeds();if(p.Needs!=null){p.Needs.LightCodes=(p.Needs.LightCodes??new string[0]).Select(LightCode).Where(x=>x!="").Distinct().ToArray();p.Needs.WateringCode=WaterCode(p.Needs.WateringCode);return p.Needs;}
        if(p.ApiId<=0||!(p.Source??"").StartsWith("Perenual"))return new SpeciesNeeds();
        return new SpeciesNeeds{WateringCode=WaterCode(p.Watering),LightCodes=(p.Sunlight??"").Split(',').Select(LightCode).Where(x=>x!="").Distinct().ToArray()};
    }
    public static bool Enabled(Plant p){return p!=null&&p.Care!=null&&p.Care.AutomaticSpeciesCare;}
    public static bool HasWater(Plant p){return Enabled(p)&&!string.IsNullOrEmpty(Needs(p).WateringCode);}
    public static bool CustomTrigger(Plant p){return p!=null&&p.Care!=null&&(p.Care.DryTriggerCustom||Math.Abs(p.Care.NearDryIndex-10)>0.001);}
    public static double Preset(Plant p){switch(Needs(p).WateringCode){case "frequent":return 40;case "average":return 25;default:return 10;}}
    public static string LightKey(Plant p){return string.Join("|",Needs(p).LightCodes.OrderBy(x=>x));}
    public static string LightGuide(Plant p){var codes=Needs(p).LightCodes;if(codes.Length==0)return "Exposição não informada pela API. Valide o local antes de registrar a referência.";
        var text=codes.Select(x=>x=="full sun"?"sol pleno":x=="full shade"?"sombra":x=="part shade"?"meia-sombra":"sol ou meia-sombra");
        return "A espécie aceita: "+string.Join(" / ",text)+". Use um local compatível ao registrar a referência. O LDR não comprova lux ou horas de sol.";}
    public static string WaterGuide(Plant p){string code=Needs(p).WateringCode;switch(code){case "minimum":return "API: pouca rega. Acompanhe a secagem e confira o substrato antes de adicionar água.";case "average":return "API: rega moderada. Acompanhe o substrato e evite repetir água com solo ainda úmido.";case "frequent":return "API: rega frequente. Verifique a umidade com maior atenção; frequência não significa uma dose maior.";case "none":return "API: sem indicação de rega. A sugestão automática de água fica suspensa; confira os cuidados específicos da espécie.";default:return "A API não informou uma categoria de rega reconhecida. Nenhuma dose ideal foi presumida.";}}
    public static string Interval(Plant p,DateTime now){var n=Needs(p);if(!n.IntervalMinDays.HasValue)return "Intervalo entre regas não informado pela API.";
        string text="Referência geral da API: "+n.IntervalMinDays+"–"+n.IntervalMaxDays+" dias entre regas.";
        var last=p.Care.Waterings.OrderByDescending(x=>x.FinishedUtc).FirstOrDefault();if(last!=null){double days=(now-last.FinishedUtc).TotalDays;text+=" Última rega há "+Math.Max(0,days).ToString("0.#")+" dia(s).";if(days>=n.IntervalMaxDays)text+=" Confira o substrato; a passagem do tempo sozinha não autoriza regar.";}
        return text;
    }
    public static string Summary(Plant p,DateTime now){if(p==null)return "Escolha uma espécie.";
        string mode=!Enabled(p)?"Adaptação por espécie pausada.":p.Soil!=null&&p.Soil.Enabled?"Faixa manual de solo tem prioridade.":CustomTrigger(p)?"Gatilho local ajustado por você tem prioridade.":HasWater(p)&&Needs(p).WateringCode!="none"?"Aviso de solo em "+Preset(p)+"% do índice local: preset experimental do app, não umidade ideal fornecida pela API.":"Sem gatilho automático da API para esta planta.";
        return WaterGuide(p)+" "+Interval(p,now)+"\n"+LightGuide(p)+"\n"+mode+" A API não informa dose total em mL nem limiar em lux nesse cadastro.";
    }
    public static void Merge(Plant target,Plant fresh){
        if(target.ApiId<=0||target.ApiId!=fresh.ApiId)throw new ApiException("A resposta da API não corresponde à espécie selecionada.");
        string oldKey=LightKey(target);target.Needs=fresh.Needs;target.Watering=fresh.Watering;target.Sunlight=fresh.Sunlight;target.SoilAdvice=fresh.SoilAdvice;target.Description=fresh.Description;target.Source=fresh.Source;target.ImportedUtc=fresh.ImportedUtc;
        if(oldKey!=LightKey(target)){target.Care.LightReferenceRaw=null;target.Care.LightReferenceUtc=null;target.Care.LightSpeciesKey=null;}
    }
}
public partial class MainForm {
    bool speciesBusy;readonly Dictionary<string,DateTime> nextSpeciesAttempt=new Dictionary<string,DateTime>();Label speciesCareLabel;
    async Task RefreshSpecies(bool force=false){
        var p=Active;if(closing||demo||speciesBusy||p==null||p.ApiId<=0||p.Care.Session!=null)return;
        var key=store.LoadKey();if(string.IsNullOrWhiteSpace(key)){if(force)Status("Cadastre sua chave Perenual em Configurações.");return;}
        DateTime retry;nextSpeciesAttempt.TryGetValue(p.LocalId,out retry);
        if(!force&&(DateTime.UtcNow<retry||(p.Needs!=null&&DateTime.UtcNow-p.Needs.FetchedUtc<TimeSpan.FromHours(24))))return;
        speciesBusy=true;nextSpeciesAttempt[p.LocalId]=DateTime.UtcNow.AddMinutes(30);
        try{var fresh=await Api.Details(key,p.ApiId);if(closing)return;var target=store.Settings.Plants.Find(x=>x.LocalId==p.LocalId);if(target==null||target.ApiId!=fresh.ApiId||target.Care.Session!=null)return;
            SpeciesCare.Merge(target,fresh);if(Active==target)samples.Clear();store.Save();Status("Orientações da espécie atualizadas pela Perenual.");if(Active==target&&(page=="Cuidados e rega"||page=="Minha planta"))Navigate(page);
        }catch(Exception ex){Status("Perenual: "+Friendly(ex)+" Orientações anteriores preservadas.");}
        finally{speciesBusy=false;}
    }
    void SpeciesControls(TableLayoutPanel stack){
        if(Active==null)return;
        var card=Theme.Card();speciesCareLabel=Theme.Label(SpeciesCare.Summary(Active,DateTime.UtcNow),10,Theme.Muted);speciesCareLabel.AutoSize=false;speciesCareLabel.Dock=DockStyle.Fill;speciesCareLabel.Padding=new Padding(18);card.Controls.Add(speciesCareLabel);Add(stack,card,230);
        var auto=new CheckBox{Text="Adaptar cuidados à espécie pela API",Checked=SpeciesCare.Enabled(Active),AutoSize=true};
        auto.CheckedChanged+=(s,e)=>{Active.Care.AutomaticSpeciesCare=auto.Checked;samples.Clear();Save();UpdateCareCards();speciesCareLabel.Text=SpeciesCare.Summary(Active,DateTime.UtcNow);};
        Add(stack,Buttons(auto,Theme.Button("Atualizar cuidados pela API",async(s,e)=>await RefreshSpecies(true))),50);
    }
}
}
