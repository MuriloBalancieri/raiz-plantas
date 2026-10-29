using System;using System.Collections.Generic;using System.IO;using System.Linq;using Raiz;
public static class SpeciesTests {
    static Plant Profile(string water,string light="full sun",string days="5-7",string unit="days"){
        return Api.ParsePlant(Json.Read<Dictionary<string,object>>(Json.Write(new{id=123,common_name="Planta teste",scientific_name=new[]{"Species test"},watering=water,sunlight=new[]{light},watering_general_benchmark=new{value=days,unit=unit}})));
    }
    public static void Run(Action<bool,string> check,string folder){
        var now=new DateTime(2026,10,29,20,0,0,DateTimeKind.Utc);var c=new Calibration{Dry=392,Wet=281,Dark=22,Bright=316};var w=new Weather{FetchedUtc=now,ModelUtc=now,IsDay=true};
        Plant low=Profile("Minimum"),average=Profile("Average","part shade"),high=Profile("Frequent","full shade"),none=Profile("None");
        check(CareEngine.Trigger(low)==10&&CareEngine.Trigger(average)==25&&CareEngine.Trigger(high)==40,"especie seleciona presets locais diferentes conforme categoria API");
        check(SpeciesCare.Summary(high,now).Contains("experimental")&&SpeciesCare.Summary(high,now).Contains("não umidade ideal"),"presets nao sao anunciados como percentuais ideais da API");
        check(average.Needs.IntervalMinDays==5&&average.Needs.IntervalMaxDays==7,"intervalo API preserva faixa de dias");
        check(Profile("Average",days:"7").Needs.IntervalMinDays==7,"intervalo API aceita um unico dia");
        check(!Profile("Average",days:"7-5").Needs.IntervalMinDays.HasValue,"intervalo API invertido rejeitado");
        check(!Profile("Average",days:"0").Needs.IntervalMinDays.HasValue,"intervalo API zero rejeitado");
        check(!Profile("Average",days:"5-7",unit:"ml").Needs.IntervalMinDays.HasValue,"nao confunde unidade desconhecida com dias ou dose");
        check(!Profile("Average",days:"Premium upgrade").Needs.IntervalMinDays.HasValue,"campo bloqueado nao vira intervalo numerico");
        check(SpeciesCare.LightGuide(low).Contains("sol pleno")&&SpeciesCare.LightGuide(average).Contains("meia-sombra")&&SpeciesCare.LightGuide(high).Contains("sombra"),"orientacao luminosa muda conforme especie");
        check(!low.Soil.Enabled&&!low.Light.Enabled&&!low.Temperature.Enabled,"importacao nao inventa limites ideais em porcentagem lux ou temperatura");
        var history=Enumerable.Range(0,61).Select(i=>new Sample{Soil=359,Light=260,Utc=now.AddSeconds(i-60)}).ToList();var s=history.Last();
        check(!CareEngine.Evaluate(low,s,w,c,history,now).CanStartWatering&&CareEngine.Evaluate(high,s,w,c,history,now).CanStartWatering,"categoria rega frequente adapta aviso por sensor sem alterar dose");
        check(!CareEngine.Evaluate(none,s,w,c,history,now).CanStartWatering,"categoria none suspende sugestao automatica");
        check(!CareEngine.Evaluate(high,new Sample{Soil=281,Light=260,Utc=now},w,c,history,now).CanStartWatering,"categoria frequente nunca recomenda rega com solo umido");
        check(!CareEngine.Evaluate(high,new Sample{Soil=359,Light=260,Utc=now.AddMinutes(-1)},w,c,history,now).CanStartWatering,"perfil API tambem rejeita sensor antigo");
        check(!CareEngine.Evaluate(high,s,w,c,new List<Sample>{s},now).CanStartWatering,"perfil API ainda exige persistencia das leituras");
        high.Soil=new Raiz.Range(15,80);check(CareEngine.Trigger(high)==15,"faixa manual tem prioridade sobre preset da API");high.Soil=new Raiz.Range();
        high.Care.NearDryIndex=55;high.Care.DryTriggerCustom=true;check(CareEngine.Trigger(high)==55,"gatilho personalizado e preservado inclusive acima de trinta");
        high.Care.NearDryIndex=10;check(CareEngine.Trigger(high)==10,"gatilho explicito de dez nao e confundido com default");high.Care.DryTriggerCustom=false;
        high.Care.AutomaticSpeciesCare=false;check(!CareEngine.Supported(high),"usuario pode pausar adaptacao da especie sem faixa manual");high.Care.AutomaticSpeciesCare=true;
        var unknown=Profile("Premium upgrade","unknown");check(!CareEngine.Supported(unknown)&&!unknown.Needs.IntervalMinDays.Equals(0),"categoria desconhecida nao ganha um preset de rega");
        var manual=new Plant{Watering="Rega frequente",Sunlight="Sol pleno"};check(!SpeciesCare.HasWater(manual),"texto manual nao e tratado como dado autenticado da API");
        var migrated=new Plant{ApiId=123,Source="Perenual · espécie 123",Watering="Rega frequente",Sunlight="Sol pleno, Meia-sombra"};
        check(CareEngine.Trigger(migrated)==40&&SpeciesCare.Needs(migrated).LightCodes.Length==2,"perfil antigo usa categorias Perenual ja salvas ate atualizar");
        CareEngine.RegisterLight(high,s,w,c,"Local em sombra compativel",now);string key=high.Care.LightSpeciesKey;
        check(key=="full shade"&&CareEngine.Evaluate(high,s,w,c,history,now).Light.Severity=="ok","referencia luminosa e vinculada ao perfil da especie");
        var fresh=Profile("Frequent","part shade");var id=high.LocalId;high.Name="Nome do meu vaso";high.Care.Waterings.Add(new WateringRecord{TotalMl=120,FinishedUtc=now});
        var records=high.Care.Waterings;SpeciesCare.Merge(high,fresh);
        check(high.LocalId==id&&high.Name=="Nome do meu vaso"&&high.Care.Waterings==records,"atualizar API preserva identidade nome e historico do vaso");
        check(!high.Care.LightReferenceRaw.HasValue,"mudanca de exposicao da API exige renovar referencia luminosa");
        high.Soil=new Raiz.Range(15,80);high.Light=new Raiz.Range(40,90);SpeciesCare.Merge(high,fresh);check(high.Soil.Min==15&&high.Light.Max==90,"atualizar API preserva faixas manuais");
        bool rejected=false;try{SpeciesCare.Merge(high,new Plant{ApiId=999});}catch(ApiException){rejected=true;}check(rejected,"API de outra especie nao sobrescreve vaso");
        var offline=Json.Read<Plant>(Json.Write(high));check(SpeciesCare.LightGuide(offline)==SpeciesCare.LightGuide(high)&&CareEngine.Trigger(offline)==15,"cuidados continuam usando cache quando offline");
        check(SpeciesCare.Interval(high,now.AddDays(8)).Contains("sozinha não autoriza"),"intervalo vencido pede verificacao e nao manda regar por calendario");
        var store=new Store(Path.Combine(folder,"especies"));store.Settings.Plants.Add(high);store.Settings.ActiveId=high.LocalId;store.Save();var restored=new Store(store.Folder).Settings.Plants[0];
        check(restored.Needs.WateringCode=="frequent"&&restored.Care.AutomaticSpeciesCare&&restored.Care.Waterings.Count==1,"metadados da API e historico sobrevivem ao reinicio");
        var empty=Profile("Minimum");empty.Needs.LightCodes=null;check(SpeciesCare.LightKey(empty)=="","metadado luminoso ausente nao derruba app");
        var realPath=Path.Combine("work","api-especie","perfil-publico.json");if(File.Exists(realPath)){var real=Json.Read<Plant>(File.ReadAllText(realPath));check(real.ApiId>0&&SpeciesCare.HasWater(real),"resposta real Perenual e reconhecida pelo novo perfil");}
    }
}

