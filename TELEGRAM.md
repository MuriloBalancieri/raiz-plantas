# Telegram no Raiz 2.0

## Configurar no celular e no computador

1. Instale o Telegram no celular e entre na sua conta.
2. Abra o bot oficial [@BotFather](https://t.me/BotFather), envie `/newbot` e siga as instruções. Escolha um nome e um nome de usuário terminado em `bot`. Crie um bot exclusivo para o Raiz.
3. Copie o token fornecido pelo BotFather. **Cole somente no campo protegido do Raiz**, em **Telegram e avisos → Salvar token e preparar vínculo**. Não envie esse token por este chat e não compartilhe capturas mostrando o token.
4. O Raiz exibirá `/start` seguido de oito dígitos. No celular, abra uma conversa privada com o SEU bot e envie esse comando completo. Você também pode usar **Abrir meu bot**, no computador com Telegram conectado à mesma conta.
5. Aguarde o cartão mostrar **Celular vinculado**. O código vale por dez minutos e é invalidado após cinco tentativas incorretas. Se expirar, use **Gerar novo vínculo**. Não compartilhe o código.
6. Clique em **Enviar teste ao meu celular**. Toque em **Já reguei** na mensagem de TESTE. O app deve mostrar o teste confirmado, sem criar uma rega real no histórico.
7. Clique em **Ativar avisos pelo Telegram**. O envio automático anterior pelo WhatsApp será pausado; suas credenciais anteriores são preservadas.

Não é necessário informar número de telefone ou procurar um chat ID. O vínculo registra somente a conta que enviou o código privado correto. Não há servidor público, túnel ou aprovação de modelo para essa integração. Se o bot tiver webhook de outro sistema, o Raiz interrompe a configuração sem apagá-lo.

## Novos recursos de rega e luz

O aviso real de rega oferece, depois de Já reguei, registrar a quantidade medida ou escolher Não medi. A pergunta é vinculada à rega e aceita um total inteiro em mL; depois, a drenagem pode ser confirmada separadamente. Quantidade e drenagem não substituem as leituras necessárias para aprender uma dose. Avisos de TESTE não geram regas nem perguntas de quantidade.

A opção Avisar também sobre pouca luz exige referência local válida, clima diurno atualizado e cinco minutos contínuos de pouca luz durante o dia. Há intervalo mínimo de seis horas entre ciclos da mesma planta. Avisos de luz não oferecem Já reguei.

Veja os detalhes em [NOVO-2.0.md](NOVO-2.0.md). O fluxo de resposta utiliza [ForceReply e botões da API oficial](https://core.telegram.org/bots/api#forcereply).

## Indicador da conexão

A faixa no topo de **Avisos** mostra a conexão atual com o Telegram. **Bot conectado** depende de resposta recente da API nesta execução, inclusive consultas que não retornam novas mensagens. **Conectando** aparece antes da primeira resposta; **Bot desconectado** aparece em falhas de consulta; **Recepção pausada** indica que a consulta foi desativada ou precisa ser retomada. Respostas com mais de 75 segundos deixam de sustentar o estado conectado.

Avisos de rega pausados podem coexistir com bot conectado: as respostas aos botões anteriores continuam sendo recebidas. Conexão ativa não comprova entrega ou leitura da mensagem no celular.

## Avisos e confirmação de rega

Quando a planta ativa permanecer perto da referência de solo seco pelo tempo exigido pelo app, ele prepara um aviso por ciclo de secagem. Sem leitura atual e confiável, sem calibração ou em demonstração, não há envio automático. Confira o substrato antes de regar.

A mensagem inclui a planta, índice relativo do solo, orientação sobre água e o botão **Já reguei**. Sem histórico medido, 30 mL é apenas uma primeira porção experimental, com total desconhecido. O botão confirma uma rega manual; não aciona bomba.

Ao tocar no botão, o Raiz recebe a confirmação e a registra em **Histórico → logs de rega**, com origem **Telegram**. Um segundo toque ou atualização repetida não duplica a rega. Sem quantidade registrada, o volume fica não informado; não é inventada uma dose e não se ensina uma quantidade fictícia ao estimador. O botão de uma mensagem marcada TESTE nunca registra rega real.

O Telegram não fornece o horário exato do clique nesse botão. Por isso, o log usa a hora em que o app recebeu a resposta. Se o sensor disponível pertencer a outro vaso ou estiver antigo, a leitura fica desconhecida. Avisos de ciclos anteriores são recusados quando já há um aviso posterior da mesma planta nesse canal. Uma confirmação encerra também o aviso paralelo do outro canal para evitar duplicidade.

## Para continuar funcionando

- Mantenha o computador ligado, o Raiz aberto e a internet ativa. O Arduino também precisa estar conectado para novos avisos baseados em sensores.
- Se o computador estiver desligado, ele não consulta sensores nem gera alertas. Uma resposta pode chegar depois que o app voltar; o Telegram guarda atualizações por até 24 horas.
- Uma mensagem criada pela API não comprova entrega da notificação na tela, leitura ou rega. A confirmação vem pelo botão. Confira também as permissões de notificação do Telegram no celular.
- **Pausar avisos** interrompe novos envios automáticos, mas continua recebendo respostas para registrar cliques em mensagens já enviadas.
- Use somente uma instalação do Raiz e um leitor por bot. Em caso de disputa, confira outros programas ou crie outro bot exclusivo. O Raiz não apaga webhooks externos.
- Erros de limite aguardam pelo menos cinco minutos ou o prazo maior informado pelo Telegram, com até três tentativas por alerta. Um envio indefinido não se repete automaticamente: confira o celular.
- Token inválido, bot bloqueado, disputa com outro leitor ou erro de gravação podem suspender a recepção. Corrija a causa e use **Receber respostas novamente**, ou salve o token novamente se ele tiver sido trocado.

## Dados e validação

Token, conta vinculada e código temporário são protegidos pelo usuário Windows em `telegram.protegido`. Configuração e CSV não contêm o token ou o chat pessoal em texto puro. Ao mudar de usuário Windows, configure novamente. O app envia ao Telegram o chat vinculado e o texto do aviso; não envia o histórico completo.

A implementação foi compilada e testada com respostas simuladas da API, sem token real. O envio e o clique no seu celular precisam ser confirmados pelo teste do passo 6.

Referências oficiais: [criação de bots](https://core.telegram.org/bots/features#botfather), [API, getUpdates e botões](https://core.telegram.org/bots/api).
