# Raiz

Aplicativo para Windows que reúne sensores de solo e luz do Arduino, orientações da espécie pela Perenual, clima da região e avisos de rega pelo Telegram.

## Abrir em outro computador

1. Baixe o ZIP deste repositório privado em **Code → Download ZIP**, ou clone o repositório.
2. Extraia a pasta inteira para um local gravável e abra **Raiz.exe**.
3. A configuração inicial incluída importa as chaves de API e o vínculo do Telegram automaticamente. As credenciais de uso local são protegidas para o novo usuário Windows.
4. Feche o Raiz no outro computador antes de usar o mesmo bot: apenas uma instalação deve consultar suas respostas por vez.
5. Conecte o Arduino em **Sensores**. Porta USB e calibração pertencem à montagem de cada dispositivo; os históricos antigos não são distribuídos neste repositório.

Requisitos: Windows 10/11, .NET Framework 4.8 e driver USB da placa para os sensores. Não precisa instalar Python ou Node.

## Configuração privada incluída

O arquivo **configuracao-inicial.json contém credenciais reais em texto legível**, incluídas por solicitação do proprietário para evitar cadastrá-las novamente em outros dispositivos. **Mantenha este repositório privado e limite os colaboradores.** Qualquer pessoa com acesso ao repositório ou ao ZIP pode usar essas chaves e o token do bot. Não publique esse arquivo, o ZIP ou cópias do histórico Git. Para compartilhar o projeto publicamente, retire as credenciais e revogue os tokens que já estiverem no histórico.

Na primeira abertura, o app importa somente credenciais ausentes. Chaves existentes, calibração, plantas, registros e offset do Telegram são preservados. Depois da importação, a pasta local `Dados` usa a proteção do Windows; o arquivo inicial do repositório continua legível para permitir a instalação nos outros dispositivos. Não há envio das credenciais ao GitHub pelo aplicativo.

Credenciais do WhatsApp, quando presentes, são importadas, mas seu envio e receptor ficam desativados até conferir o endereço HTTPS no novo dispositivo. O bot Telegram conserva seu vínculo, sem precisar gerar outro código.

## Recursos

- Solo e luz em tempo real, com plotter serial permanente.
- Orientações por espécie e presets locais de secagem, sem inventar dose ideal em mL ou luz em lux.
- Regas manuais e aprendizado a partir de volumes medidos com drenagem observada.
- Telegram com botão **Já reguei**, logs e indicador de conexão.
- Busca de cidade com estado e país, para o clima externo.

## Compilar e testar

No PowerShell, a partir desta pasta:

```powershell
.\Fonte\build.ps1
.\Fonte\testar.ps1
```

A compilação usa o compilador incluído no .NET Framework do Windows. Os testes usam dados fictícios e APIs simuladas; alguns testes do receptor exigem permitir conexões ao próprio computador. Não enviam mensagens reais. A suíte não executa o fluxo de abertura do aplicativo nem importa as credenciais reais deste repositório.

Mais informações: [guia de uso](LEIA-ME.md), [Telegram](TELEGRAM.md) e [validação](VALIDACAO.md).
