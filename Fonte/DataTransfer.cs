using System;using System.Collections.Generic;using System.IO;using System.IO.Compression;using System.Linq;using System.Security.Cryptography;using System.Text;
namespace Raiz {
public class TransferData {
    public int Version {get;set;}public DateTime CreatedUtc {get;set;}public Settings Settings {get;set;}public string PerenualKey {get;set;}public TelegramSecrets Telegram {get;set;}public WhatsAppSecrets WhatsApp {get;set;}public byte[] History;
}
public static class DataTransfer {
    const int Iterations=180000,MaxBytes=100000000;
    static readonly byte[] Magic=Encoding.ASCII.GetBytes("RAIZPACK01");
    public static void Password(string value){if(value==null||value.Length<12||value.Length>256)throw new ApiException("Use uma senha entre 12 e 256 caracteres. Ela será necessária no outro computador.");}
    static byte[] Random(int count){var b=new byte[count];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(b);return b;}
    static byte[] Derive(string password,byte[] salt){using(var kdf=new Rfc2898DeriveBytes(password,salt,Iterations,HashAlgorithmName.SHA256))return kdf.GetBytes(64);}
    public static void Export(Store store,string path,string password){
        Password(password);if(store.LoadWarning!=null)throw new ApiException("Confira a configuração local antes de transferir; ela não pôde ser lida.");
        var data=new TransferData{Version=1,CreatedUtc=DateTime.UtcNow,Settings=Json.Read<Settings>(Json.Write(store.Settings)),PerenualKey=store.LoadKey(),Telegram=store.LoadTelegramSecrets(),WhatsApp=store.LoadWhatsAppSecrets()};
        data.Settings.Weather=null;
        if(File.Exists(store.HistoryPath)&&new FileInfo(store.HistoryPath).Length>MaxBytes)throw new ApiException("Histórico acima de 100 MB. Preserve a pasta de dados; esta transferência aceita até 100 MB.");
        byte[] zip;
        using(var buffer=new MemoryStream()){
            using(var archive=new ZipArchive(buffer,ZipArchiveMode.Create,true)){
                var entry=archive.CreateEntry("dados.json",CompressionLevel.Optimal);using(var w=new StreamWriter(entry.Open(),new UTF8Encoding(false)))w.Write(Json.Write(data));
                if(File.Exists(store.HistoryPath)){entry=archive.CreateEntry("historico.csv",CompressionLevel.Optimal);using(var source=new FileStream(store.HistoryPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var dest=entry.Open())source.CopyTo(dest);}
            }zip=buffer.ToArray();
        }
        if(zip.Length>MaxBytes)throw new ApiException("Arquivo maior que o limite de transferência.");
        var salt=Random(16);var iv=Random(16);var keys=Derive(password,salt);byte[] cipher;
        try{
            using(var aes=Aes.Create()){aes.Key=keys.Take(32).ToArray();aes.IV=iv;aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;using(var encrypt=aes.CreateEncryptor())cipher=encrypt.TransformFinalBlock(zip,0,zip.Length);}
            using(var result=new MemoryStream()){result.Write(Magic,0,Magic.Length);result.Write(salt,0,16);result.Write(iv,0,16);result.Write(cipher,0,cipher.Length);var signed=result.ToArray();using(var mac=new HMACSHA256(keys.Skip(32).ToArray())){var tag=mac.ComputeHash(signed);result.Write(tag,0,tag.Length);}AtomicWrite(path,result.ToArray());}
        }finally{Array.Clear(keys,0,keys.Length);Array.Clear(zip,0,zip.Length);}
    }
    public static TransferData Read(string path,string password){
        Password(password);long length=new FileInfo(path).Length;if(length<90||length>MaxBytes+1024)throw new ApiException("Arquivo de transferência inválido ou maior que 100 MB.");var bytes=File.ReadAllBytes(path);
        if(!bytes.Take(Magic.Length).SequenceEqual(Magic))throw new ApiException("Este arquivo não é uma transferência Raiz compatível.");
        var salt=bytes.Skip(Magic.Length).Take(16).ToArray();var iv=bytes.Skip(Magic.Length+16).Take(16).ToArray();var keys=Derive(password,salt);byte[] zip=null;
        try{
            using(var mac=new HMACSHA256(keys.Skip(32).ToArray())){var expected=mac.ComputeHash(bytes,0,bytes.Length-32);int diff=0;for(int i=0;i<32;i++)diff|=expected[i]^bytes[bytes.Length-32+i];if(diff!=0)throw new ApiException("Senha incorreta ou arquivo alterado. Nenhum dado foi importado.");}
            using(var aes=Aes.Create()){aes.Key=keys.Take(32).ToArray();aes.IV=iv;aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;using(var decrypt=aes.CreateDecryptor())zip=decrypt.TransformFinalBlock(bytes,Magic.Length+32,bytes.Length-Magic.Length-64);}
            using(var buffer=new MemoryStream(zip))using(var archive=new ZipArchive(buffer,ZipArchiveMode.Read)){
                if(archive.Entries.Count>2||archive.Entries.Any(e=>e.FullName!="dados.json"&&e.FullName!="historico.csv")||archive.Entries.GroupBy(e=>e.FullName).Any(g=>g.Count()>1))throw new ApiException("Conteúdo inesperado no arquivo.");
                var entry=archive.GetEntry("dados.json");if(entry==null||entry.Length>4000000)throw new ApiException("Configuração de transferência inválida.");TransferData data;
                using(var reader=new StreamReader(entry.Open(),Encoding.UTF8))data=Json.Read<TransferData>(reader.ReadToEnd());Validate(data);
                entry=archive.GetEntry("historico.csv");if(entry!=null){if(entry.Length>MaxBytes)throw new ApiException("Histórico maior que o permitido.");using(var stream=entry.Open())using(var ms=new MemoryStream()){stream.CopyTo(ms);data.History=ms.ToArray();}}
                return data;
            }
        }catch(ApiException){throw;}catch{throw new ApiException("Não foi possível ler a transferência. Nenhum dado foi importado.");}
        finally{Array.Clear(keys,0,keys.Length);if(zip!=null)Array.Clear(zip,0,zip.Length);}
    }
    public static void Validate(TransferData data){
        if(data==null||data.Version!=1||data.Settings==null||data.Settings.Plants==null||data.Settings.Calibration==null||data.Settings.Plants.Count>1000)throw new ApiException("Configuração incompatível.");
        var s=data.Settings;var ids=s.Plants.Select(p=>p==null?null:p.LocalId).ToList();if(ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct().Count()!=ids.Count||(s.ActiveId!=null&&!ids.Contains(s.ActiveId)))throw new ApiException("Cadastros de plantas inválidos.");
        var c=s.Calibration;foreach(var pair in new[]{new[]{c.Dry,c.Wet},new[]{c.Dark,c.Bright}})if(pair[0].HasValue!=pair[1].HasValue||pair.Any(x=>x.HasValue&&(x<0||x>1023))||(pair[0].HasValue&&Math.Abs(pair[0].Value-pair[1].Value)<30))throw new ApiException("Calibração inválida na transferência.");
        if(data.PerenualKey!=null&&(data.PerenualKey.Length>2048||data.PerenualKey.Contains("\n")||data.PerenualKey.Contains("\r")))throw new ApiException("Chave de plantas inválida.");
        if(data.Telegram!=null&&!string.IsNullOrEmpty(data.Telegram.Token)){TelegramRules.ValidateToken(data.Telegram.Token);if(data.Telegram.Paired&&data.Telegram.UserId!=data.Telegram.ChatId)throw new ApiException("Vínculo inválido do bot.");}
        foreach(var p in s.Plants)if(p.Care==null||p.Care.Waterings==null)throw new ApiException("Histórico de regas inválido.");
    }
    public static string ImportToNewFolder(TransferData data,string parent){
        Validate(data);var copy=Json.Read<TransferData>(Json.Write(new TransferData{Version=data.Version,CreatedUtc=data.CreatedUtc,Settings=data.Settings,PerenualKey=data.PerenualKey,Telegram=data.Telegram,WhatsApp=data.WhatsApp}));
        var s=copy.Settings;s.SkipInitialCredentialImport=true;s.Weather=null;s.Port="";s.SetupDismissed=false;s.CalibrationChanges=s.CalibrationChanges??new List<DateTime>();s.CalibrationChanges.Add(DateTime.UtcNow);
        s.Telegram=s.Telegram??new TelegramOptions();s.Telegram.Enabled=false;s.Telegram.PollEnabled=false;if(copy.Telegram!=null&&!string.IsNullOrEmpty(copy.Telegram.Token))s.Telegram.BotFingerprint=WhatsAppRules.Hash(copy.Telegram.Token);
        s.WhatsApp=s.WhatsApp??new WhatsAppOptions();s.WhatsApp.Enabled=false;s.WhatsApp.ReceiverEnabled=false;s.WhatsApp.VerifiedConfig=null;s.WhatsApp.VerifiedUtc=null;
        s.Notifications=s.Notifications??new List<NotificationEvent>();foreach(var n in s.Notifications){n.ClosedUtc=n.ClosedUtc??DateTime.UtcNow;n.State="resolved";n.Detail="Evento preservado na importação; nenhum envio será retomado.";}
        foreach(var p in s.Plants){p.Care.Session=null;p.Care.LightReferenceRaw=null;p.Care.LightReferenceUtc=null;p.Care.LearningResetUtc=DateTime.UtcNow;}
        string folder=Path.Combine(parent,"Raiz-importado-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
        try{
            File.WriteAllText(Path.Combine(folder,"configuracao.json"),Json.Write(s),new UTF8Encoding(false));var target=new Store(folder);if(target.LoadWarning!=null)throw new ApiException("Não foi possível preparar os dados importados.");
            if(data.History!=null)File.WriteAllBytes(target.HistoryPath,data.History);target.Save();
            if(!string.IsNullOrEmpty(copy.PerenualKey))target.SaveKey(copy.PerenualKey);if(copy.Telegram!=null)target.SaveTelegramSecrets(copy.Telegram);if(copy.WhatsApp!=null)target.SaveWhatsAppSecrets(copy.WhatsApp);
            return folder;
        }catch{File.WriteAllText(Path.Combine(folder,"IMPORTACAO_INCOMPLETA.txt"),"Não ative esta pasta. A pasta original foi preservada.");throw;}
    }
    public static void AtomicWrite(string path,byte[] bytes){var tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None))f.Write(bytes,0,bytes.Length);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
}
}
