# Raiz 2.0 — os seis novos recursos

## 1. Uma recomendação principal

A tela **Hoje** destaca o próximo cuidado e seu motivo: **Não precisa regar agora**, **Confira o solo antes de regar**, **Confira a iluminação** ou **Aguardando sensor**. Demonstração e falta de leitura atual não viram uma ordem para aplicar água. Os detalhes de solo, quantidade e luz continuam numa seção secundária.

## 2. Avisos de pouca luz no Telegram

Em **Avisos**, ative os avisos gerais e deixe marcada a opção **Avisar também sobre pouca luz**. É necessário ter faixa de luz cadastrada ou uma referência local válida.

O aviso exige cinco minutos contínuos abaixo da referência, com leituras marcadas como diurnas pelo contexto atualizado do clima. Uma sombra passageira, falta de leitura, contexto de clima antigo ou noite não bastam. Há pelo menos seis horas entre ciclos de aviso da mesma planta, contando o último envio/tentativa do ciclo anterior. Após recuperação da luz, o ciclo é encerrado; se o problema continuar ou voltar depois do intervalo, um novo aviso exige novamente leituras persistentes.

O aviso de luz não tem botão Já reguei e não gera registro de água. O LDR continua medindo uma referência relativa, sem comprovar lux ou duração ideal da exposição solar.

## 3. Quantidade opcional pelo Telegram

Depois de tocar em **Já reguei** numa mensagem real:

1. A rega é registrada uma única vez, inicialmente sem volume, quando ele ainda não é conhecido.
2. O bot oferece **Informar quantidade** ou **Não medi**.
3. Se escolher informar, responda à pergunta do bot com o total medido, por exemplo **120 mL**. Use Responder na própria pergunta para vincular o valor à rega correta. São aceitos valores inteiros entre 1 e 100000 mL; não são aceitas suposições em litros ou texto livre como quantidade.
4. Depois, o bot pergunta se você observou água saindo pelos furos. Responda apenas sobre a rega já realizada; não coloque água adicional para testar.

O volume atualiza o mesmo registro, sem criar outra rega. **Não medi** mantém a quantidade desconhecida. Respostas repetidas, de outra conta, de outra mensagem ou fora do prazo de 24 horas não substituem a rega. Mensagens de TESTE continuam sem gerar rega real.

Para aprender uma dose, o app também exige drenagem confirmada, ponto inicial conhecido de um aviso recente, leitura final válida com resposta à água e a mesma calibração. Um volume informado sem essas condições fica no histórico, mas não ensina uma dose. Leituras novas para confirmar a drenagem só são aproveitadas nos primeiros 30 minutos após a confirmação da rega e quando pertencem ao mesmo vaso.

Se a pergunta opcional não puder ser enviada, a rega continua salva. Após um minuto, toque novamente no Já reguei original para tentar reenviar a pergunta pendente, com até três tentativas por etapa. Não existe repetição automática de mensagem com resultado indefinido.

## 4. Histórico diário

Em **Histórico**, escolha 7, 14 ou 30 dias e selecione um dia na tabela. A curva mostra **solo em verde**, **luz em dourado** e **regas em azul tracejado**.

- A tabela mostra mínimo, máximo e última umidade relativa do solo, regas e tempo sem cobertura de leituras.
- Volume desconhecido fica separado de volume medido.
- Sem dados não significa umidade zero: o gráfico deixa lacunas e inclui dias sem coleta.
- O desenho usa o último ponto de cada trecho contínuo de cinco minutos; mínimo e máximo usam todas as leituras válidas lidas.
- Mudanças de calibração registradas a partir desta versão interrompem a curva. O CSV antigo não informa onde ocorreram recalibrações anteriores; evite comparar índices de montagens diferentes como se fossem a mesma escala.
- Os horários são os do computador. A atualização ocorre ao abrir a tela, pelo botão Atualizar ou a cada minuto enquanto ela estiver aberta.
- Para manter a leitura local limitada, arquivos grandes são consultados a partir dos últimos 80 MB; a tela avisa quando isso restringir o período disponível. Os arquivos originais não são reduzidos ou regravados.

## 5. Primeiros passos

O assistente mostra planta, cidade, Arduino, calibração, referência de luz e vínculo opcional com o Telegram, indicando **Pronto** ou **Pendente**. Selecione uma etapa e clique em Abrir etapa selecionada. A referência de luz deve ser registrada durante o dia.

Instalações sem planta ou calibração são encaminhadas ao assistente na primeira abertura. Use **Ir para Hoje** para continuar e retome por Hoje ou Configurações. Nada precisa ser cadastrado outra vez só para abrir o assistente.

## 6. Perto do relógio do Windows

Na primeira vez que fechar a janela pelo X, o Raiz oferece continuar em segundo plano, encerrar ou cancelar. Ao continuar, o ícone perto do relógio mantém o aplicativo disponível e o ciclo de coleta e avisos permanece ativo.

Clique duas vezes no ícone para reabrir. Para sair de verdade, use **Encerrar Raiz** no menu do ícone. Em **Configurações**, a opção **Ao fechar a janela, continuar perto do relógio do Windows** permite mudar o comportamento.

O recurso mantém o programa aberto; não inicia o Raiz sozinho ao ligar o computador e não coleta com o computador desligado ou suspenso. Apenas uma instalação deve usar o mesmo bot por vez.
