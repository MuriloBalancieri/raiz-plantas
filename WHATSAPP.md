# WhatsApp no Raiz 1.2

O app envia o aviso de rega pela API oficial WhatsApp Cloud API da Meta. O botão **Já reguei fica dentro da mensagem no WhatsApp**: o clique retorna ao computador e registra a confirmação em **Histórico → Logs de rega**.

O envio vem desativado. Nenhuma conta ou número foi criado e nenhum aviso real foi enviado durante o desenvolvimento. É preciso configurar sua conta Meta e um endereço HTTPS para o retorno do botão. O plotter e o registro de regas no computador funcionam sem essa configuração.

## O que precisa ficar ligado

Arduino → USB → Raiz aberto → API da Meta → WhatsApp no celular.

Botão no celular → Meta → endereço HTTPS público → receptor local do Raiz → logs de rega.

O computador e o Raiz precisam continuar ligados, com internet e o túnel HTTPS ativo. O programa não instala um serviço em segundo plano. Sem o receptor acessível, o clique não pode ser confirmado nos logs até ser recebido; o app não afirma que registrou algo que ainda não chegou.

## 1. Obter acesso à API oficial

Abra https://developers.facebook.com/apps/ e configure WhatsApp Business Platform / Cloud API na sua conta Meta. No painel do seu aplicativo, obtenha:

- **Phone Number ID:** identificador do número remetente. Não é o seu número de celular nem o WABA ID.
- **Token de acesso:** com a permissão de envio de mensagens do WhatsApp necessária ao seu remetente. Tokens temporários de teste podem expirar; siga a documentação da sua conta para uso continuado.
- **App Secret:** segredo do aplicativo Meta, usado para verificar a assinatura das confirmações recebidas.
- **Versão da Graph API:** copie a versão da URL de exemplo exibida no seu painel, no formato `vNN.0`. O Raiz não escolhe uma versão supostamente atual sem confirmação do painel.

O destinatário é o seu celular, com país + DDD + número. Em modo de teste da Meta, autorize esse destinatário no próprio painel quando solicitado. Os limites, permissões, aprovação e eventual cobrança são definidos pela Meta; confira as condições da sua conta. O Raiz não cria contas, não gera tokens de acesso da Meta e não contrata planos.

## 2. Criar o modelo com botão

Para avisos proativos, configure um modelo aprovado na Meta. O Raiz usa o formato de template mesmo quando existe uma conversa recente, para não depender do envio de texto livre dentro de uma janela de atendimento.

Sugestão de nome: **raiz_rega**. Idioma: **Português (Brasil), pt_BR**. Escolha a categoria apropriada no painel; a Meta decide a aprovação/categoria.

Crie um corpo com estas **três variáveis posicionais, nesta ordem**:

```
Aviso de cuidado da sua planta {{1}}.
O índice relativo de solo está em {{2}}% e indica proximidade da referência seca.
Confira o substrato antes de regar. Água: {{3}}.
Depois de realizar a rega, toque em Já reguei para registrar.
```

As variáveis são:

1. Nome da planta.
2. Índice relativo de solo.
3. Estimativa aprendida de água, ou indicação de primeira porção experimental quando ainda não houver histórico.

Adicione **um botão de resposta rápida**, na primeira posição, com o texto **Já reguei**. Não use botão de link/URL no lugar da resposta rápida. O app atribui a cada envio um identificador opaco de correlação; a Meta devolve esse identificador quando você toca no botão.

Aguarde a aprovação e habilitação do modelo. Se usar outro nome ou idioma, preencha os valores correspondentes no Raiz. Um modelo com número/ordem de variáveis diferente será recusado pela API. O app não submete nem aprova modelos automaticamente.

## 3. Preparar o receptor no app

No Raiz, abra **WhatsApp e avisos → Configurar WhatsApp**.

1. Preencha seu celular, Phone Number ID, token de acesso e App Secret.
2. Clique em **Gerar token** para criar o token de verificação do webhook. Esse é um segredo gerado pelo Raiz para a etapa de cadastro do receptor; não é o token de acesso da Meta.
3. Use **Copiar token** quando precisar colá-lo no painel Meta. Não envie nenhum desses segredos pelo chat.
4. Preencha a versão da API, nome do modelo e idioma.
5. Deixe a porta local **8788**, se estiver disponível.
6. Ative **receptor local**. Você pode salvar com os **envios automáticos ainda desativados** enquanto prepara o endereço público.

O receptor escuta somente em `127.0.0.1:8788`, caminho `/whatsapp`. Ele não serve o painel do app, arquivos, tokens ou uma interface geral de controle remoto. Não é preciso abrir a porta do roteador nem instalar uma reserva administrativa de URL do Windows para esse receptor.

## 4. Criar o endereço HTTPS de retorno

É necessário um túnel ou proxy HTTPS que encaminhe o caminho `/whatsapp` ao receptor local. Você pode usar um provedor de túnel compatível.

Exemplo de teste com **Cloudflare Quick Tunnel**, após instalar `cloudflared` pelas instruções oficiais:

```
cloudflared tunnel --url http://127.0.0.1:8788
```

O processo informa uma URL temporária `https://...trycloudflare.com`. Use essa URL acrescida de **/whatsapp**, sem barra final. Mantenha o processo do túnel ativo.

Quick Tunnels são para testes: o hostname muda quando você cria outro túnel e não há garantia de disponibilidade. Para uso contínuo, configure um túnel com hostname estável seguindo o provedor. O pacote não inclui nem instala `cloudflared` e não publicou sua máquina na internet automaticamente.

Não coloque uma tela de login por email na frente desse endpoint: a Meta precisa acessá-lo como serviço, sem navegador interativo. O Raiz valida o token do cadastro e a assinatura das mensagens; outros caminhos não são atendidos.

No Raiz, salve a URL pública HTTPS completa no campo correspondente. Mudar a URL, a porta, o destinatário, o App Secret ou o token de verificação exige validar novamente o receptor.

Fonte do exemplo e limitações: https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/

## 5. Validar na Meta

No cadastro de webhooks do seu aplicativo Meta:

1. **Callback URL:** a URL HTTPS pública terminando em `/whatsapp`.
2. **Verify token:** o mesmo token de verificação gerado e salvo no Raiz.
3. Valide/salve a inscrição. O Raiz responderá ao challenge e indicará **webhook validado** quando receber a validação correta.
4. Inscreva o aplicativo nos eventos **messages** do WhatsApp e associe a conta/remetente conforme o painel Meta exigir. Esses eventos incluem os retornos do botão e os estados de entrega.

O status “webhook validado” confirma o cadastro recebido pelo app, mas não comprova sozinho que você inscreveu todos os eventos ou que a mensagem chegou ao celular.

## 6. Testar e ativar os avisos

Com modelo aprovado, receptor validado e planta escolhida, use **Enviar mensagem de teste** no Raiz. Essa ação realmente envia uma mensagem ao celular configurado. Ela será identificada como **TESTE**; clicar no botão dessa mensagem não cria uma rega real.

Os estados têm significados diferentes:

- **Aceito pela Meta:** a API retornou um identificador de mensagem; ainda não é comprovação de entrega.
- **Enviado / Entregue / Lido:** estados retornados pelos eventos recebidos da Meta.
- **Já reguei confirmado:** o app recebeu o botão correspondente. Em aviso real, a rega foi registrada.
- **Resultado indefinido:** houve interrupção ou resposta ambígua. Confira o celular; o app não repete automaticamente esse envio.

Depois do teste, marque **Quero receber avisos de rega neste celular** e salve. Isso habilita o envio automático para o número informado. O aviso só é produzido por leituras reais, atuais, calibradas, no perfil ativo e após a condição de solo seco persistir. O modo de demonstração não envia alertas.

## Botão Já reguei e logs

Quando você regar após um aviso real, toque no botão da mensagem. O app registra data/hora da confirmação, planta, origem **WhatsApp** e identificador do evento. Reenvios do mesmo evento ou novos cliques no mesmo aviso não duplicam a rega. Um botão de ciclo antigo não confirma um alerta novo.

Se o sensor já tiver ficado úmido antes do clique, a confirmação do último aviso ainda pode ser registrada. Se a planta ativa no computador tiver mudado, a rega é atribuída à planta do aviso e não usa a medição do outro vaso.

Sem um volume informado, a quantidade fica **não informada** nos logs e vazia no CSV — nunca como se tivesse sido medido 0 mL. O clique não afirma drenagem e não ensina uma dose ao estimador. Se uma sessão guiada já tinha porções informadas para esse vaso, o volume contado é preservado, mas o clique sozinho ainda não confirma a drenagem.

Também existe o botão **Já reguei** no app para registrar manualmente, sem WhatsApp. A rega continua manual; não há bomba ou irrigação automática.

Abra **Histórico → Logs de rega** para ver os registros e **Exportar logs de rega CSV** para copiá-los. A fonte persistente dos registros é a configuração local; o arquivo `Dados/regas.csv` é regenerado sem duplicar linhas a cada atualização.

## Repetição e falhas

Há um aviso por ciclo de secagem, e não um envio a cada amostra. Uma confirmação pausa os avisos desse ciclo; o sensor precisa voltar a registrar umidade maior de forma contínua para rearmar um ciclo futuro. Depois de uma rega, o app mantém a pausa operacional de 30 minutos já existente.

Erros de limite HTTP 429 admitem até três tentativas no ciclo, separadas por pelo menos cinco minutos. Token/modelo/destinatário recusados aguardam correção. Salvar novamente a configuração libera um próximo aviso para falhas definitivas. Um envio ambíguo, ou interrompido ao encerrar o app, não é repetido automaticamente, para evitar mensagens duplicadas.

Se o computador ou túnel estiver fora do ar, não há garantia de confirmação imediata. Confira o log no app. O Raiz só registra uma confirmação depois de recebê-la e salvá-la.

## Dados e proteção

Token de acesso, App Secret, token de verificação, número remetente e celular destinatário ficam em **Dados/whatsapp.protegido**, protegidos pelo Windows para este usuário (DPAPI). Não entram no pacote de distribuição nem nos CSVs. Para mover a configuração a outro usuário Windows, cadastre esses dados novamente.

O envio para a Meta inclui destinatário e conteúdo do aviso (planta, índice de solo e orientação de quantidade), como necessário ao WhatsApp. As confirmações retornam pela Meta e pelo provedor do túnel escolhido. O app não publica o histórico completo nem a chave da Perenual.

O receptor verifica HMAC-SHA256 sobre os bytes originais com o App Secret, confere remetente/telefone e correlaciona o botão ao aviso. Há limites de corpo, cabeçalhos, tempo, simultaneidade e frequência. Mensagens não autenticadas ou de outro celular não alteram os registros. O receptor público deve usar HTTPS pelo túnel/proxy.

A ligação desta versão é por número de telefone. Se a Meta devolver um `wa_id` canônico diferente, o app vincula esse identificador somente quando a resposta autenticada da API o associa ao número solicitado; não tenta adivinhar removendo ou acrescentando dígitos. Eventos que só contenham identidade de username/BSUID, sem telefone compatível, não confirmam rega nesta versão. Valide o teste do botão na sua conta antes de depender dos avisos.

## Referências

- Documentação oficial atual da Meta: https://developers.facebook.com/documentation/business-messaging/whatsapp/ e https://developers.facebook.com/docs/whatsapp/cloud-api/
- Exemplo oficial da estrutura de template/botão: https://whatsapp.github.io/WhatsApp-Nodejs-SDK/api-reference/types/TemplateObject/ — referência de formato do antigo SDK Meta, hoje arquivado. O Raiz usa REST diretamente, não instala esse SDK. Valide a versão e o modelo no painel atual da Meta.
- Receptor HTTPS/túnel no exemplo oficial Meta: https://whatsapp.github.io/WhatsApp-Nodejs-SDK/receivingMessages/
- Cloudflare Quick Tunnels: https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/

O fluxo foi validado com mensagens simuladas e receptor HTTP real em loopback. **Envio e clique em um celular real ainda dependem da configuração da sua conta e do endpoint HTTPS.**
