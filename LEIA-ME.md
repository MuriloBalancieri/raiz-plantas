# Uso desta cópia privada

Esta cópia foi preparada para o GitHub privado. O arquivo `configuracao-inicial.json` contém as chaves solicitadas pelo proprietário e é importado na primeira abertura em cada usuário Windows. Leia o README.md antes de compartilhar ou copiar o pacote. Esta cópia não inclui `dados-local.txt`: em cada instalação, a pasta Dados é criada ao lado do executável.

# Raiz — monitor de plantas para Windows

Os seis novos recursos da versão 2.0 estão descritos em [NOVO-2.0.md](NOVO-2.0.md).

Versão 2.0 · 29/10/2026

Aplicativo local que reúne solo e luminosidade do Arduino, cuidados da espécie e clima externo da cidade. A rega é manual.

## Novo na 1.7: conexão do bot e troca de cidade

Em **Avisos**, a faixa no topo mostra **Bot conectado**, **Bot desconectado**, **Conectando ao bot** ou **Recepção pausada**. Conectado exige uma resposta autenticada recente do Telegram nesta execução do app; ter um token ou vínculo salvo não basta. A faixa mostra a hora da última resposta e sai do estado conectado após 75 segundos sem confirmação recente, ou imediatamente quando a consulta de respostas falha. Pausar apenas os avisos de rega mantém a recepção ativa para registrar confirmações de mensagens já enviadas. O estado da conexão não comprova que uma mensagem foi entregue ou lida no celular.

A tela **Clima da região** mostra a localização atual e uma busca pelo nome da cidade. Os resultados exibem cidade, estado e país em linhas separadas. Nenhum resultado é escolhido automaticamente: selecione a cidade e clique em **Usar esta cidade**. A busca também funciona com Enter, mostra carregamento, ausência de resultados e erros na própria tela. A troca guarda a cidade e consulta o clima, sem alterar a calibração. Acesse por **Hoje → Clima da região → Mudar cidade** ou **Configurações → Localização do clima**.

Em **Sensores**, a confirmação de remoção do DHT e sua exigência para conectar foram removidas. Selecione a porta e clique em **Conectar**.

## Recursos da 1.5: interface mais simples

- **Hoje:** destaca solo e luz, as ações principais e três orientações atuais. Clima externo fica em uma seção que pode ser aberta. O plotter permanece visível.
- **Cuidados:** mostra primeiro o estado do solo, a orientação de água e a iluminação, com Já reguei no topo. Rega guiada, ajustes de luz e adaptação pela API ficam em seções secundárias. Uma sessão de rega em andamento abre a seção guiada ao entrar na tela.
- **Avisos:** mostra o estado do Telegram e os botões de ativar, pausar e testar. Depois de vincular, a configuração do bot fica recolhida. Antes do vínculo, fica aberta. Histórico dos avisos, explicações e a configuração anterior do WhatsApp continuam acessíveis em seções próprias.
- **Minha planta e Configurações:** informações técnicas, faixas, descrição, fontes e manutenção ficam recolhidas. O cadastro da chave da API fica aberto na primeira configuração; com chave já cadastrada, fica em uma seção secundária.
- **Layout:** botões maiores, espaçamento entre campos e ações, fileiras que crescem quando os botões quebram e cartões que ajustam a altura ao texto. Cada página usa a rolagem principal, evitando a lista interna de avisos com rolagem própria na tela inicial.

Os nomes da navegação foram encurtados: Hoje, Minha planta, Cuidados, Avisos, Sensores, Histórico e Configurações. A cidade pode ser alterada em Hoje → Clima da região → Mudar cidade, ou em Configurações → Localização do clima. As funções de sensores, cuidados por espécie, logs, Telegram e plotter continuam disponíveis.

Feche o Raiz anterior e abra o executável da pasta Raiz-GitHub. A referência aos seus dados originais foi preservada; não precisa cadastrar o bot ou recalibrar novamente.

## Recursos da 1.4: cuidados adaptados à espécie da API

Em **Minha planta** ou **Cuidados e rega**, a opção **Adaptar cuidados à espécie pela API** vem habilitada. O app usa a categoria de rega e a exposição solar retornadas pela Perenual para a espécie selecionada. Os perfis importados antigos também aproveitam as categorias já salvas. A consulta é renovada automaticamente quando o cadastro está sem metadados ou tem mais de 24 horas; há o botão **Atualizar cuidados pela API**. Se a consulta falhar, o perfil salvo continua disponível e há espera de 30 minutos para nova tentativa por planta. Sem chave, configure a Perenual primeiro. Durante uma sessão de rega ou demonstração, o app não atualiza o perfil.

**Água:** categorias `minimum`, `average` e `frequent` selecionam, respectivamente, presets operacionais de aviso em 10%, 25% e 40% do índice relativo calibrado. Esses três números são aproximações experimentais do app para diferenciar a secagem; **não são umidades ideais fornecidas pela API**. A necessidade deve ser conferida no substrato. Categoria `none` suspende a sugestão automática; categoria desconhecida não recebe preset. A jade também mantém seu perfil anterior caso a fonte não tenha categoria reconhecida. Faixa manual e ajuste local explícito têm prioridade. Em **Ajustar aviso de solo seco → Usar preset da espécie**, você pode remover o ajuste local. Nenhuma categoria manda regar em solo úmido, sem leitura atual ou por um único pico.

O intervalo `watering_general_benchmark`, quando a API realmente o fornece em dias, aparece como referência para conferir a planta. O vencimento desse intervalo não autoriza regar sozinho. **A API consultada não informa uma dose total em mL para este vaso.** A quantidade continua sendo aprendida com volumes medidos e drenagem observada, sem transformar frequência em volume. Sem aprendizado, a porção de 30 mL continua sendo apenas experimental, com total desconhecido.

**Luz:** a orientação muda automaticamente entre sol pleno, meia-sombra, sombra ou alternativas que a API listar. Ao registrar uma referência, o app mostra a exposição exigida pela espécie. A referência fica vinculada à categoria da planta; se uma atualização mudar a exposição, ela precisa ser renovada. Faixas manuais têm prioridade. O LDR continua comparando com um local apropriado que você confirmou: categorias da API não podem ser convertidas diretamente em lux ou porcentagem ideal do sensor. Não são inventadas horas diárias de sol.

A consulta real da jade nesta atualização retornou `minimum` e `full sun`, sem intervalo em dias. Seus arquivos reais não foram modificados durante essa verificação. A atualização mantém Telegram, plotter, calibração e histórico.

Referência: [documentação oficial da Perenual](https://www.perenual.com/docs/api).

## Recursos da 1.3: Telegram com Já reguei

Instale o Telegram e crie um bot exclusivo no @BotFather usando `/newbot`. No Raiz, abra **Telegram e avisos**, cole o token no campo protegido e envie ao seu bot o comando de vínculo mostrado na tela. Não é necessário procurar chat ID. Depois envie um teste e ative os avisos. [Passo a passo em TELEGRAM.md](TELEGRAM.md).

O botão **Já reguei** fica na mensagem e registra uma única confirmação nos logs. Sem volume medido, a quantidade permanece não informada. O computador precisa ficar ligado, com o app e a internet ativos. Esta integração consulta as respostas diretamente no Telegram, sem túnel ou servidor público. O log usa a hora de recebimento no app, pois o botão não fornece o horário exato do clique.

Ativar Telegram pausa os envios automáticos pelo WhatsApp, preservando a configuração anterior. Token e conta vinculada ficam protegidos por este usuário Windows. Nenhuma mensagem real ao celular foi enviada durante os testes; faça o teste incluído no app depois de vincular sua conta.

## Recursos da 1.2: WhatsApp, Já reguei e plotter permanente

- **Plotter serial fixo:** aparece abaixo de todas as telas. Mostra as curvas de solo e luz, com seleção de séries, valores brutos ou índices relativos, janelas de 30 segundos a 1 hora, pausa do desenho, consulta com o mouse e botão **Ampliar** para uma janela independente. A mesma conexão USB alimenta painel e plotter; não abra outro leitor da porta. A coleta e o CSV continuam enquanto o desenho está pausado. Intervalos de desconexão aparecem como lacunas. Ao trocar a planta ou recalibrar, a visualização é reiniciada para não misturar referências.
- **Já reguei:** botão no app e, com a API configurada, na mensagem do Telegram ou WhatsApp. A confirmação registra data/hora, planta, origem e quantidade quando informada. Volume desconhecido fica como não informado, sem ensinar uma dose fictícia ao estimador.
- **Logs de rega:** abra Histórico para consultar e exportar `regas.csv`. O CSV de medições permanece separado e preservado.
- **WhatsApp oficial:** abra Telegram e avisos → WhatsApp (opção anterior). O envio vem desativado e depende de token, Phone Number ID, App Secret, modelo aprovado com resposta rápida e webhook HTTPS público. O receptor está incluído no app, escuta somente no próprio computador e valida as assinaturas da Meta. Há um aviso por ciclo de secagem, sem repetição a cada amostra. [Veja o guia WHATSAPP.md](WHATSAPP.md).

Os testes usaram API simulada e receptor HTTP real em loopback. Nenhuma mensagem real foi enviada ao celular e nenhum túnel público foi criado nesta atualização. O computador, app, internet e encaminhamento HTTPS precisam permanecer ativos para o botão chegar aos logs.

## Novo: umidade, água e iluminação

Abra **Cuidados e rega**. Com a jade selecionada e leituras reais atuais, o app indica:

- **Solo úmido — não precisa regar** ou **Umidade adequada para aguardar** durante a secagem.
- **Precisa regar? Confira o substrato** quando o sensor permanecer no gatilho local da espécie por 30 segundos. Confira o substrato com o toque antes da rega.
- **Água agora: 0 mL** quando não há indicação de rega.
- **Iluminação adequada pela referência**, variação passageira ou leitura abaixo da referência local.

Para a jade, sem faixa manual de solo, o aviso inicial usa **10% do índice relativo** como aproximação operacional de “perto da referência seca”. Esse número não é um teor de água ideal da jade e não veio da Perenual. Use **Ajustar aviso de solo seco** para refiná-lo após observar o substrato. Faixas manuais já cadastradas têm prioridade. O sensor fora das referências ou sem leitura atual suspende a recomendação. Depois de regar, há uma pausa operacional de 30 minutos antes de avaliar outra rega.

### Estimativa de água sem tamanho do vaso

Não precisa informar diâmetro ou litros. Sem histórico, o app não tem como saber o total necessário: ele sugere **uma primeira porção experimental de 30 mL**, que não é a dose total e não vem de uma tabela agronômica. Aplique lentamente, observe o substrato e pare ao começar a drenar pelos furos. Se o sensor já subiu perto da referência úmida, pause e confira antes de colocar mais água.

Para o app aprender a quantidade deste vaso:

1. Quando houver indicação pelo gatilho da espécie, confira a terra e clique em **Iniciar rega guiada**.
2. Em **Registrar porção aplicada**, informe apenas o volume que realmente colocou. O campo começa em 30 mL, mas pode ser alterado para o que foi medido. Registrar uma porção não significa receber ordem para aplicar outra.
3. Ao observar o início da drenagem, pare de regar e clique em **Começou a drenar: finalizar**. Esvazie o pratinho. Se o sensor ainda não respondeu, aguarde a distribuição da água; não continue regando apenas para aumentar o índice.
4. Nas próximas regas a estimativa será a mediana dos volumes das últimas cinco sessões válidas deste vaso, iniciadas perto da referência seca e finalizadas com drenagem e resposta do sensor. Uma única sessão permite uma primeira estimativa, ainda aproximada. Pare ao iniciar a drenagem mesmo se o volume estimado não tiver sido atingido.

**Encerrar sem aprender** guarda o volume informado, mas não usa essa sessão para estimar doses. Se trocar vaso/substrato, use **Troquei o vaso ou substrato** e recalibre. Os registros anteriores são preservados; mudar as referências do sensor também invalida o aprendizado anterior. Nenhum volume é calculado a partir de uma falsa equivalência entre índice relativo e mL.

### Referência simples de luz

Com o LDR ao lado da planta, em um local cuja iluminação você confirmou como apropriada, clique em **Registrar referência de luz** durante o dia e descreva o local. Uma referência escuro/claro calibra o sensor, mas não comprova sozinha a iluminação ideal da espécie.

O app compara a leitura com o ponto validado, com tolerância operacional de 20%. Uma redução precisa persistir por 60 segundos para gerar aviso. À noite, ou sem contexto diurno atualizado, a avaliação fica pausada. Uma faixa de luz manual cadastrada tem prioridade. Recalibrar o LDR exige renovar a referência.

“Adequada pela referência” significa apenas que o ponto está semelhante ao que você registrou: não comprova lux, intensidade ideal absoluta ou duração diária suficiente. O perfil da jade usa a orientação qualitativa de local bem iluminado e secagem entre regas; não inventa conversão da Perenual para porcentagem do LDR.

## Abrir

**Cópia do repositório privado:** extraia a pasta inteira e abra Raiz.exe. As chaves são importadas do arquivo inicial. Cada instalação cria seus próprios dados locais; históricos e calibração do computador anterior não são incluídos. Para continuar usando seus dados atuais neste computador, mantenha a instalação já configurada até migrar os dados com ambos os aplicativos fechados.

**Pacote portátil / outro local:** o ZIP não contém dados, chaves ou referência ao seu diretório pessoal. Após extrair, o app cria sua própria pasta Dados. Para uma migração, copie os dados somente com as versões fechadas; chaves protegidas devem ser cadastradas novamente se mudar o usuário Windows.

1. Extraia o ZIP inteiro para uma pasta gravável, como Documentos. Se você já está na pasta extraída, não precisa extrair de novo.
2. Abra **Raiz.exe**. Não é necessário instalar Python nem Node.
3. Na primeira abertura, o app consulta **Guarulhos, São Paulo, Brasil**, cidade solicitada. Você pode mudar em **Clima da região**.
4. Para conhecer a interface sem ligar o circuito, clique em **Ver demonstração**. Solo e luz serão fictícios e identificados como tal; não entram no histórico, não enviam mensagens ao celular e não registram regas reais. O clima continua sendo real, quando disponível.

Requisitos: Windows 10/11 com .NET Framework 4.8, porta USB e driver da placa para usar o Arduino. O executável foi testado neste computador; outras máquinas e seus drivers não foram testados. O programa não solicita privilégios de administrador.

## Segurança da montagem atual

O DHT22 anterior aqueceu e deformou. **Não reutilize esse sensor.** Remova seus conectores com o USB desligado. Não reconecte a montagem antes de conferir alimentação e GND. Apoie o Arduino em base isolante, sem contato dos terminais com o gabinete metálico.

O app não envia firmware automaticamente nem abre portas seriais sozinho. A opção **Conectar** abre a porta selecionada sem confirmação adicional de remoção do DHT.

## Escolher a planta pela API

1. Abra **Configurações → Abrir cadastro da Perenual**.
2. Crie a conta/chave diretamente no site do fornecedor. O app não cria conta nem contrata plano. Verifique as condições e as espécies liberadas no plano disponível.
3. Cole a chave no campo protegido do app e clique em **Guardar chave**. Não envie a chave pelo chat.
4. Abra **Minha planta → Buscar na API**. Prefira o nome científico, como `Monstera deliciosa`, porque os nomes comuns da base podem estar em inglês.
5. Selecione uma espécie e clique em **Importar espécie**. O app consulta os detalhes, mostra a origem e guarda as orientações localmente. No cadastro atual já existe uma jade importada, com a calibração de solo seco 392 e úmido 281; o pacote para distribuição não inclui seus dados pessoais.

A busca consulta a primeira página, com até 30 resultados. Refine o nome se precisar. Sem chave, você pode criar um **Perfil manual**, configurar faixas com uma referência própria e usar clima/sensores normalmente. Uma espécie importada permanece disponível sem internet. A atualização 1.1 preserva a chave já cadastrada e a jade importada; não executou nova busca autenticada.

## O que a API sabe e o que precisa ser validado

A Perenual é usada para identidade da espécie e orientações de rega, exposição solar e substrato, quando disponíveis. O app traduz categorias comuns; descrições extensas e termos sem tradução permanecem no idioma original.

**Não há garantia de faixas numéricas ideais de temperatura, umidade do ar ou umidade do substrato para cada espécie.** O app deixa essas faixas vazias. Não converte rega “moderada” em uma porcentagem inventada, rusticidade em temperatura ideal nem temperatura de rega em faixa térmica da planta.

Em **Editar planta e faixas**, cadastre apenas limites que você validou para o vaso e informe a fonte ou observação. Os limites de solo e luz são índices relativos da sua calibração. Cada planta cadastrada guarda suas próprias faixas. Um par vazio suspende aquela avaliação. Os limites são do perfil cadastrado, não dados supostamente fornecidos pela API.

Use o seletor em **Minha planta** para trocar o vaso ativo. Há um Arduino e um par de sensores por vez; o app não identifica automaticamente qual vaso recebeu o sensor. Ao trocar a planta, a leitura anterior e o gráfico da sessão são limpos; aguarde novas amostras.

## Clima regional

A Open-Meteo fornece temperatura e umidade externas, além do contexto de dia/noite. O painel mostra o horário do modelo e a última consulta. A atualização ocorre a cada 15 minutos enquanto o app está aberto.

O clima da cidade **não mede o ar dentro de casa**. Em perfis internos, ele serve como contexto. Só habilite **Externo** se essa comparação regional fizer sentido para o vaso; ainda será uma estimativa da região, não um sensor junto à planta.

Sem internet, o último clima fica visível com seu horário. A comparação é suspensa quando a consulta tem 45 minutos ou o horário do modelo tem 60 minutos. Dados ausentes não viram zero. Para a luz, a avaliação é pausada à noite ou quando falta contexto diurno atualizado. Não há cálculo de horas diárias de exposição nesta versão.

## Arduino sem DHT

O arquivo está em **Arduino/Sensores_Planta/Sensores_Planta.ino**. Ele lê apenas:

| Item | Ligação |
|---|---|
| Sensor de solo AOUT | A1 |
| Sensor de solo VCC | 3.3V, conforme o sensor atual compatível |
| Sensor de solo GND | GND comum |
| LDR, uma perna | 5V |
| LDR, outra perna | A0 e uma ponta de resistor de 10 kΩ |
| Outra ponta do resistor | GND |

Não una 3.3V a 5V. Confira VCC/GND/AOUT no próprio sensor. O DHT e seu resistor de pull-up não são necessários nessa montagem.

Depois de conferir a montagem com a alimentação desligada:

1. Abra o sketch incluído na Arduino IDE, selecione Arduino Uno e a porta correta.
2. Envie o sketch pela IDE. Ele não precisa de bibliotecas externas.
3. **Feche o Monitor Serial e o Serial Plotter** antes de conectar pelo app; só um programa pode usar a porta.
4. Em **Arduino e calibração**, atualize as portas, selecione a do Uno e clique em **Conectar**.

O Arduino pode reiniciar ao abrir a porta; aguarde alguns segundos. A transmissão usa 9600 baud, uma linha JSON a cada aproximadamente 2,5 segundos. O app também entende o formato textual anterior de solo/luz e ignora linhas do DHT. O sketch antigo não foi sobrescrito nesta entrega.

Exemplo de protocolo:

```json
{"type":"sensors","seq":1,"light_raw":242,"soil_raw":557,"uptime_ms":5000}
```

O programa novo compilou para Uno, mas **não foi enviado nem testado fisicamente nesta etapa**, devido ao problema elétrico relatado.

## Calibração

Em **Arduino e calibração → Calibrar solo e luz**, use **Usar leitura atual** para cada referência:

- Solo seco: substrato representativo seco, com sensor na posição de uso.
- Solo úmido: mesmo substrato após regar e deixar drenar.
- LDR escuro: referência com pouca luz.
- LDR claro: referência iluminada do local de uso.

Cada par deve diferir em pelo menos 30 pontos brutos. O app aceita sensores cujo sinal diminui ou aumenta com a umidade. Os índices ficam entre 0 e 100; não são teor volumétrico de água, lux ou medida de fotossíntese. A calibração pertence à montagem: repita ao mudar o sensor ou o substrato. Não molhe a eletrônica do sensor capacitivo.

Uma leitura do Arduino com 12 segundos ou mais deixa de gerar avaliação. Mudanças no painel são avisos para observação e decisão de rega manual; não são diagnóstico agronômico. Esta versão não controla bomba. O aviso ao celular pode usar Telegram ou WhatsApp quando configurado. A opção principal é Telegram e avisos.

## Cruzamento das informações

O app compara os sensores calibrados com as faixas do vaso e mostra a orientação de rega da espécie. A versão 1.1 acrescenta o acompanhamento de secagem da jade e a estimativa aprendida de rega descritos acima. Para plantas sem esse perfil, são necessários limites manuais validados. O clima externo continua separado do ambiente interno; ele não aumenta a dose de uma jade dentro de casa.

## Histórico e dados privados

A pasta **Dados** contém os itens abaixo. Na instalação portátil ela é criada ao lado do executável; nesta atualização local a versão 2.0 continua usando a pasta original. Use **Histórico → Abrir pasta de dados** para localizar os arquivos.

- `configuracao.json`: plantas, cidade, calibração, limites, último clima, referência de luz e regas informadas. Alterações são salvas com uma cópia anterior `.bak`.
- `historico.csv`: leituras reais de todas as sessões, identificação do perfil ativo e contexto meteorológico. Demonstrações não são gravadas.
- `perenual.protegido`: chave criptografada com DPAPI para o usuário Windows atual.
- `telegram.protegido`: token, conta vinculada e código temporário do Telegram, protegidos com DPAPI para este usuário.
- `whatsapp.protegido`: token, App Secret, token de verificação e números do WhatsApp, protegidos com DPAPI para este usuário.
- `regas.csv`: confirmações de rega com origem, volume quando informado e indicação de drenagem confirmada ou não informada. É regenerado da configuração persistente sem duplicar linhas.

O **plotter fixo** usa a sessão/planta ativa, com até 10 mil amostras em memória e janela selecionável de até 1 hora; o CSV preserva sessões anteriores. Pausar congela só a visualização. Em **Histórico**, use **Exportar histórico CSV** para medições e **Exportar logs de rega CSV** para regas. Para Excel, importe como UTF-8 com delimitador vírgula se a abertura direta não separar as colunas.

O app envia à Open-Meteo a consulta da cidade/coordenadas escolhidas. Envia à Perenual o nome pesquisado/ID da espécie e a chave exigida pelo serviço. Se o Telegram estiver configurado e habilitado, envia ao Telegram o chat vinculado e o aviso com nome da planta, índice atual de solo e orientação de quantidade. As respostas do botão são consultadas diretamente no Telegram. Se o WhatsApp estiver configurado e habilitado, envia à Meta o destinatário e o aviso com nome da planta, índice atual de solo e orientação de quantidade. O botão retorna pela Meta e pelo túnel/proxy HTTPS que você configurar. O histórico completo fica local; o app não coleta localização precisa automaticamente nem usa telemetria.

Faça backup da pasta Dados. A chave protegida não é portátil entre usuários do Windows: cadastre-a novamente no outro computador. Não compartilhe essa pasta ao distribuir o app. O ZIP original não contém dados de uso nem credenciais.

## Código e validação

O código editável está em **Fonte**. O script `Fonte/build.ps1` recompila o executável com o compilador do .NET Framework instalado no Windows. `Fonte/Tests.cs` contém as verificações de protocolo, calibração, avaliação, persistência e proteção da chave. A pasta Fonte não é necessária para executar o aplicativo.

Veja **VALIDACAO.md** para o alcance dos testes e o que depende de hardware/chave.

## Fontes e atribuição

- Perenual: https://perenual.com/docs/api — consultar termos e limites do plano na própria conta.
- Open-Meteo: https://open-meteo.com/en/docs — dados meteorológicos por Open-Meteo, com atribuição [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
- Localidades: https://open-meteo.com/en/docs/geocoding-api — Open-Meteo / GeoNames.
- Jade, secagem e drenagem: https://yardandgarden.extension.iastate.edu/faq/how-often-should-i-water-my-jade-plant — Iowa State University.
- Jade, iluminação e cuidados: https://plants.ces.ncsu.edu/plants/crassula-ovata/ — North Carolina State University.

O acesso público de clima é destinado a uso não comercial conforme os termos do fornecedor. Para publicar um produto comercial, revise os planos e termos das APIs.
