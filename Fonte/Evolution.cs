using System;using System.Collections.Generic;using System.Globalization;using System.Linq;
namespace Raiz {
public class LightHour {public int Hour;public double Seconds,WeightedIndex;public double Mean {get{return Seconds==0?0:WeightedIndex/Seconds;}}}
public static class Evolution {
    public static List<double> DryingHours(DailySnapshot data,double trigger){
        var result=new List<double>();var water=data.Waterings.OrderBy(x=>x.FinishedUtc).ToArray();
        for(int i=0;i<water.Length;i++){
            var start=water[i].FinishedUtc;if(data.LastCalibrationUtc.HasValue&&start<data.LastCalibrationUtc.Value)continue;var end=i+1<water.Length?water[i+1].FinishedUtc:DateTime.MaxValue;
            var first=data.Points.FirstOrDefault(x=>x.Utc>=start&&x.Utc<end&&x.Soil.HasValue);
            if(first==null||first.Utc-start>TimeSpan.FromMinutes(10)||first.Soil<=trigger)continue;
            var dry=data.Points.FirstOrDefault(x=>x.Utc>first.Utc&&x.Utc<end&&x.Segment==first.Segment&&x.Soil.HasValue&&x.Soil<=trigger);
            if(dry!=null)result.Add((dry.Utc-start).TotalHours);
        }return result;
    }
    public static string Describe(DailySnapshot data,Plant plant){
        var cycles=DryingHours(data,CareEngine.Trigger(plant));string drying;
        if(cycles.Count==0)drying="Secagem: ainda falta um ciclo observado sem interrupções, de uma rega registrada até o gatilho de solo.";
        else{var sorted=cycles.OrderBy(x=>x).ToArray();double median=(sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])/2;drying="Secagem observada: mediana de "+median.ToString("0.0",CultureInfo.CurrentCulture)+" h até o gatilho, em "+cycles.Count+" ciclo(s) contínuo(s). Aproximação em trechos de cinco minutos; não prevê a próxima rega.";}
        var hours=data.LightHours.Where(x=>x.Seconds>=600).OrderByDescending(x=>x.Mean).ToArray();
        string light=hours.Length==0?"Luz: aguardando pelo menos dez minutos de coleta contínua em uma faixa horária.":"Maior luz média entre as faixas observadas: "+hours[0].Hour.ToString("00")+"h–"+(hours[0].Hour+1).ToString("00")+"h, índice relativo "+hours[0].Mean.ToString("0")+"%. Comparação apenas das horas coletadas; não mede horas de sol direto.";
        return drying+"\n\n"+light+(data.LastCalibrationUtc.HasValue?" Resumo restrito à calibração mais recente registrada.":"")+(data.Truncated?"\nResumo limitado à parte do arquivo consultada.":"");
    }
}
public static class SensorHealth {
    public static Advice Evaluate(IList<Sample> samples,Sample last,Calibration c,bool connected,bool demo,DateTime now,DateTime? invalidUtc){
        if(demo)return new Advice("Diagnóstico pausado na demonstração","Conecte sensores reais para conferir a montagem.","pending");
        if(!connected)return new Advice("Arduino desconectado","Confira o cabo USB e selecione a porta em Sensores. Sem coleta, o histórico terá uma lacuna.","pending");
        if(last==null||!last.Fresh(now))return new Advice("Sem medições atuais",invalidUtc.HasValue&&now-invalidUtc.Value<TimeSpan.FromSeconds(30)?"O Arduino enviou um pacote inválido ou fora de 0–1023. Confira o programa da placa; o pacote foi descartado.":"A porta está aberta, mas não há um par de leituras recente. Confira o cabo e o programa da placa; reconecte em Sensores.","attention");
        if(invalidUtc.HasValue&&invalidUtc.Value>last.Utc&&now-invalidUtc.Value<TimeSpan.FromSeconds(30))return new Advice("Pacote de sensor descartado","Formato inválido ou valor fora de 0–1023. A última leitura válida foi preservada; confira o programa da placa se persistir.","attention");
        if(CareEngine.Persistent(samples,now,60,x=>x.Soil==0||x.Soil==1023)||CareEngine.Persistent(samples,now,60,x=>x.Light==0||x.Light==1023))return new Advice("Sinal no limite por um minuto","Solo ou luz permaneceu em 0 ou 1023. Pode ser saturação ou ligação solta. Confira a condição e a fiação com a alimentação desligada antes de alterar conexões.","attention");
        if(c.Dry.HasValue&&c.Wet.HasValue&&CareEngine.Persistent(samples,now,30,x=>!CareEngine.ReliableSoil(x,c,x.Utc)))return new Advice("Solo fora das referências locais","O sinal ficou fora da margem da calibração por 30 segundos. Confira a posição do sensor e as referências. Isso sozinho não confirma defeito nem indica rega.","attention");
        if(CareEngine.Persistent(samples,now,600,x=>x.Soil==last.Soil)||CareEngine.Persistent(samples,now,600,x=>x.Light==last.Light))return new Advice("Possível leitura parada","Solo ou luz repetiu exatamente o valor por dez minutos. O ambiente pode estar estável. Faça uma sombra breve no LDR e confira a resposta, sem mover a fiação energizada.","attention");
        return new Advice("Leituras chegando normalmente","Nenhuma anomalia foi detectada nas verificações disponíveis. Isso não comprova a precisão dos sensores.","ok");
    }
}
}

