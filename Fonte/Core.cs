using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Raiz {
public static class Json {
    public static readonly JavaScriptSerializer Codec = new JavaScriptSerializer { MaxJsonLength = 4000000 };
    public static string Write(object value) { lock (Codec) return Codec.Serialize(value); }
    public static T Read<T>(string value) { lock (Codec) return Codec.Deserialize<T>(value); }
    public static Dictionary<string,object> Map(object obj) { return obj as Dictionary<string,object> ?? new Dictionary<string,object>(); }
    public static object Get(Dictionary<string,object> d, string key) { object v; return d.TryGetValue(key,out v) ? v : null; }
    public static string Str(object obj) { return obj == null ? "" : Convert.ToString(obj, CultureInfo.InvariantCulture); }
    public static double? Num(object obj) {
        double n; return obj != null && double.TryParse(Str(obj), NumberStyles.Float, CultureInfo.InvariantCulture, out n) && !double.IsNaN(n) && !double.IsInfinity(n) ? (double?)n : null;
    }
    public static List<object> List(object obj) { var a = obj as IEnumerable; return a == null || obj is string ? new List<object>() : a.Cast<object>().ToList(); }
}

public class Location {
    public string Name {get;set;} public string Region {get;set;} public string Country {get;set;}
    public double Latitude {get;set;} public double Longitude {get;set;}
    public override string ToString() { return Name + " · " + Region + " · " + Country; }
}
public class Weather {
    public double? Temperature {get;set;} public double? Humidity {get;set;} public bool? IsDay {get;set;}
    public string ModelTime {get;set;} public string Timezone {get;set;} public DateTime FetchedUtc {get;set;} public DateTime? ModelUtc {get;set;}
    public bool Fresh(DateTime now) { return ModelUtc.HasValue && Math.Abs((now-ModelUtc.Value).TotalMinutes)<60 && now >= FetchedUtc.AddMinutes(-1) && now - FetchedUtc < TimeSpan.FromMinutes(45); }
}
public class Range {
    public double? Min {get;set;} public double? Max {get;set;}
    public bool Enabled {get {return Min.HasValue && Max.HasValue;}}
    public Range() {} public Range(double min,double max) {Min=min;Max=max;}
}
public class Calibration {
    public int? Dry {get;set;} public int? Wet {get;set;}
    public int? Dark {get;set;} public int? Bright {get;set;}
    public static double? Normalize(int raw, int? low, int? high) {
        if (!low.HasValue || !high.HasValue || Math.Abs(high.Value-low.Value)<30) return null;
        return Math.Max(0,Math.Min(100,100.0*(raw-low.Value)/(high.Value-low.Value)));
    }
    public double? Soil(int raw) { return Normalize(raw,Dry,Wet); }
    public double? Light(int raw) { return Normalize(raw,Dark,Bright); }
}
public class Plant {
    public SpeciesNeeds Needs {get;set;}
    public string LocalId {get;set;} public int ApiId {get;set;} public string Name {get;set;}
    public string Scientific {get;set;} public string Watering {get;set;} public string Sunlight {get;set;}
    public string SoilAdvice {get;set;} public string Description {get;set;} public string Source {get;set;}
    public DateTime? ImportedUtc {get;set;}
    public Range Soil {get;set;} public Range Light {get;set;} public Range Temperature {get;set;} public Range Humidity {get;set;}
    public string Reference {get;set;} public bool Outdoor {get;set;}
    public CarePlan Care {get;set;}
    public Plant() {LocalId=Guid.NewGuid().ToString("N"); Soil=new Range();Light=new Range();Temperature=new Range();Humidity=new Range();Source="Perfil manual";Care=new CarePlan();}
    public override string ToString() {return Name + (string.IsNullOrEmpty(Scientific) ? "" : " · " + Scientific);}
}
public class Settings {
    public List<Plant> Plants {get;set;} public string ActiveId {get;set;}
    public Location Location {get;set;} public Weather Weather {get;set;} public string Port {get;set;}
    public Calibration Calibration {get;set;}
    public WhatsAppOptions WhatsApp {get;set;} public List<NotificationEvent> Notifications {get;set;}
    public TelegramOptions Telegram {get;set;}
    public Settings() { Plants=new List<Plant>(); Calibration=new Calibration();Port="COM3";WhatsApp=new WhatsAppOptions();Notifications=new List<NotificationEvent>();Telegram=new TelegramOptions(); }
}
public class Store {
    public string Folder {get;private set;} public Settings Settings {get;private set;}
    public string LoadWarning {get;private set;}
    public Store(string folder) {
        Folder=folder;Directory.CreateDirectory(folder);Settings=new Settings();
        var path=Path.Combine(folder,"configuracao.json");
        if (File.Exists(path)) {
            try {Settings=Json.Read<Settings>(File.ReadAllText(path,Encoding.UTF8)) ?? new Settings();}
            catch {LoadWarning="Não foi possível ler a configuração. O arquivo foi preservado; confira antes de salvar.";}
        }
        if(Settings.Plants==null) Settings.Plants=new List<Plant>();
        if(Settings.Calibration==null) Settings.Calibration=new Calibration();
        if(Settings.WhatsApp==null)Settings.WhatsApp=new WhatsAppOptions();
        if(Settings.Telegram==null)Settings.Telegram=new TelegramOptions();
        if(Settings.Notifications==null)Settings.Notifications=new List<NotificationEvent>();
        foreach(var n in Settings.Notifications)if(n.State=="sending"){n.State="uncertain";n.Detail="O app foi encerrado durante o envio. Confira o celular; não haverá repetição automática.";}
        foreach(var p in Settings.Plants) {if(p.Care==null)p.Care=new CarePlan();if(p.Care.Waterings==null)p.Care.Waterings=new List<WateringRecord>();}
    }
    public void Save() {
        SaveConfiguration();SaveWateringLog();
    }
    public void SaveConfiguration() {
        var path=Path.Combine(Folder,"configuracao.json");var tmp=path+".tmp";
        File.WriteAllText(tmp,Json.Write(Settings),new UTF8Encoding(false));
        if(File.Exists(path)) File.Replace(tmp,path,path+".bak"); else File.Move(tmp,path);
    }
    public void SaveWhatsAppSecrets(WhatsAppSecrets secrets) {
        var path=Path.Combine(Folder,"whatsapp.protegido");var tmp=path+".tmp";
        File.WriteAllBytes(tmp,ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Write(secrets)),null,DataProtectionScope.CurrentUser));
        if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
    }
    public WhatsAppSecrets LoadWhatsAppSecrets() {
        try {return Json.Read<WhatsAppSecrets>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(Folder,"whatsapp.protegido")),null,DataProtectionScope.CurrentUser)))??new WhatsAppSecrets();} catch{return new WhatsAppSecrets();}
    }
    public void SaveTelegramSecrets(TelegramSecrets secrets) {
        var path=Path.Combine(Folder,"telegram.protegido");var tmp=path+".tmp";
        File.WriteAllBytes(tmp,ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Write(secrets)),null,DataProtectionScope.CurrentUser));
        if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
    }
    public TelegramSecrets LoadTelegramSecrets() {
        try{return Json.Read<TelegramSecrets>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(Folder,"telegram.protegido")),null,DataProtectionScope.CurrentUser)))??new TelegramSecrets();}catch{return new TelegramSecrets();}
    }
    public string WateringLogPath {get {return Path.Combine(Folder,"regas.csv");}}
    public void SaveWateringLog() {
        var lines=new List<string>{"id,utc,planta_id,planta,origem,volume_ml,drenagem_confirmada,indice_inicial,indice_final,evento"};
        foreach(var p in Settings.Plants)if(p.Care!=null&&p.Care.Waterings!=null)foreach(var r in p.Care.Waterings.OrderBy(x=>x.FinishedUtc)) {
            lines.Add(string.Join(",",new[]{r.RecordId,r.FinishedUtc.ToString("o"),p.LocalId,r.PlantName??p.Name,r.Origin??"Guiada",r.VolumeUnknown?"":r.TotalMl.ToString(CultureInfo.InvariantCulture),r.DrainageConfirmed?"sim":"nao_confirmada",r.InitialReadingUnknown?"":r.InitialIndex.ToString("0.0",CultureInfo.InvariantCulture),r.SensorReadingUnknown?"":r.FinalIndex.ToString("0.0",CultureInfo.InvariantCulture),r.EventKey??""}.Select(CsvCell)));
        }
        var tmp=WateringLogPath+".tmp";File.WriteAllText(tmp,string.Join("\r\n",lines)+"\r\n",new UTF8Encoding(true));
        if(File.Exists(WateringLogPath))File.Replace(tmp,WateringLogPath,null);else File.Move(tmp,WateringLogPath);
    }
    public void SaveKey(string key) {
        byte[] bytes=Encoding.UTF8.GetBytes(key.Trim());
        File.WriteAllBytes(Path.Combine(Folder,"perenual.protegido"),ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser));
    }
    public string LoadKey() {
        var path=Path.Combine(Folder,"perenual.protegido");
        if(!File.Exists(path)) return "";
        try {return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));} catch {return "";}
    }
    public static string CsvCell(string value) {
        value=value??"";
        if(value.Length>0 && "=+-@\t\r".Contains(value[0])) value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
    public string HistoryPath {get {return Path.Combine(Folder,"historico.csv");}}
    public void Append(Sample s,Plant plant,Weather weather,Location location,Calibration cal) {
        bool fresh=weather!=null && weather.Fresh(s.Utc);
        var fields=new [] {s.Utc.ToString("o"),plant==null?"":plant.LocalId,plant==null?"Sem planta selecionada":plant.Name,
            s.Soil.ToString(),s.Light.ToString(),Format(cal.Soil(s.Soil)),Format(cal.Light(s.Light)),
            fresh?Format(weather.Temperature):"",fresh?Format(weather.Humidity):"",weather==null?"":weather.ModelTime,
            location==null?"":location.ToString(),fresh?"atual":"indisponivel_ou_antigo","real"};
        if(!File.Exists(HistoryPath)) File.WriteAllText(HistoryPath,"utc,planta_id,planta,solo_bruto,luz_bruta,solo_indice_relativo,luz_indice_relativo,temperatura_regional_c,umidade_regional_pct,horario_modelo,localidade,estado_clima,origem\r\n",new UTF8Encoding(true));
        File.AppendAllText(HistoryPath,string.Join(",",fields.Select(CsvCell))+"\r\n",Encoding.UTF8);
    }
    static string Format(double? n) {return n.HasValue?n.Value.ToString("0.0",CultureInfo.InvariantCulture):"";}
}
public class Sample {
    public int Soil {get;set;} public int Light {get;set;} public DateTime Utc {get;set;}
    public bool Fresh(DateTime now) {return now>=Utc.AddSeconds(-1) && now-Utc<TimeSpan.FromSeconds(12);}
}
public class SensorParser {
    int? light; DateTime lightTime;
    static readonly Regex LightLine=new Regex(@"^Luz\s*\([^)]*\):\s*(\d+)\s*$",RegexOptions.IgnoreCase);
    static readonly Regex SoilLine=new Regex(@"^Solo\s*\([^)]*\):\s*(\d+)\s*$",RegexOptions.IgnoreCase);
    public Sample Parse(string text,DateTime now) {
        if(text==null || text.Length>1024) return null;
        text=text.Trim();
        if(text.StartsWith("{")) {
            try {
                var d=Json.Read<Dictionary<string,object>>(text);
                var a=Json.Num(Json.Get(d,"soil_raw"));var b=Json.Num(Json.Get(d,"light_raw"));
                if(!Valid(a)||!Valid(b)) return null;
                return new Sample {Soil=(int)a.Value,Light=(int)b.Value,Utc=now};
            } catch {return null;}
        }
        if(text.StartsWith("---")) {light=null;return null;}
        var m=LightLine.Match(text);
        if(m.Success) {int n;if(int.TryParse(m.Groups[1].Value,out n)&&Valid(n)){light=n;lightTime=now;}else light=null;return null;}
        m=SoilLine.Match(text);
        if(m.Success) {
            int soil;var l=light;light=null;
            if(l.HasValue && now-lightTime<TimeSpan.FromSeconds(4) && int.TryParse(m.Groups[1].Value,out soil)&&Valid(soil))
                return new Sample {Soil=soil,Light=l.Value,Utc=now};
        }
        return null;
    }
    static bool Valid(double? n) {return n.HasValue&&n>=0&&n<=1023&&n==Math.Floor(n.Value);}
}
public class Advice {
    public string Title {get;set;} public string Body {get;set;} public string Severity {get;set;}
    public Advice(string title,string body,string severity="info") {Title=title;Body=body;Severity=severity;}
}
public static class Assessment {
    public static List<Advice> Evaluate(Plant p,Sample s,Weather w,Calibration c,DateTime now) {
        var items=new List<Advice>();
        if(p==null) items.Add(new Advice("Escolha sua planta","Busque uma espécie ou crie um perfil manual para iniciar a comparação."));
        if(s==null || !s.Fresh(now)) items.Add(new Advice("Aguardando sensores","Conecte o Arduino; leituras antigas não geram recomendações.","pending"));
        else {
            var soil=c.Soil(s.Soil);var light=c.Light(s.Light);
            if(!soil.HasValue) items.Add(new Advice("Calibre o sensor de solo","Registre referências de substrato seco e úmido. O valor bruto não é uma porcentagem de água.","pending"));
            else if(p!=null) Compare(items,"Solo",soil,p.Soil,"índice relativo", "Confira o substrato antes de regar manualmente.");
            if(!light.HasValue) items.Add(new Advice("Calibre a luminosidade","Defina referências de escuro e claro no local do vaso. O LDR não mede lux nem luz para fotossíntese.","pending"));
            else if(w==null || !w.Fresh(now) || !w.IsDay.HasValue) items.Add(new Advice("Luz: contexto de horário pendente","Sem horário diurno atualizado, o app mostra a leitura e suspende a avaliação de pouca luz."));
            else if(!w.IsDay.Value) items.Add(new Advice("É noite na cidade selecionada","Avaliação de luminosidade pausada durante a noite."));
            else if(p!=null) Compare(items,"Luz",light,p.Light,"índice relativo","Uma leitura isolada não representa as horas de exposição diária.");
        }
        if(w==null || !w.Fresh(now)) items.Add(new Advice("Clima indisponível ou antigo","Atualize o clima. O último registro permanece visível, mas fica fora da comparação.","pending"));
        else if(p!=null) {
            if(p.Outdoor) {
                Compare(items,"Temperatura regional",w.Temperature,p.Temperature,"°C","Estimativa externa da região, não uma medição no vaso.");
                Compare(items,"Umidade regional",w.Humidity,p.Humidity,"%","Estimativa externa da região, não uma medição no vaso.");
            } else items.Add(new Advice("Planta em ambiente interno","Temperatura e umidade da cidade são contexto externo. Não permitem avaliar o ar dentro de casa."));
        }
        if(p!=null && !string.IsNullOrEmpty(p.Watering)) items.Add(new Advice("Rega indicada pela espécie",p.Watering+". Use essa orientação junto à observação do substrato e à calibração local."));
        if(p!=null && s!=null && s.Fresh(now) && p.Soil!=null && p.Soil.Enabled) {
            var soil=c.Soil(s.Soil);
            if(soil.HasValue && soil.Value<p.Soil.Min.Value && !string.IsNullOrEmpty(p.Watering)) {
                string context="A espécie tem a orientação: "+p.Watering.ToLowerInvariant()+". O solo está abaixo da faixa que você cadastrou.";
                if(p.Outdoor && w!=null && w.Fresh(now) && w.Temperature.HasValue && p.Temperature!=null && p.Temperature.Enabled && w.Temperature.Value>p.Temperature.Max.Value)
                    context+=" A temperatura regional também está acima da faixa cadastrada.";
                items.Insert(0,new Advice("Espécie + sensores + contexto",context+" Confira a umidade do substrato e a drenagem antes de decidir a rega manual.","attention"));
            }
        }
        return items;
    }
    static void Compare(List<Advice> a,string name,double? value,Range r,string unit,string note) {
        if(!value.HasValue) {a.Add(new Advice(name+": sem dado","A fonte não retornou essa medida.","pending"));return;}
        if(r==null || !r.Enabled) {a.Add(new Advice(name+": falta definir faixa","Cadastre uma faixa com referência em Minha planta. Nenhum limite foi presumido.","pending"));return;}
        string range=r.Min.Value.ToString("0.#")+" a "+r.Max.Value.ToString("0.#")+" "+unit;
        bool low=value<r.Min,high=value>r.Max;
        a.Add(new Advice(name+": "+(low?"abaixo da faixa":high?"acima da faixa":"na faixa cadastrada"),"Referência cadastrada: "+range+". "+note,low||high?"attention":"ok"));
    }
}
public class ApiException:Exception {public ApiException(string message):base(message){}}
public class Api {
    static readonly HttpClient Client=CreateClient();
    static HttpClient CreateClient() {
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var c=new HttpClient();c.Timeout=TimeSpan.FromSeconds(20);c.DefaultRequestHeaders.UserAgent.ParseAdd("RaizDesktop/1.0");return c;
    }
    public static async Task<Dictionary<string,object>> Get(string url) {
        try {
            using(var response=await Client.GetAsync(url).ConfigureAwait(false)) {
                if(!response.IsSuccessStatusCode) {
                    int code=(int)response.StatusCode;
                    throw new ApiException(code==401||code==403?"Acesso não autorizado. Confira a chave e o plano da API.":code==429?"Limite de consultas atingido. Aguarde antes de tentar novamente.":"O serviço retornou erro "+code+". Tente novamente mais tarde.");
                }
                var text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if(text.Length>4000000) throw new ApiException("Resposta maior que o esperado.");
                var data=Json.Read<Dictionary<string,object>>(text);
                if(data==null) throw new ApiException("O serviço retornou uma resposta vazia.");
                if(Json.Get(data,"error")!=null && !object.Equals(Json.Get(data,"error"),false)) throw new ApiException("A API não conseguiu atender à consulta. Confira acesso, plano e parâmetros.");
                return data;
            }
        } catch(ApiException) {throw;} catch(TaskCanceledException) {throw new ApiException("A consulta demorou demais. Confira a conexão e tente novamente.");}
        catch {throw new ApiException("Não foi possível consultar o serviço. Confira sua internet. Os dados locais continuam disponíveis.");}
    }
    static string Q(string s) {return Uri.EscapeDataString(s??"");}
    public static async Task<List<Location>> Cities(string query) {
        var d=await Get("https://geocoding-api.open-meteo.com/v1/search?name="+Q(query)+"&count=10&language=pt&format=json").ConfigureAwait(false);
        return Json.List(Json.Get(d,"results")).Select(o=>{var m=Json.Map(o);return new Location{Name=Json.Str(Json.Get(m,"name")),Region=Json.Str(Json.Get(m,"admin1")),Country=Json.Str(Json.Get(m,"country")),Latitude=Json.Num(Json.Get(m,"latitude"))??0,Longitude=Json.Num(Json.Get(m,"longitude"))??0};}).ToList();
    }
    public static async Task<Weather> Climate(Location l) {
        var d=await Get("https://api.open-meteo.com/v1/forecast?latitude="+l.Latitude.ToString(CultureInfo.InvariantCulture)+"&longitude="+l.Longitude.ToString(CultureInfo.InvariantCulture)+"&current=temperature_2m,relative_humidity_2m,is_day&timezone=auto&forecast_days=1").ConfigureAwait(false);
        return ParseWeather(d,DateTime.UtcNow);
    }
    public static Weather ParseWeather(Dictionary<string,object> d,DateTime now) {
        var c=Json.Map(Json.Get(d,"current"));var units=Json.Map(Json.Get(d,"current_units"));
        var t=Json.Num(Json.Get(c,"temperature_2m"));var h=Json.Num(Json.Get(c,"relative_humidity_2m"));
        var day=Json.Num(Json.Get(c,"is_day"));
        if(h.HasValue && (h<0||h>100)) h=null;
        if(t.HasValue && (t < -100 || t>70)) t=null;
        if(Json.Str(Json.Get(units,"temperature_2m"))!="°C") t=null;
        if(Json.Str(Json.Get(units,"relative_humidity_2m"))!="%") h=null;
        DateTime modelTime; string stamp=Json.Str(Json.Get(c,"time"));
        var offset=Json.Num(Json.Get(d,"utc_offset_seconds"));
        if(!DateTime.TryParse(stamp,CultureInfo.InvariantCulture,DateTimeStyles.None,out modelTime)||!offset.HasValue)
            throw new ApiException("A API retornou clima sem horário válido. A comparação foi suspensa.");
        DateTime modelUtc=DateTime.SpecifyKind(modelTime,DateTimeKind.Unspecified).AddSeconds(-offset.Value);
        modelUtc=DateTime.SpecifyKind(modelUtc,DateTimeKind.Utc);
        if(Math.Abs((now-modelUtc).TotalMinutes)>60) throw new ApiException("A API retornou um horário meteorológico antigo. O último registro foi preservado.");
        if(!t.HasValue&&!h.HasValue) throw new ApiException("A API não retornou temperatura nem umidade válidas.");
        return new Weather {Temperature=t,Humidity=h,IsDay=day==1?(bool?)true:day==0?(bool?)false:null,ModelTime=stamp,Timezone=Json.Str(Json.Get(d,"timezone")),FetchedUtc=now,ModelUtc=modelUtc};
    }
    public static async Task<List<Plant>> SearchPlants(string key,string query) {
        if(string.IsNullOrWhiteSpace(key)) throw new ApiException("Cadastre sua chave da Perenual em Configurações para buscar espécies.");
        var d=await Get("https://perenual.com/api/v2/species-list?key="+Q(key)+"&q="+Q(query)).ConfigureAwait(false);
        return Json.List(Json.Get(d,"data")).Select(o=>{var m=Json.Map(o);return new Plant{ApiId=(int)(Json.Num(Json.Get(m,"id"))??0),Name=Json.Str(Json.Get(m,"common_name")),Scientific=string.Join(", ",Json.List(Json.Get(m,"scientific_name")).Select(Json.Str))};}).Where(p=>p.ApiId>0).ToList();
    }
    static string Translate(string v) {
        switch(v.Trim().ToLowerInvariant()) {
            case "frequent":return "Rega frequente";case "average":return "Rega moderada";case "minimum":return "Pouca rega";case "none":return "Sem indicação de rega";
            case "full sun":return "Sol pleno";case "part shade":case "partial shade":return "Meia-sombra";case "full shade":return "Sombra";case "sun-part shade":return "Sol / meia-sombra";
            case "well-drained":return "Bem drenado";case "dry":return "Seco";case "moist":return "Úmido";default:return v;
        }
    }
    static string CleanField(object v) {
        string s=Json.Str(v);
        if(s.ToLowerInvariant().Contains("upgrade")||s.ToLowerInvariant().Contains("premium")||s.ToLowerInvariant().Contains("api key")) return "";
        return s;
    }
    public static Plant ParsePlant(Dictionary<string,object> m) {
        int id=(int)(Json.Num(Json.Get(m,"id"))??0);
        string name=CleanField(Json.Get(m,"common_name"));
        if(id<=0 || string.IsNullOrWhiteSpace(name)) throw new ApiException("Detalhes da espécie indisponíveis. Confira as permissões do plano Perenual.");
        return new Plant{Needs=SpeciesCare.Parse(m,DateTime.UtcNow),ApiId=id,Name=name,Scientific=string.Join(", ",Json.List(Json.Get(m,"scientific_name")).Select(Json.Str)),
            Watering=Translate(CleanField(Json.Get(m,"watering"))),Sunlight=string.Join(", ",Json.List(Json.Get(m,"sunlight")).Select(x=>Translate(CleanField(x))).Where(x=>x!="")),
            SoilAdvice=string.Join(", ",Json.List(Json.Get(m,"soil")).Select(x=>Translate(CleanField(x))).Where(x=>x!="")),Description=CleanField(Json.Get(m,"description")),
            Source="Perenual · espécie "+id,ImportedUtc=DateTime.UtcNow};
    }
    public static async Task<Plant> Details(string key,int id) {
        return ParsePlant(await Get("https://perenual.com/api/v2/species/details/"+id+"?key="+Q(key)).ConfigureAwait(false));
    }
}
}
