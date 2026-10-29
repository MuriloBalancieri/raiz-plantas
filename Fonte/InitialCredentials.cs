using System;
using System.IO;
using System.Text;

namespace Raiz {
public class InitialCredentialBundle {
    public int Version {get;set;}
    public string PerenualKey {get;set;}
    public TelegramSecrets Telegram {get;set;}
    public bool TelegramAlertsEnabled {get;set;}
    public WhatsAppSecrets WhatsApp {get;set;}
    public WhatsAppOptions WhatsAppOptions {get;set;}
    public InitialCredentialBundle(){Version=1;}
}
public static class InitialCredentials {
    public static bool ImportFile(Store store,string path){
        if(!File.Exists(path)||store.LoadWarning!=null)return false;
        if(new FileInfo(path).Length>65536)throw new ApiException("Configuração inicial maior que o permitido.");
        InitialCredentialBundle bundle;try{bundle=Json.Read<InitialCredentialBundle>(File.ReadAllText(path,Encoding.UTF8));}catch{throw new ApiException("Não foi possível ler a configuração inicial.");}
        return Apply(store,bundle);
    }
    public static bool Apply(Store store,InitialCredentialBundle bundle){
        if(bundle==null||bundle.Version!=1)throw new ApiException("Formato de configuração inicial não suportado.");
        string key=(bundle.PerenualKey??"").Trim();if(key!=""&&(key.Length<8||key.Length>2048||key.Contains("\r")||key.Contains("\n")))throw new ApiException("Chave de plantas inválida na configuração inicial.");
        var telegram=bundle.Telegram;
        if(telegram!=null&&!string.IsNullOrEmpty(telegram.Token)){
            TelegramRules.ValidateToken(telegram.Token);
            if(telegram.BotId<=0||TelegramRules.Id(telegram.BotId)!=telegram.BotId||TelegramRules.Id(telegram.ChatId)!=telegram.ChatId||TelegramRules.Id(telegram.UserId)!=telegram.UserId||(telegram.Paired&&telegram.ChatId!=telegram.UserId))throw new ApiException("Vínculo do Telegram inválido na configuração inicial.");
        }
        bool changed=false;
        if(key!=""&&string.IsNullOrEmpty(store.LoadKey())){store.SaveKey(key);changed=true;}
        var existing=store.LoadTelegramSecrets();
        if(telegram!=null&&!string.IsNullOrEmpty(telegram.Token)&&(string.IsNullOrEmpty(existing.Token)||(existing.Token==telegram.Token&&string.IsNullOrEmpty(store.Settings.Telegram.BotFingerprint)))){
            var copy=Json.Read<TelegramSecrets>(Json.Write(telegram));copy.PairCode=null;copy.PairExpiresUtc=null;copy.PairFailures=0;
            store.SaveTelegramSecrets(copy);
            store.Settings.Telegram=new TelegramOptions{Enabled=bundle.TelegramAlertsEnabled&&copy.Paired,PollEnabled=true,NextOffset=0,BotFingerprint=WhatsAppRules.Hash(copy.Token)};changed=true;
        }
        if(bundle.WhatsApp!=null&&!string.IsNullOrEmpty(bundle.WhatsApp.AccessToken)&&string.IsNullOrEmpty(store.LoadWhatsAppSecrets().AccessToken)){
            store.SaveWhatsAppSecrets(bundle.WhatsApp);
            if(bundle.WhatsAppOptions!=null){var options=Json.Read<WhatsAppOptions>(Json.Write(bundle.WhatsAppOptions));options.Enabled=false;options.ReceiverEnabled=false;options.VerifiedUtc=null;options.VerifiedConfig=null;store.Settings.WhatsApp=options;}
            changed=true;
        }
        if(changed)store.SaveConfiguration();return changed;
    }
}
}
