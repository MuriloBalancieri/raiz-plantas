# Raiz 2.4.1

Aplicativo para Windows que reúne sensores de solo e luz do Arduino, orientações da espécie pela Perenual, clima da região e avisos de rega pelo Telegram.

## Abrir em outro computador

1. Baixe o ZIP deste repositório privado em **Code → Download ZIP**, ou clone o repositório.
2. Extraia a pasta inteira para um local gravável e abra **Raiz.exe**.
3. A configuração inicial incluída importa as chaves de API e o vínculo do Telegram automaticamente. As credenciais de uso local são protegidas para o novo usuário Windows.
4. Feche o Raiz no outro computador antes de usar o mesmo bot: apenas uma instalação deve consultar suas respostas por vez.
5. Conecte o Arduino em **Sensores**. Porta USB e calibração pertencem à montagem de cada dispositivo; os históricos antigos não são distribuídos neste repositório.

Requisitos: Windows 10/11, .NET Framework 4.8 e driver USB da placa para os sensores. Não precisa instalar Python ou Node.

## Downloads para preparar outro computador

Os links abaixo são dos fabricantes e projetos oficiais. Não é necessário instalar todos os itens: escolha conforme o uso.

| Item | Link oficial | Quando precisa |
| --- | --- | --- |
| .NET Framework 4.8 **Runtime** | [Download da Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) | Para executar o Raiz, caso o Windows ainda não tenha uma versão compatível. Escolha **Runtime**, não Developer Pack. Se o computador já tiver 4.8 ou 4.8.1, não precisa reinstalar. |
| Arduino IDE | [Download da Arduino](https://www.arduino.cc/en/software) | Para enviar ou atualizar o programa da placa. Se o Arduino já estiver programado e reconhecido, a IDE não é necessária para abrir o Raiz. |
| Driver USB serial CH340/CH341 | [CH341SER.EXE — WCH](https://www.wch.cn/downloads/CH341SER_EXE.html) | Somente para placas que usam esse chip, caso a porta USB serial não seja reconhecida. No Gerenciador de Dispositivos, pode aparecer como **USB-SERIAL CH340**. O nome Uno sozinho não identifica o chip USB. |
| Orientações de driver da Arduino | [Placa com aviso no Gerenciador de Dispositivos](https://support.arduino.cc/hc/en-us/articles/360016420100-Windows-Device-Manager-shows-a-warning-sign-under-my-device) | Para conferir a instalação da placa e do pacote correto na IDE quando o Windows não a reconhece. Siga esta orientação para a sua placa; o driver CH340 não serve para todos os modelos. |
| Telegram no celular | [Aplicativos oficiais](https://telegram.org/apps) | Para receber o aviso e confirmar a demonstração no bot. Mantenha a mesma conta que foi vinculada ao Raiz. |
| Telegram Desktop — opcional | [Download oficial para computador](https://desktop.telegram.org/) | Apenas se quiser mostrar a conversa também na tela do computador. O Raiz não depende do Telegram Desktop para enviar mensagens ao celular. |
| Git para Windows — opcional | [Download oficial](https://git-scm.com/install/windows) | Para clonar e atualizar o repositório por comandos Git. Se usar **Code → Download ZIP**, não precisa instalar Git. |

### Conferência rápida antes da apresentação

1. Extraia ou clone a pasta inteira em um local onde possa salvar arquivos. Abra `Raiz.exe` ou `Apresentar.cmd`.
2. Para levar a mesma planta, calibração e histórico, use **Configurações → Transferir dados** na origem e importe o arquivo protegido no destino. Apenas clonar o repositório não transfere esses registros.
3. Se for usar sensores reais, conecte o Arduino com um cabo USB de dados e confira a porta **COM** no Gerenciador de Dispositivos e em **Sensores** no Raiz. A numeração pode mudar no outro computador.
4. Se precisar atualizar o programa da placa, use o arquivo `Arduino/Sensores_Planta/Sensores_Planta.ino` incluído no projeto. Depois, feche o Monitor Serial e o Plotter da IDE para liberar a porta ao Raiz.
5. Feche o Raiz no computador anterior antes de usar o mesmo bot no destino. Confira a internet e use **Enviar demonstração ao celular** na etapa 5 para verificar a chegada da mensagem e sua confirmação.
6. Para mostrar apenas o roteiro e o histórico simulado, não precisa conectar o Arduino nem instalar o driver USB. A mensagem no celular depende de internet e do bot vinculado.

Links conferidos em 06/11/2026. O app já inclui o executável; esses downloads não exigem recompilar o projeto.

## Configuração privada incluída

O arquivo **configuracao-inicial.json contém credenciais reais em texto legível**, incluídas por solicitação do proprietário para evitar cadastrá-las novamente em outros dispositivos. **Mantenha este repositório privado e limite os colaboradores.** Qualquer pessoa com acesso ao repositório ou ao ZIP pode usar essas chaves e o token do bot. Não publique esse arquivo, o ZIP ou cópias do histórico Git. Para compartilhar o projeto publicamente, retire as credenciais e revogue os tokens que já estiverem no histórico.

Na primeira abertura, o app importa somente credenciais ausentes. Chaves existentes, calibração, plantas, registros e offset do Telegram são preservados. Depois da importação, a pasta local `Dados` usa a proteção do Windows; o arquivo inicial do repositório continua legível para permitir a instalação nos outros dispositivos. Não há envio das credenciais ao GitHub pelo aplicativo.

Credenciais do WhatsApp, quando presentes, são importadas, mas seu envio e receptor ficam desativados até conferir o endereço HTTPS no novo dispositivo. O bot Telegram conserva seu vínculo, sem precisar gerar outro código.

## Novo na 2.4

O histórico simulado usa uma cópia do perfil da planta cadastrada e cobre sete dias completos anteriores à geração. O título mostra o nome da planta, com uma indicação discreta de dados simulados. A visão Semana completa reúne todo o período; selecionar a tabela permite examinar cada dia. Mostrar histórico da planta está disponível em todas as etapas do roteiro. 475 verificações locais passaram.

## Novo na 2.3

- A etapa 5 da apresentação oferece Enviar demonstração ao celular, com mensagem real de teste e confirmação pelo bot, sem criar rega.
- O Histórico alterna entre dados reais e sete dias fictícios da mostra.
- CSV, JSON e PDF fictícios ficam em uma pasta separada, com origem simulada.
- Avisos automáticos continuam pausados durante a apresentação e o histórico fictício.

## Novo na 2.2

Abra **Apresentar.cmd** para entrar direto no roteiro da mostra de conhecimento, ou escolha **Apresentação** no menu lateral. Há cinco etapas, cenários simulados, comparação com sensores reais e tela cheia por F11. Os exemplos não gravam medições ou regas, e os avisos automáticos ficam pausados nessa tela. Leia o [roteiro sugerido](ROTEIRO-DA-MOSTRA.md).

Compilação e 441 verificações locais passaram; telas foram inspecionadas com dados fictícios. A projeção no equipamento da faculdade ainda precisa de ensaio no local.

## Novo na 2.1

- Resumo de secagem observada e faixa horária de maior luz no Histórico.
- Diagnósticos de conexão, pacotes inválidos e sinais suspeitos.
- Relatórios por período em PDF e CSV.
- Transferência protegida por senha, com dados anteriores preservados.

Veja [como usar os quatro recursos](NOVO-2.1.md). Para transferir seu histórico e suas conexões com proteção, prefira o arquivo .raiz gerado pelo app.

## Novo na 2.0

- Uma recomendação principal em Hoje, com o próximo cuidado e o motivo.
- Pouca luz pelo Telegram após cinco minutos de condição diurna persistente, com intervalo entre avisos.
- Quantidade opcional e confirmação de drenagem pelo Telegram depois de Já reguei.
- Histórico diário com evolução do solo e luz, regas e lacunas de coleta.
- Assistente Primeiros passos com etapas prontas e pendentes.
- Opção de continuar perto do relógio do Windows ao fechar a janela.

Veja [como usar os seis recursos](NOVO-2.0.md). A recomendação continua usando sensores e referências locais, sem inventar dose ideal em mL ou luz ideal em lux.

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
