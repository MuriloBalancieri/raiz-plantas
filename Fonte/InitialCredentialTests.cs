using System;using System.IO;using Raiz;
public static class InitialCredentialTests {
    public static void Run(Action<bool,string> check,string folder){
        var store=new Store(Path.Combine(folder,"credenciais-iniciais"));
        var bundle=new InitialCredentialBundle{PerenualKey="FAKE-PERENUAL-TEST-KEY-ONLY",Telegram=new TelegramSecrets{Token="123456789:FAKE_TELEGRAM_TOKEN_FOR_TEST_ONLY_12345",BotId=123456789,BotUsername="raiz_test_bot",ChatId=5000000001,UserId=5000000001,PairedUtc=DateTime.UtcNow,PairCode="12345678"},TelegramAlertsEnabled=true};
        check(InitialCredentials.Apply(store,bundle),"configuracao inicial importa chaves em novo dispositivo");
        check(store.LoadKey()==bundle.PerenualKey&&store.LoadTelegramSecrets().Token==bundle.Telegram.Token,"chaves importadas sao recuperadas pela protecao local Windows");
        check(store.LoadTelegramSecrets().Paired&&store.LoadTelegramSecrets().PairCode==null,"vinculo Telegram migra sem carregar codigo temporario");
        check(store.Settings.Telegram.PollEnabled&&store.Settings.Telegram.Enabled,"Telegram importado conserva avisos e ativa recepcao");
        check(!File.ReadAllText(Path.Combine(store.Folder,"configuracao.json")).Contains(bundle.Telegram.Token),"configuracao de uso local nao reproduz token em texto puro");
        check(!InitialCredentials.Apply(store,bundle),"reabrir app nao reimporta chaves nem reinicia offset");
        store.SaveKey("OUTRA-CHAVE-LOCAL-TESTE");store.Settings.Telegram.NextOffset=123;store.SaveConfiguration();InitialCredentials.Apply(store,bundle);
        check(store.LoadKey()=="OUTRA-CHAVE-LOCAL-TESTE"&&store.Settings.Telegram.NextOffset==123,"importacao preserva chaves ja configuradas e offset existente");
        bool rejected=false;try{InitialCredentials.Apply(store,new InitialCredentialBundle{Version=99});}catch(ApiException){rejected=true;}check(rejected,"formato de configuracao inicial desconhecido e recusado");
        var invalid=new InitialCredentialBundle{Telegram=new TelegramSecrets{Token="nao-e-token"}};rejected=false;try{InitialCredentials.Apply(store,invalid);}catch(ApiException){rejected=true;}check(rejected,"token invalido na configuracao inicial nao e gravado");
    }
}
