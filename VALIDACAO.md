# Validação — Raiz 2.0

Data: 29/10/2026. Testes locais neste Windows; não houve CI nem validação em outro computador.

## Versão 2.0 — seis recursos

- **355 verificações locais passaram** na suíte final. Sem o arquivo opcional da resposta pública da Perenual da versão anterior, a distribuição executa 354. Foram preservados os testes anteriores de sensores, cuidados, importação, Telegram, receptor local, configuração e interface.
- A recomendação principal foi testada com solo úmido, solo seco persistente, leitura ausente, demonstração e pouca luz com solo sem necessidade de água.
- Alertas de luz foram testados para persistência de cinco minutos, ciclo único, separação de rega, ausência de botão de rega, recuperação, cooldown, novo ciclo depois do intervalo, noite, período noturno anterior ao amanhecer, condição curta e desativação da opção.
- O fluxo de quantidade foi testado com primeiro clique, volume desconhecido, referência inicial, usuário indevido, mensagem incorreta, pergunta vinculada, texto sem vínculo, outro bot, valores inválidos, resposta antiga, volume medido no mesmo registro, ausência de aprendizado sem drenagem, confirmação explícita com medidas válidas, repetição, Não medi, persistência e leitura final desconhecida impedindo aprendizado. O parser de respostas conserva a mensagem e o bot de origem.
- O histórico diário foi testado com isolamento por planta, CSV com vírgulas e aspas, extremos reais, falta de coleta separando segmentos, regas medidas e não medidas, cobertura observada, dias sem dados, linha incompleta, mudança de calibração e arquivo ausente.
- O assistente foi testado por estados prontos/pendentes e incluído nas verificações de acomodação de controles em 1080 e 1240 pixels. Um teste com janela oculta confirmou que o timer e o ciclo do app continuam processando; não mostrou ícone na área de notificação nem controlou janelas reais.
- Renderizações de Hoje, Avisos, Primeiros passos e Histórico foram inspecionadas com dados fictícios, sem abrir conexão serial ou enviar mensagens reais. Os testes de Telegram usam respostas simuladas; não houve novo teste físico de luz/rega, conversa real de quantidade pelo celular ou teste prolongado de coleta em segundo plano.
- Os dados reais do usuário e o arquivo inicial privado de credenciais foram preservados. A cópia local de atualização aponta para os dados originais e exclui o arquivo de credenciais em texto puro. O repositório privado mantém esse arquivo já autorizado, sem inclusão de novos segredos em outros arquivos.
- As limitações de medição, aprendizado, histórico legado e execução em segundo plano estão descritas em NOVO-2.0.md.

## Registro da publicação privada e configuração inicial

- **302 verificações locais passaram**, incluindo nove cenários de importação de credenciais: primeira instalação, proteção local Windows, vínculo sem código temporário, recepção e avisos, ausência do token na configuração de uso local, reabertura sem reiniciar offset, preservação de chaves existentes, versão desconhecida e token inválido. Sem o arquivo opcional da resposta pública real da Perenual, a suíte portátil executa 301 verificações.
- O arquivo inicial contém as chaves reais por solicitação explícita do proprietário, para distribuição entre seus dispositivos em repositório privado. Não está protegido por DPAPI no repositório: essa proteção só é aplicada ao importar em cada usuário Windows. O README informa essa condição.
- A exportação leu as chaves locais sem exibi-las e não alterou os dados originais. A verificação local restringiu os segredos exportados ao arquivo inicial solicitado. Históricos, backups, diretório Dados, arquivos protegidos e caminhos pessoais de referência foram excluídos do repositório.
- Os testes da interface desativam explicitamente a importação inicial e usam dados fictícios, sem abrir conexões reais ou enviar mensagens. Não houve teste em outro computador Windows nem novo teste de mensagem no celular nesta publicação.

## Registro da atualização 1.7 — conclusão da troca de cidade e conexão do bot

- **293 verificações passaram** nesta execução local. A suíte portátil executa 292 sem o arquivo opcional da resposta pública da Perenual usado nas versões anteriores.
- Concluídas e validadas as mudanças iniciadas na 1.6: remoção da confirmação do DHT e do bloqueio associado; tela de clima com localização atual, busca por nome, lista com cidade/estado/país, seleção explícita e confirmação da troca.
- Testes da busca cobrem entrada vazia sem consulta, cidades homônimas, ausência de seleção automática, coordenadas da cidade escolhida, resultado vazio limpando seleção anterior e erro recuperável na tela. A página de clima foi incluída nas verificações de acomodação de ações em 1080 e 1240 pixels.
- Indicador do bot testado para configuração ausente, token salvo sem resposta atual, resposta autenticada recente, resposta antiga, erro de rede, token revogado, conflito de leitor/webhook, reconexão, recepção desativada, demonstração e reinício da verificação. O indicador usa estado em memória desta execução e não a data antiga de um evento salvo.
- A faixa de conexão é atualizada pelas consultas existentes; não foram introduzidos envios ou consultas adicionais de teste. Conectado descreve acesso recente ao Telegram e recepção ativa, não entrega ou leitura no celular.
- Renderizações isoladas das telas de bot e clima foram inspecionadas com dados fictícios. Nenhuma janela do usuário foi controlada, nenhuma mensagem real foi enviada e nenhuma porta serial foi aberta. Não houve novo teste de clima externo: a integração usa a função de consulta já existente, com testes simulados da nova interface.
- Gravação da cidade persiste somente a configuração, sem depender de reconstruir o CSV de regas; se essa gravação falhar, o estado anterior em memória é restaurado. Dados pessoais reais não foram alterados durante os testes. A versão foi compilada e preparada em pasta separada para abertura manual.

## Registro da atualização 1.5 — interface

- **271 verificações passaram** neste computador, incluindo 22 verificações de layout e interação com seções secundárias, além da suíte anterior. A distribuição executa 270 verificações quando não há o arquivo opcional da resposta pública real da espécie usado na versão 1.4.
- Testadas navegação e acomodação de ações visíveis em Hoje, Cuidados, Avisos, Minha planta, Sensores, Configurações e Histórico, com larguras de 1080 e 1240 pixels; expansão e recolhimento da rega guiada; permanência do plotter; altura de fileira com múltiplas linhas; tamanho mínimo de ações; texto longo sem sobrepor título e corpo; altura de cartão conforme largura.
- Renderizações isoladas foram inspecionadas com dados fictícios. Não foram abertas ou controladas janelas do usuário. O teste cria os controles da interface sem mostrar uma janela e não dispara o evento de abertura, consultas de API, conexão serial ou mensagens.
- Não houve teste físico adicional do Arduino, envio real ao celular ou inspeção em outras escalas de DPI/outros computadores nesta atualização. Os testes anteriores de transporte continuam usando APIs simuladas e o receptor HTTP local.
- Arquivos reais de dados e credenciais não foram copiados nem alterados. Versão preparada separadamente para abertura manual, usando a referência à pasta original. ZIP exclui a referência pessoal e arquivos privados.

| Ajuste | Antes | Depois |
| --- | --- | --- |
| Ações | Fileiras com altura fixa, cortando controles após quebra | Altura acompanha as linhas, com separação entre campos e botões |
| Textos | Títulos e corpo em posições e alturas fixas | Cartões calculam a altura necessária e quebram textos longos |
| Uso diário | Configuração, instruções e ações avançadas misturadas | Estado e ações principais primeiro; detalhes em seções secundárias |
| Navegação | Rótulos extensos e clima como tela principal no menu | Rótulos curtos e clima acessível dentro de Hoje e Configurações |
| Plotter | Controles pequenos em uma única linha fixa | Fileira pode crescer com a quebra dos controles; gráfico permanece visível |

## Registro da atualização 1.4 — adaptação por espécie

- **249 verificações passaram neste computador**, incluindo a suíte anterior de sensores, cuidados, plotter, WhatsApp e Telegram; parser das categorias da API; intervalos em dias; presets operacionais diferentes; sensor antigo, pico isolado e solo úmido; campos desconhecidos/bloqueados; prioridade dos ajustes manuais; migração e cache; renovação da referência de luz ao mudar exposição; preservação do nome, identidade e logs ao atualizar; recusa de resposta de outra espécie.
- Uma consulta autenticada real da espécie selecionada na Perenual retornou categoria `minimum` e luz `full sun`, sem intervalo de dias. A chave foi carregada no processo e não foi exibida ou salva no pacote. A resposta pública foi usada em uma verificação adicional local; a suíte distribuída executa 248 verificações sem esse arquivo opcional. A consulta não alterou os dados reais.
- Adaptação automática por categorias usa presets locais explicitamente experimentais: 10/25/40 do índice de solo, com persistência de 30 segundos. Não há alegação de que esses valores sejam ideais agronômicos da API. A dose em mL continua baseada no histórico medido; intervalo de dias não manda regar automaticamente. Nenhuma conversão de categoria de luz para lux é presumida.
- Atualização de metadados por espécie em cache de 24 horas; falhas aguardam 30 minutos por planta. Durante demonstração ou sessão de rega em andamento, a atualização é suspensa. O código preserva faixas, dados protegidos e histórico; mudanças na categoria de exposição invalidam somente a referência luminosa local.
- Executável e ZIP preparados separadamente em Raiz-1.4 para abertura manual. Nenhuma janela do usuário foi controlada; não houve novo teste físico de rega, luz ou Arduino nesta atualização. O botão Telegram de teste já havia sido confirmado pelo usuário na versão 1.3; isso não é teste novo de envio pela versão 1.4.

## Registro da atualização 1.3 — Telegram

- Compilação nativa concluída. A suíte final passou em **217 verificações**, incluindo os 163 cenários anteriores e 54 verificações do Telegram. Os testes usam dados isolados, APIs simuladas e receptor HTTP local; não acessam contas reais nem enviam mensagens.
- Implementados validação do bot, vínculo privado por código aleatório de oito dígitos com validade de dez minutos e cinco tentativas, consulta de respostas sem webhook público, envio com botão Já reguei e proteção de segredos por DPAPI. Webhook existente é preservado; método de exclusão não é permitido pelo transporte.
- Testados IDs maiores que 32 bits, conta/chat/bot/mensagem incorretos, código antigo, payload inválido, repetição do mesmo callback e segundo clique, aviso antigo, teste sem rega real, leitura de outro vaso e confirmação cruzada entre canais sem duplicar registro.
- Token e identificador pessoal não aparecem na configuração pública nem no CSV. Offset de processamento e regas são salvos juntos na configuração. Falha de gravação pausa a recepção sem avançar a confirmação das atualizações no servidor.
- Envio aceito com identificação do chat é descrito como mensagem criada, sem afirmar entrega ou leitura no celular. Resultado indefinido fica sem repetição automática; erro 429 respeita o prazo maior informado pelo serviço, com limite de tentativas. Erros exibidos não reproduzem token ou corpo externo.
- A versão 1.3 foi preparada separadamente para abertura manual. Mantém a referência local à pasta original de dados; não foi copiada ou regravada a configuração real. ZIP portátil exclui dados pessoais e essa referência. Nenhuma janela aberta pelo usuário foi controlada nesta atualização.
- **Ainda falta o teste real no celular:** crie o bot, configure o token somente no app, faça o vínculo e use Enviar teste. Não houve inspeção visual interativa da nova página nem nova validação física do Arduino nesta versão. Veja TELEGRAM.md.

## Registro histórico da atualização 1.2 — WhatsApp, confirmação e plotter

- **163 verificações automatizadas passaram** na entrega final, incluindo os cenários anteriores, configuração protegida, retorno assinado, deduplicação, estado de entrega, aliases canônicos do destinatário, logs sem volume inventado, plotter e referência ao diretório de dados existente.
- Implementado o envio de template oficial com resposta rápida **Já reguei**, desativado até configurar conta Meta, destinatário e webhook HTTPS. Os dados privados do WhatsApp usam DPAPI. Não houve envio real ao celular nem publicação de um túnel nesta atualização.
- Implementado receptor real HTTP em **loopback**, protegido por assinatura HMAC, vínculo ao número remetente e destinatário do aviso, identificador opaco por alerta, deduplicação e restrição de método/caminho/tamanho/tempo/frequência/simultaneidade. O receptor não serve arquivos nem o painel.
- O clique simulado foi recebido pelo receptor HTTP e registrado, inclusive com transferência chunked. Retentativas HTTP não duplicaram a rega. Confirmações sem quantidade não inventam mL, drenagem ou leitura anterior; campos ausentes ficam vazios no CSV.
- O fluxo usa o destinatário canônico `wa_id` apenas quando associado ao número consultado pela resposta autenticada da API. O namespace de `context.id` não é usado como credencial de autenticação: a correlação depende do payload opaco assinado, destinatário vinculado e ciclo válido. Identidades somente por username/BSUID não são suportadas nesta versão.
- Os testes de transporte usam HTTP simulado, sem chamada real à Graph API. Aceitação pela API, entrega e leitura são estados separados. Timeouts/resultados indefinidos ficam sem repetição automática; erros 429 têm intervalo e limite de tentativas.
- Logs de rega são preservados na configuração e exportáveis em **regas.csv**, com origem, planta, data/hora e volume quando informado. A reconstrução do CSV não duplica entradas.
- Plotter implementado fixo abaixo de todas as páginas, com duas séries, escala bruta ou relativa, seleção de janela, pausa só do desenho, leitura com o mouse e janela ampliada. Usa a mesma conexão USB do painel. Janelas de tempo, valores zero, calibração ausente e lacunas por desconexão foram testados.
- Inspeção visual em cópia isolada: plotter fixo no painel, controles de séries/escala/tempo/pausa e abertura da janela ampliada. Não foi aberta conexão física serial durante essa inspeção e não foram enviadas mensagens reais.
- O usuário interrompeu o controle da interface com Esc. Nenhuma outra janela foi controlada depois disso. A versão final foi preparada separadamente em Raiz-1.2, para abertura manual. Nesta instalação, uma referência local aponta para os dados originais; o programa pede para fechar a versão anterior antes de usar o mesmo diretório. O ZIP portátil não contém essa referência pessoal nem os dados do usuário.
- Testes completos executados com acesso apenas a TCP loopback para o receptor; nenhum teste contactou a Meta. Em ambientes que bloqueiam conexões locais, essa parte da suíte exige permitir loopback. Os testes usam dados e credenciais fictícios, em diretório isolado.

### Ainda precisa de configuração real

Conta Meta, Phone Number ID, token de acesso, App Secret, template aprovado com três variáveis e botão de resposta rápida, destinatário autorizado e endpoint HTTPS público apontando para o receptor local. O app e o encaminhamento precisam permanecer ativos. **Só depois disso poderá ser validado o envio e o clique num celular real.** O guia WHATSAPP.md descreve o cadastro e o teste.

## Registro histórico da versão 1.1 — cuidados e rega

- **88 verificações automatizadas passaram** no código final. Incluem as 44 anteriores, além de acompanhamento de secagem da jade; persistência de solo seco por 30 segundos; pausa para mudança passageira da luz; descarte de leituras antigas e fora da calibração; não recomendar água no solo úmido; respeito às faixas manuais; proteção contra aprender uma dose sem drenagem confirmada e resposta do sensor; pausa quando o sensor já indica solo úmido durante a rega; cálculo por mediana de regas observadas; preservação de sessões encerradas sem aprendizado; isolamento entre vasos; invalidação após recalibrar; recuperação de perfis da versão anterior; pausa noturna; necessidade de referência local de luz.
- A compilação do executável final foi concluída. A tela **Cuidados e rega** foi inspecionada no Windows, em cópia de teste com dados isolados. Foram observados os estados de espera e, em demonstração identificada, o aviso de solo úmido / umidade adequada para aguardar, a proibição de aplicar água baseada na simulação e a solicitação de referência local de iluminação.
- O teste de demonstração não criou CSV de leituras reais e não abriu conexão serial. Não foram feitas novas regas físicas, nem afirmada uma dose empiricamente validada neste vaso. Os cenários de aprendizado e drenagem foram verificados com amostras controladas nos testes.
- Dados reais existentes antes da atualização: jade importada como *Crassula ovata*; solo seco 392 e úmido 281; LDR escuro 22 e claro 316; ambiente interno. A atualização preserva o cadastro, a calibração, a chave protegida e o histórico. Os backups ficam fora do pacote público.
- A estimativa não pede dimensões do vaso. Sem regas anteriores, são mostrados **30 mL como primeira porção experimental**, com total desconhecido. Esse valor é uma escolha operacional do protótipo, não um volume agronômico atribuído às fontes. Uma dose futura depende dos volumes que o usuário efetivamente registrar até começar a drenar.
- O gatilho inicial de 10% do índice e a tolerância de 20% para referência de luz são aproximações operacionais transparentes, não condições ideais obtidas da Perenual. O usuário pode ajustar o gatilho de secagem; uma faixa manual tem prioridade. A adequação da luz depende de um local validado pelo usuário e não comprova lux nem exposição diária.
- Fontes consultadas para as orientações qualitativas: [Iowa State University — secagem entre regas e drenagem](https://yardandgarden.extension.iastate.edu/faq/how-often-should-i-water-my-jade-plant) e [NC State — iluminação e cuidados da jade](https://plants.ces.ncsu.edu/plants/crassula-ovata/). Não houve nova consulta autenticada à Perenual nesta atualização.

## Registro histórico da versão 1.0

Os itens abaixo documentam o alcance da validação original, antes do cadastro e dos testes físicos feitos pelo usuário ao longo da conversa.

## Confirmado

- Executável Windows compilado com sucesso, usando o .NET Framework do computador.
- **44 verificações automatizadas passaram** no código final: protocolo JSON e formato textual legado; limites do ADC e valores zero; descarte de pacotes parciais; calibração nos dois sentidos e ausência de calibração; expiração de sensores e clima; separação entre clima externo e ambiente interno; pausa da avaliação de luz à noite; cruzamento entre orientação de rega, solo e temperatura; ausência de faixas inventadas; validação de limites; persistência; proteção DPAPI da chave; histórico sem segredo; neutralização de fórmulas no CSV.
- Consulta real da Open-Meteo e geocodificação de **Guarulhos, São Paulo, Brasil** validada com o código final. A resposta incluiu temperatura, umidade, dia/noite e horário do modelo válidos. Valores variam com o momento da consulta e não foram fixados no programa.
- Interface inspecionada no Windows durante o desenvolvimento: painel com clima real, modo de demonstração identificado, criação e salvamento de perfil manual e página de Arduino/calibração. O teste de demonstração não criou arquivo de histórico. Os dados de teste ficaram fora do pacote de entrega.
- Novo sketch sem DHT compilou para **Arduino Uno**: 2.448 bytes de programa e 192 bytes de variáveis globais. Nenhuma biblioteca externa é necessária.
- Versão final recompilada e sua abertura solicitada no Windows. A revisão adicional da interface foi interrompida pelo usuário com Esc; nenhuma outra interação com janelas foi feita depois disso.

## Pendente de configuração ou teste físico

- **Perenual autenticada:** o usuário ainda não tem chave. Os adaptadores usam os endpoints documentados; normalização e campos ausentes foram testados com respostas representativas. Não houve busca/importação autenticada real. É necessário cadastrar uma chave válida e verificar as permissões do plano.
- **Arduino pelo aplicativo:** não foi aberta conexão com o hardware nesta etapa. O sensor DHT apresentou superaquecimento; a montagem precisa ser conferida e o DHT danificado removido antes de alimentar novamente.
- **Novo firmware:** compilado, mas não enviado ao Uno. O sketch anterior permanece preservado.
- **Calibração e faixas:** dependem da planta, do substrato e das leituras reais. Não foram preenchidas com valores presumidos.
- Chave DPAPI e geração do CSV foram verificadas em diretórios isolados. O fluxo visual completo de cadastro de chave, importação autenticada e exportação por diálogo não foi concluído nesta sessão.
- Histórico não é persistido em modo de demonstração. O gráfico mostra a sessão atual; sessões anteriores ficam no CSV.

## Reproduzir as verificações

Use `Fonte/testar.ps1` para compilar e executar os testes atuais, incluindo o receptor TCP em loopback. O script cria uma pasta isolada e não usa a configuração real do app. `Fonte/build.ps1` recompila o executável. Requer o compilador do .NET Framework disponível no Windows.

## Referências consultadas

- [API de espécies e detalhes — Perenual](https://perenual.com/docs/api)
- [Clima — Open-Meteo](https://open-meteo.com/en/docs)
- [Geocodificação — Open-Meteo](https://open-meteo.com/en/docs/geocoding-api)

As APIs não garantem todas as condições ideais em formato numérico. O app preserva a distinção entre orientação importada e limite cadastrado pelo usuário.
