using System;
using System.Collections.Generic;
using System.Linq;

namespace Raiz {
public class CarePlan {
    public bool AutomaticSpeciesCare {get;set;} public bool DryTriggerCustom {get;set;} public string LightSpeciesKey {get;set;}
    public double NearDryIndex {get;set;}
    public int? LightReferenceRaw {get;set;} public int? ReferenceDark {get;set;} public int? ReferenceBright {get;set;}
    public DateTime? LightReferenceUtc {get;set;} public string LightReferenceNote {get;set;}
    public DateTime? LearningResetUtc {get;set;}
    public List<WateringRecord> Waterings {get;set;} public WaterSession Session {get;set;}
    public CarePlan(){AutomaticSpeciesCare=true;NearDryIndex=10;Waterings=new List<WateringRecord>();}
}
public class WaterSession {
    public DateTime StartedUtc {get;set;} public double InitialIndex {get;set;} public int TotalMl {get;set;}
    public int? Dry {get;set;} public int? Wet {get;set;}
}
public class WateringRecord {
    public string RecordId {get;set;} public string Origin {get;set;} public string EventKey {get;set;} public string PlantName {get;set;}
    public bool VolumeUnknown {get;set;} public bool SensorReadingUnknown {get;set;} public bool InitialReadingUnknown {get;set;}
    public DateTime FinishedUtc {get;set;} public int TotalMl {get;set;}
    public double InitialIndex {get;set;} public double FinalIndex {get;set;}
    public int? Dry {get;set;} public int? Wet {get;set;}
    public bool DrainageConfirmed {get;set;}
    public WateringRecord(){RecordId=Guid.NewGuid().ToString("N");Origin="Guiada";}
}
public class CareReport {
    public Advice Soil {get;set;} public Advice Water {get;set;} public Advice Light {get;set;}
    public bool CanStartWatering {get;set;} public bool HasLiveSoil {get;set;}
    public int? EstimatedMl {get;set;} public int LearningCount {get;set;}
    public IEnumerable<Advice> Advice {get {return new[]{Soil,Water,Light};}}
}
public static class CareEngine {
    public const int FirstPortionMl=30;
    public const int DryPersistenceSeconds=30;
    public const int LightPersistenceSeconds=60;
    public const string WaterSource="https://yardandgarden.extension.iastate.edu/faq/how-often-should-i-water-my-jade-plant";
    public const string LightSource="https://plants.ces.ncsu.edu/plants/crassula-ovata/";
    public static bool IsJade(Plant p) {
        if(p==null)return false;
        string name=(p.Scientific??"").Trim().ToLowerInvariant();
        return name=="crassula ovata"||name.StartsWith("crassula ovata '")||name=="crassula argentea";
    }
    public static bool Supported(Plant p){return IsJade(p)||SpeciesCare.HasWater(p)||(p!=null&&p.Soil!=null&&p.Soil.Enabled);}
    public static double Trigger(Plant p) {
        if(p!=null&&p.Soil!=null&&p.Soil.Enabled)return p.Soil.Min.Value;
        if(SpeciesCare.HasWater(p)&&!SpeciesCare.CustomTrigger(p))return SpeciesCare.Preset(p);
        var plan=p==null?null:p.Care;
        return plan!=null&&plan.NearDryIndex>=0&&plan.NearDryIndex<=60?plan.NearDryIndex:10;
    }
    public static bool ReliableSoil(Sample s,Calibration c,DateTime now) {
        if(s==null||!s.Fresh(now)||!c.Soil(s.Soil).HasValue)return false;
        int low=Math.Min(c.Dry.Value,c.Wet.Value),high=Math.Max(c.Dry.Value,c.Wet.Value);
        double margin=Math.Max(30,(high-low)*0.25);
        return s.Soil>0&&s.Soil<1023&&s.Soil>=low-margin&&s.Soil<=high+margin;
    }
    // Continuity matters: a gap, reconnect, or isolated dip cannot count as persistence.
    public static bool Persistent(IList<Sample> history,DateTime now,int seconds,Func<Sample,bool> condition) {
        if(history==null||history.Count==0)return false;
        DateTime previous=now;int count=0;
        for(int i=history.Count-1;i>=0;i--) {
            var s=history[i];
            if(s.Utc>previous||previous-s.Utc>=TimeSpan.FromSeconds(12)||!condition(s))return false;
            previous=s.Utc;count++;
            if(now-s.Utc>=TimeSpan.FromSeconds(seconds))return count>=4;
        }
        return false;
    }
    public static List<WateringRecord> Learned(Plant p,Calibration c) {
        if(p==null||p.Care==null||p.Care.Waterings==null)return new List<WateringRecord>();
        return p.Care.Waterings.Where(x=>!x.VolumeUnknown&&!x.SensorReadingUnknown&&!x.InitialReadingUnknown&&x.DrainageConfirmed&&x.TotalMl>0&&x.TotalMl<=100000&&x.Dry==c.Dry&&x.Wet==c.Wet&&x.InitialIndex<=Trigger(p)&&x.FinalIndex>x.InitialIndex+5&&(!p.Care.LearningResetUtc.HasValue||x.FinishedUtc>p.Care.LearningResetUtc.Value))
            .OrderByDescending(x=>x.FinishedUtc).Take(5).ToList();
    }
    public static int? Estimate(Plant p,Calibration c) {
        var values=Learned(p,c).Select(x=>x.TotalMl).OrderBy(x=>x).ToArray();if(values.Length==0)return null;
        double median=values.Length%2==1?values[values.Length/2]:(values[values.Length/2-1]+values[values.Length/2])/2.0;
        return Math.Max(1,(int)Math.Round(median));
    }
    public static CareReport Evaluate(Plant p,Sample s,Weather w,Calibration c,IList<Sample> history,DateTime now) {
        var result=new CareReport {Soil=new Advice("Umidade: aguardando sensor","Conecte o Arduino para saber se é hora de regar.","pending"),
            Water=new Advice("Água: sem estimativa atual","Sem uma leitura atual, nenhuma dose é sugerida.","pending"),
            Light=new Advice("Luz: aguardando sensor","A avaliação começa ao receber leituras atuais.","pending")};
        if(p==null){result.Soil=new Advice("Escolha uma planta","Cadastre a espécie para ativar seus cuidados.","pending");return result;}
        if(s==null||!s.Fresh(now))return result;
        result.Light=EvaluateLight(p,s,w,c,history,now);
        if(SpeciesCare.Enabled(p)&&SpeciesCare.Needs(p).WateringCode=="none"&&!(p.Soil!=null&&p.Soil.Enabled)){result.Soil=new Advice("Rega automática suspensa pela espécie",SpeciesCare.WaterGuide(p),"pending");result.Water=new Advice("Água: sem indicação automática","A API não fornece dose de rega para este perfil. Confira a planta antes de intervir.","pending");return result;}
        if(!Supported(p)){result.Soil=new Advice("Umidade: defina o perfil da espécie","Use uma faixa validada para esta planta. Atualize a espécie pela API ou cadastre uma faixa local validada.","pending");return result;}
        if(!c.Soil(s.Soil).HasValue){result.Soil=new Advice("Umidade: falta calibrar","Registre solo seco e úmido para interpretar a leitura.","pending");return result;}
        if(!ReliableSoil(s,c,now)){result.Soil=new Advice("Umidade: confira o sensor","Leitura muito fora das referências. Confira contato, fios e calibração antes de decidir a rega.","attention");return result;}
        result.HasLiveSoil=true;double soil=c.Soil(s.Soil).Value;double threshold=Trigger(p);
        result.LearningCount=Learned(p,c).Count;
        var session=p.Care==null?null:p.Care.Session;
        if(session!=null) {
            result.Soil=new Advice("Rega em andamento","Aplique devagar e pare quando começar a sair água pelos furos. Não tente manter o sensor em 100%.");
            result.Water=new Advice("Registrado nesta rega: "+session.TotalMl+" mL","Registre apenas o que você realmente colocou. Ao drenar, finalize a sessão para o app aprender a quantidade deste vaso.");
            if(soil>=85) {
                result.Soil=new Advice("Solo já úmido — pause e confira","A leitura subiu perto da referência úmida. Pause a adição de água e observe a drenagem; não continue regando só para fazer o índice subir.","attention");
                result.Water=new Advice("Registrado nesta rega: "+session.TotalMl+" mL","Se começou a drenar, finalize. Se ainda não drenou, aguarde a distribuição e confira o substrato antes de adicionar outra porção.");
            }
            if(session.Dry!=c.Dry||session.Wet!=c.Wet)result.Water=new Advice("A calibração mudou durante a rega","Encerre a sessão sem aprender e confira as referências antes de iniciar outra.","attention");
            else if(now-session.StartedUtc>TimeSpan.FromHours(2))result.Water=new Advice("Sessão de rega antiga","Encerre sem aprender. Uma sessão antiga não serve para estimar a próxima dose.","pending");
            return result;
        }
        var lastWater=p.Care==null||p.Care.Waterings==null?null:p.Care.Waterings.OrderByDescending(x=>x.FinishedUtc).FirstOrDefault();
        if(lastWater!=null && now>=lastWater.FinishedUtc && now-lastWater.FinishedUtc<TimeSpan.FromMinutes(30)) {
            result.Soil=new Advice("Rega registrada: aguarde a distribuição","Aguarde 30 minutos antes de avaliar uma nova rega. Confira a drenagem e não repita a dose automaticamente.");
            result.Water=new Advice("Água agora: não adicionar","O app está em pausa após a rega. Esvazie o pratinho.","ok");return result;
        }
        if(p.Soil!=null&&p.Soil.Enabled&&soil>p.Soil.Max.Value) {
            result.Soil=new Advice("Solo acima da faixa cadastrada — não regar","A leitura excedeu o máximo que você definiu. Confira a drenagem e acompanhe a secagem; a leitura isolada não diagnostica encharcamento.","attention");
            result.Water=new Advice("Água agora: 0 mL","Não adicione água com o solo acima da faixa cadastrada.","ok");return result;
        }
        if(soil>threshold) {
            result.Soil=new Advice(soil>=75?"Solo úmido — não precisa regar":"Umidade adequada para aguardar",IsJade(p)?"A jade deve secar entre regas. Deixe o substrato perder umidade; 100% é a referência úmida, não uma meta para manter.":SpeciesCare.WaterGuide(p)+" A leitura está acima do gatilho local de rega. Aguarde e acompanhe o substrato.","ok");
            result.Water=new Advice("Água agora: 0 mL","Não há indicação de rega neste momento.","ok");return result;
        }
        bool stable=Persistent(history,now,DryPersistenceSeconds,x=>ReliableSoil(x,c,x.Utc)&&c.Soil(x.Soil).Value<=threshold);
        if(!stable) {
            result.Soil=new Advice("Solo perto de seco — confirmando leitura","Aguarde 30 segundos de leituras contínuas nessa condição. Um pico isolado não deve provocar rega.","pending");
            result.Water=new Advice("Água: aguarde a confirmação","Verifique também o substrato com o toque antes de regar.","pending");return result;
        }
        result.CanStartWatering=true;
        result.Soil=new Advice("Precisa regar? Confira o substrato","A leitura permaneceu no gatilho local de "+threshold.ToString("0")+"%. Confirme a necessidade de água no substrato antes de regar. O gatilho é local e ajustável, não um limite universal da espécie.","attention");
        result.EstimatedMl=Estimate(p,c);
        if(result.EstimatedMl.HasValue)
            result.Water=new Advice("Estimativa de rega: cerca de "+result.EstimatedMl.Value+" mL","Base: "+result.LearningCount+" rega(s) registrada(s) neste vaso até iniciar drenagem. É uma aproximação: aplique devagar e pare ao drenar, mesmo antes do valor estimado.","attention");
        else result.Water=new Advice("Primeira porção de teste: "+FirstPortionMl+" mL","Sem tamanho do vaso ou regas anteriores, o total é desconhecido. Comece devagar com essa pequena porção experimental; acompanhe a drenagem e registre as porções para o app aprender. Não é a dose total necessária.","attention");
        return result;
    }
    static Advice EvaluateLight(Plant p,Sample s,Weather w,Calibration c,IList<Sample> history,DateTime now) {
        var value=c.Light(s.Light);
        if(!value.HasValue)return new Advice("Luz: falta calibrar","Registre as referências de escuro e claro.","pending");
        if(w==null||!w.Fresh(now)||!w.IsDay.HasValue)return new Advice("Luz: aguardando contexto diurno","Atualize o clima. A leitura continua visível, mas o app evita classificar a noite como falta de luz.","pending");
        if(!w.IsDay.Value)return new Advice("Luz: pausa noturna","Durante a noite não há aviso de iluminação insuficiente.");
        double minimum,maximum=100;
        bool custom=p.Light!=null&&p.Light.Enabled;
        if(custom){minimum=p.Light.Min.Value;maximum=p.Light.Max.Value;}
        else {
            var plan=p.Care;
            if(SpeciesCare.Enabled(p)&&plan!=null&&plan.LightSpeciesKey!=null&&plan.LightSpeciesKey!=SpeciesCare.LightKey(p))return new Advice("Luz: renove a referência da espécie",SpeciesCare.LightGuide(p),"pending");
            if(plan==null||!plan.LightReferenceRaw.HasValue)return new Advice("Luz: registre um local adequado","Em Cuidados e rega, registre a luz de um local cuja iluminação você confirmou para a planta. O percentual do LDR sozinho não informa se é ideal. "+SpeciesCare.LightGuide(p)+"","pending");
            if(plan.ReferenceDark!=c.Dark||plan.ReferenceBright!=c.Bright)return new Advice("Luz: renove a referência","A calibração mudou. Registre novamente o local iluminado.","pending");
            var reference=c.Light(plan.LightReferenceRaw.Value);
            if(!reference.HasValue||reference<=0)return new Advice("Luz: referência inválida","Registre novamente um ponto iluminado.","pending");
            minimum=reference.Value*0.8;
        }
        if(value.Value>=minimum&&value.Value<=maximum)return new Advice("Iluminação adequada pela referência",custom?"A leitura está na faixa que você cadastrou. O app não mede lux nem as horas diárias de exposição.":"A luz está próxima ou acima do ponto iluminado que você registrou. A tolerância local é de 20%; isso não comprova lux ou horas diárias suficientes.","ok");
        bool below=value.Value<minimum;
        bool stable=Persistent(history,now,LightPersistenceSeconds,x=>{var n=c.Light(x.Light);return n.HasValue&&(below?n<minimum:n>maximum);});
        if(!stable)return new Advice("Luz: variação passageira","Aguarde 60 segundos para confirmar a mudança. A sombra da mão ou de um fio não gera aviso imediato.","pending");
        return new Advice(below?"Iluminação abaixo da referência":"Iluminação acima da faixa cadastrada",below?"A luz permaneceu abaixo da referência local. Confira sombras e a posição junto à janela. A decisão considera a sua referência, não um limiar universal da espécie.":"A leitura ficou acima do máximo que você cadastrou. Confira a exposição da planta.","attention");
    }
    public static void RegisterLight(Plant p,Sample s,Weather w,Calibration c,string note,DateTime now) {
        if(p==null||s==null||!s.Fresh(now))throw new ApiException("Conecte os sensores e aguarde uma leitura atual.");
        if(w==null||!w.Fresh(now)||w.IsDay!=true)throw new ApiException("Registre a referência durante o dia, com o clima atualizado.");
        var light=c.Light(s.Light);if(!light.HasValue||light<=0)throw new ApiException("Calibre a luz e escolha um ponto iluminado para a referência.");
        if(string.IsNullOrWhiteSpace(note))throw new ApiException("Descreva o local usado como referência.");
        if(p.Care==null)p.Care=new CarePlan();
        p.Care.LightSpeciesKey=SpeciesCare.LightKey(p);p.Care.LightReferenceRaw=s.Light;p.Care.ReferenceDark=c.Dark;p.Care.ReferenceBright=c.Bright;p.Care.LightReferenceUtc=now;p.Care.LightReferenceNote=note.Trim();
    }
    public static void Start(Plant p,Sample s,Weather w,Calibration c,IList<Sample> history,DateTime now) {
        if(!Evaluate(p,s,w,c,history,now).CanStartWatering)throw new ApiException("Não há indicação atual para iniciar uma rega. Confira o sensor e aguarde o solo secar.");
        if(p.Care==null)p.Care=new CarePlan();
        p.Care.Session=new WaterSession{StartedUtc=now,InitialIndex=c.Soil(s.Soil).Value,Dry=c.Dry,Wet=c.Wet};
    }
    public static void AddPortion(Plant p,int ml,DateTime now) {
        if(p==null||p.Care==null||p.Care.Session==null)throw new ApiException("Inicie uma sessão de rega primeiro.");
        var session=p.Care.Session;
        if(now<session.StartedUtc||now-session.StartedUtc>TimeSpan.FromHours(2))throw new ApiException("Esta sessão ficou antiga. Encerre sem aprender e confira o vaso.");
        if(ml<=0||ml>10000||session.TotalMl>100000-ml)throw new ApiException("Informe uma porção válida, em mL.");
        session.TotalMl+=ml;
    }
    public static WateringRecord Finish(Plant p,Sample s,Calibration c,bool drained,DateTime now) {
        if(p==null||p.Care==null||p.Care.Session==null)throw new ApiException("Nenhuma sessão de rega em andamento.");
        var session=p.Care.Session;
        if(!drained)throw new ApiException("Só conclua o aprendizado depois de observar água começar a sair pelos furos.");
        if(!ReliableSoil(s,c,now))throw new ApiException("Aguarde uma leitura atual e válida antes de concluir o aprendizado.");
        if(session.Dry!=c.Dry||session.Wet!=c.Wet)throw new ApiException("A calibração mudou. Encerre sem aprender e inicie outra sessão quando o solo estiver seco.");
        if(now<session.StartedUtc||now-session.StartedUtc>TimeSpan.FromHours(2))throw new ApiException("Sessão antiga: encerre sem aprender.");
        if(session.TotalMl<=0)throw new ApiException("Registre o volume realmente aplicado antes de concluir.");
        double final=c.Soil(s.Soil).Value;
        if(final<session.InitialIndex+5)throw new ApiException("O sensor ainda não respondeu à rega. Aguarde a distribuição da água; não adicione mais apenas para aumentar o índice.");
        var record=new WateringRecord{PlantName=p.Name,FinishedUtc=now,TotalMl=session.TotalMl,InitialIndex=session.InitialIndex,FinalIndex=final,Dry=c.Dry,Wet=c.Wet,DrainageConfirmed=true};
        if(p.Care.Waterings==null)p.Care.Waterings=new List<WateringRecord>();p.Care.Waterings.Add(record);p.Care.Session=null;return record;
    }
    public static void Cancel(Plant p,DateTime now) {
        if(p==null||p.Care==null||p.Care.Session==null)return;
        var session=p.Care.Session;
        // Retain the volume actually reported, but do not use an unfinished session for dose learning.
        if(session.TotalMl>0) {
            if(p.Care.Waterings==null)p.Care.Waterings=new List<WateringRecord>();
            p.Care.Waterings.Add(new WateringRecord{PlantName=p.Name,FinishedUtc=now,TotalMl=session.TotalMl,InitialIndex=session.InitialIndex,FinalIndex=session.InitialIndex,SensorReadingUnknown=true,Dry=session.Dry,Wet=session.Wet,DrainageConfirmed=false});
        }
        p.Care.Session=null;
    }
    public static bool ConfirmAlreadyWatered(Plant p,Sample s,Calibration c,string origin,string eventKey,DateTime happenedUtc,DateTime receivedUtc) {
        if(p==null||string.IsNullOrWhiteSpace(eventKey))throw new ApiException("Confirmação de rega inválida.");
        if(p.Care==null)p.Care=new CarePlan();if(p.Care.Waterings==null)p.Care.Waterings=new List<WateringRecord>();
        if(p.Care.Waterings.Any(x=>x.EventKey==eventKey))return false;
        var session=p.Care.Session;
        bool measured=s!=null&&ReliableSoil(s,c,receivedUtc);
        double index=measured?c.Soil(s.Soil).Value:0;
        p.Care.Waterings.Add(new WateringRecord{PlantName=p.Name,Origin=origin,EventKey=eventKey,FinishedUtc=happenedUtc,TotalMl=session==null?0:session.TotalMl,VolumeUnknown=session==null||session.TotalMl==0,SensorReadingUnknown=!measured,InitialReadingUnknown=session==null,InitialIndex=session==null?0:session.InitialIndex,FinalIndex=index,Dry=c.Dry,Wet=c.Wet,DrainageConfirmed=false});
        p.Care.Session=null;return true;
    }
}
}
