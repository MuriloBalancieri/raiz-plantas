using System;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Linq;using System.Text;
namespace Raiz {
// A small PDF writer for text and vector charts; no external runtime or printer is required.
public sealed class ReportPdf {
    readonly bool simulated;public ReportPdf(bool simulated=false){this.simulated=simulated;}readonly List<StringBuilder> pages=new List<StringBuilder>();StringBuilder current;
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    static string N(double n){return n.ToString("0.##",Inv);}
    public void Page(){current=new StringBuilder();pages.Add(current);Text(42,800,simulated?"RAIZ | Histórico da planta":"RAIZ | Relatório do vaso",12,true);Text(42,25,(simulated?"Dados simulados  |  ":"Índices relativos à calibração local. Horários deste computador.  |  ")+pages.Count,8,false);}
    static string Escape(string value){return (value??"").Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)").Replace("\r"," ").Replace("\n"," ");}
    public void Text(double x,double y,string value,int size=10,bool bold=false){current.Append("BT /").Append(bold?"F2":"F1").Append(' ').Append(size).Append(" Tf ").Append(N(x)).Append(' ').Append(N(y)).Append(" Td (").Append(Escape(value)).Append(") Tj ET\n");}
    public double Paragraph(double y,string value,int max=85){foreach(var paragraph in value.Split('\n')){string line="";foreach(var word in paragraph.Split(' ')){if(line.Length+word.Length>max){Text(42,y,line);y-=15;line="";}line+=(line.Length==0?"":" ")+word;}if(line!=""){Text(42,y,line);y-=15;}}return y-8;}
    public void Line(double x1,double y1,double x2,double y2,string color){current.Append(color).Append(" RG 1 w ").Append(N(x1)).Append(' ').Append(N(y1)).Append(" m ").Append(N(x2)).Append(' ').Append(N(y2)).Append(" l S 0 0 0 RG\n");}
    public void Save(string path){
        var encoding=Encoding.GetEncoding(1252);var objects=new List<byte[]>();Action<string> add=s=>objects.Add(encoding.GetBytes(s));
        add("<< /Type /Catalog /Pages 2 0 R >>");add("<< /Type /Pages /Count "+pages.Count+" /Kids ["+string.Join(" ",Enumerable.Range(0,pages.Count).Select(i=>(5+i*2)+" 0 R"))+"] >>");
        add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        for(int i=0;i<pages.Count;i++){
            add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents "+(6+i*2)+" 0 R >>");
            byte[] content=encoding.GetBytes(pages[i].ToString());using(var stream=new MemoryStream()){var start=encoding.GetBytes("<< /Length "+content.Length+" >>\nstream\n");stream.Write(start,0,start.Length);stream.Write(content,0,content.Length);var end=encoding.GetBytes("\nendstream");stream.Write(end,0,end.Length);objects.Add(stream.ToArray());}
        }
        using(var result=new MemoryStream()){
            Action<string> write=s=>{var b=encoding.GetBytes(s);result.Write(b,0,b.Length);};write("%PDF-1.4\n%âãÏÓ\n");var offsets=new List<long>();
            for(int i=0;i<objects.Count;i++){offsets.Add(result.Position);write((i+1)+" 0 obj\n");result.Write(objects[i],0,objects[i].Length);write("\nendobj\n");}
            long xref=result.Position;write("xref\n0 "+(objects.Count+1)+"\n0000000000 65535 f \n");foreach(var offset in offsets)write(offset.ToString("0000000000",Inv)+" 00000 n \n");write("trailer\n<< /Size "+(objects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");DataTransfer.AtomicWrite(path,result.ToArray());
        }
    }
}
public static class CareReports {
    static string Num(double? n){return n.HasValue?n.Value.ToString("0.0",CultureInfo.InvariantCulture):"";}
    public static void Csv(string path,Plant plant,DailySnapshot data){
        var rows=new List<string>{"tipo,data_local,planta,solo_min_pct,solo_max_pct,solo_ultimo_pct,leituras,minutos_observados,minutos_sem_cobertura,regas,volume_ml,volume_desconhecido,origem"};
        foreach(var d in data.Days)rows.Add(string.Join(",",new[]{"dia",d.Date.ToString("yyyy-MM-dd"),plant.Name,Num(d.SoilMin),Num(d.SoilMax),Num(d.LastSoil),d.Count.ToString(),(d.CoveredSeconds/60).ToString("0.0",CultureInfo.InvariantCulture),d.MissingMinutes.ToString("0.0",CultureInfo.InvariantCulture),d.Waterings.ToString(),d.KnownMl.ToString(),d.UnknownVolumes.ToString(),data.Simulated?"simulado":"real"}.Select(Store.CsvCell)));
        foreach(var r in data.Waterings)rows.Add(string.Join(",",new[]{"rega",r.FinishedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),plant.Name,"","","","","","","1",r.VolumeUnknown?"":r.TotalMl.ToString(),r.VolumeUnknown?"1":"0",r.Origin}.Select(Store.CsvCell)));
        DataTransfer.AtomicWrite(path,new UTF8Encoding(true).GetBytes(string.Join("\r\n",rows)+"\r\n"));
    }
    public static void Pdf(string path,Plant plant,DailySnapshot data,DateTime now){
        var pdf=new ReportPdf(data.Simulated);pdf.Page();pdf.Text(42,755,plant.Name??"Planta",20,true);pdf.Text(42,727,plant.Scientific??"Espécie não informada",11);
        string period=data.Days.Count==0?"Sem período":data.Days.First().Date.ToString("dd/MM/yyyy")+" a "+data.Days.Last().Date.ToString("dd/MM/yyyy");pdf.Text(42,700,"Período: "+period+"  |  Emitido: "+now.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),10);
        pdf.Text(42,660,"Resumo da evolução",14,true);double y=pdf.Paragraph(635,Evolution.Describe(data,plant));
        y=pdf.Paragraph(y-10,data.Days.Sum(d=>d.Count)+(data.Simulated?" leituras no período; ":" leituras reais; ")+data.Waterings.Count+" rega(s); "+data.Waterings.Where(r=>!r.VolumeUnknown).Sum(r=>(long)r.TotalMl)+(data.Simulated?" mL registrados e ":" mL medidos e ")+data.Waterings.Count(r=>r.VolumeUnknown)+" rega(s) sem volume informado.");
        y=pdf.Paragraph(y,"O tempo sem cobertura indica falta de observação, não valor zero. Mudanças de calibração registradas e lacunas interrompem as curvas. A dose e os índices não são medições físicas de água no substrato ou iluminância.");
        if(data.Truncated||data.Invalid>0)pdf.Paragraph(y,"Consulta limitada: "+(data.Truncated?"últimos 80 MB do arquivo. ":"")+data.Invalid+" linha(s) inválida(s) ignorada(s).");
        pdf.Page();pdf.Text(42,755,"Solo e luz no período",18,true);pdf.Text(42,726,"Verde: solo  |  dourado: luz  |  azul: rega",10);
        double left=55,right=552,bottom=300,top=675;var first=data.Days.Count>0?data.Days.First().Date.ToUniversalTime():now;var end=data.Days.Count>0?data.Days.Last().Date.AddDays(1).ToUniversalTime():now.AddDays(1);double span=Math.Max(1,(end-first).TotalSeconds);
        Func<DateTime,double> x=t=>left+(right-left)*(t-first).TotalSeconds/span;Func<double,double> yy=n=>bottom+(top-bottom)*n/100;
        for(int i=0;i<=4;i++){double h=yy(i*25);pdf.Line(left,h,right,h,"0.85 0.85 0.85");pdf.Text(28,h-3,(i*25).ToString(),9);var date=first.AddSeconds(span*i/4);pdf.Text(Math.Min(518,Math.Max(42,x(date)-15)),bottom-20,date.ToLocalTime().ToString("dd/MM"),9);}
        HistoryPoint previous=null;foreach(var point in data.Points){if(previous!=null&&previous.Segment==point.Segment){if(previous.Soil.HasValue&&point.Soil.HasValue)pdf.Line(x(previous.Utc),yy(previous.Soil.Value),x(point.Utc),yy(point.Soil.Value),"0.14 0.44 0.30");if(previous.Light.HasValue&&point.Light.HasValue)pdf.Line(x(previous.Utc),yy(previous.Light.Value),x(point.Utc),yy(point.Light.Value),"0.7 0.45 0.08");}else{if(point.Soil.HasValue)pdf.Line(x(point.Utc),yy(point.Soil.Value),x(point.Utc)+1,yy(point.Soil.Value),"0.14 0.44 0.30");if(point.Light.HasValue)pdf.Line(x(point.Utc),yy(point.Light.Value),x(point.Utc)+1,yy(point.Light.Value),"0.7 0.45 0.08");}previous=point;}
        foreach(var r in data.Waterings)pdf.Line(x(r.FinishedUtc),bottom,x(r.FinishedUtc),top,"0.2 0.45 0.7");
        pdf.Paragraph(245,data.Points.Count==0?"Sem medições reais no período selecionado. Nenhuma curva foi presumida.":"A curva usa o último ponto de cada trecho contínuo de cinco minutos. A tabela diária considera todas as leituras válidas. Gráfico em índice relativo de 0 a 100.");
        pdf.Page();pdf.Text(42,755,"Medições por dia",18,true);pdf.Text(42,725,data.Simulated?"Dia          Solo mín.–máx.    Último solo    Regas    mL registrados  Sem cobertura":"Dia          Solo mín.–máx.    Último solo    Regas    mL medidos    Sem cobertura",10,true);y=703;
        foreach(var day in data.Days){pdf.Text(42,y,day.Date.ToString("dd/MM"),9);pdf.Text(106,y,day.SoilMin.HasValue?Num(day.SoilMin)+"–"+Num(day.SoilMax)+"%":"Sem índice",9);pdf.Text(218,y,day.LastSoil.HasValue?Num(day.LastSoil)+"%":"—",9);pdf.Text(300,y,day.Waterings.ToString(),9);pdf.Text(355,y,day.KnownMl+(day.UnknownVolumes>0?" + ?":""),9);pdf.Text(455,y,Math.Round(day.MissingMinutes)+" min",9);y-=18;}
        pdf.Paragraph(y-15,"? indica rega com volume desconhecido. Uma linha sem índice pode conter leituras brutas sem calibração. Horários seguem o fuso atual deste computador.");
        pdf.Page();pdf.Text(42,755,"Regas registradas",18,true);y=723;
        if(data.Waterings.Count==0)pdf.Text(42,y,"Nenhuma rega registrada no período.");
        foreach(var r in data.Waterings.OrderBy(r=>r.FinishedUtc)){
            if(y<80){pdf.Page();pdf.Text(42,755,"Regas registradas (continuação)",18,true);y=720;}
            y=pdf.Paragraph(y,r.FinishedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")+" | "+(r.VolumeUnknown?"Volume não informado":r.TotalMl+" mL")+" | "+(r.Origin??"Manual")+" | Drenagem: "+(r.DrainageConfirmed?"confirmada":"não confirmada"));
        }pdf.Save(path);
    }
}
}
