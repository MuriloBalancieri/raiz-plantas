# Roteiro de apresentação — cerca de cinco minutos

Abra **Apresentar.cmd**, pressione **F11** e avance pelas etapas. Você pode repetir o roteiro para cada grupo de visitantes.

## 1. O problema — 40 segundos

“O Raiz ajuda a acompanhar uma planta. Ele reúne o que acontece no vaso com orientações da espécie e o clima da região para explicar quando vale conferir o solo e a luz.”

Aponte o caminho: **sensor → Arduino → aplicativo → pessoa e celular**. Explique que a rega continua manual.

## 2. A montagem — um minuto

Mostre o Uno, o sensor capacitivo de solo e o LDR. Explique que o Arduino envia números para o computador pela USB.

Com a montagem pronta, escolha **Sensores reais**. Faça uma sombra breve sobre o LDR para mostrar a resposta. Se não houver leitura ou calibração, a tela informa o que falta; use **Exemplos simulados** para continuar. Não altere a fiação com a alimentação ligada. Mantenha a eletrônica do sensor de solo seca e fora da terra.

## 3. A interpretação — um minuto

Escolha **Solo úmido**, **Solo seco**, **Pouca luz** e **Sem sinal**.

“Esses cenários são exemplos. Em uso real, o app compara o sinal com referências registradas na montagem e exige continuidade antes de avisar. Sem sinal, ele espera uma leitura atual.”

Explique que **100% não é uma meta de rega**. O índice compara as referências locais. O LDR também fornece uma comparação relativa de luz.

## 4. As APIs — um minuto

“Uma API permite que um programa consulte outro serviço. A Perenual oferece orientações gerais por espécie. A Open-Meteo oferece o clima externo da cidade.”

Mostre o cadastro salvo e a indicação do clima. Explique que o ar dentro de casa pode ser diferente e que as categorias não determinam exatamente quantos mL um vaso precisa.

## 5. O retorno da pessoa — um minuto

Use **Enviar demonstração ao celular** e mostre a mensagem DEMONSTRAÇÃO DO RAIZ no Telegram. Toque em **Confirmar demonstração** para mostrar o retorno ao computador. Depois, use **Mostrar histórico da planta** para mostrar os gráficos e **Abrir arquivos do histórico** para apresentar CSV, JSON e PDF.

“Em uso normal, o aviso pode chegar pelo Telegram. A pessoa confere a planta, realiza a rega e confirma no celular. Ela pode informar a quantidade medida e se houve drenagem. O histórico ajuda a acompanhar a resposta do vaso.”

O botão **Simular Já reguei** é somente visual. **Enviar demonstração ao celular** envia uma mensagem real de teste, cujo botão confirma a demonstração sem registrar uma rega real. Não é necessário regar a planta para apresentar.

## Encerramento — 20 segundos

“O projeto integra sensores, programação, APIs e comunicação. A principal contribuição é tornar as leituras compreensíveis e guardar o histórico para apoiar o cuidado.”

Se houver tempo, use **Voltar ao app** para mostrar o Histórico, os relatórios e o plotter. Ao sair da apresentação, a rotina de avisos pode retomar conforme as configurações existentes.

## Antes do evento

Ensaie F11 e Esc no projetor. Se for usar sensores reais, confirme cabo, porta e calibração antes de começar. Deixe a Arduino IDE sem o Monitor Serial e o Plotter ocupando a mesma porta. Os exemplos do roteiro funcionam mesmo sem internet ou Arduino.
