# Raiz 2.1 — evolução, diagnóstico, relatórios e transferência

## Evolução no Histórico

O cartão **O que mudou neste vaso?** acompanha o período escolhido de 7, 14 ou 30 dias. A secagem usa a mediana dos ciclos observados entre uma rega registrada e o primeiro ponto no gatilho local. Exige uma leitura acima do gatilho até dez minutos após a rega e coleta sem interrupções até o cruzamento. Outra rega ou mudança registrada da calibração interrompe o ciclo. Sem ciclos suficientes, o app explica a falta de dados em vez de prever uma data.

A luz apresenta a faixa horária com maior índice médio entre as horas efetivamente coletadas. O cálculo pondera cada intervalo contínuo pelo tempo, aceita apenas intervalos menores que 12 segundos e exige dez minutos observados na faixa. Não mede horas de sol direto e não compara escalas anteriores à última calibração registrada. O gráfico diário continua disponível para acompanhar a variação.

## Avisos dos sensores

Em **Sensores**, o diagnóstico distingue desconexão, ausência de leitura atual, pacote inválido, saturação em 0/1023 por um minuto, solo fora da margem de calibração por 30 segundos e valor idêntico por dez minutos contínuos. Em **Hoje**, aparecem somente as condições que pedem atenção, sem um cartão permanente de estado normal.

Valor repetido é uma suspeita, pois um ambiente estável também pode produzir leituras iguais. Não confirma defeito. O diagnóstico não envia mensagens extras ao celular e não altera automaticamente as referências. As regras existentes continuam rejeitando leituras antigas e solo sem referência confiável.

## Relatórios

Em **Histórico**, escolha o período e use:

- **Relatório PDF**: resumo, gráfico do período, tabela diária e regas, com paginação para listas longas. É gerado diretamente pelo app, sem impressora ou outro programa.
- **Resumo e regas CSV**: linhas por dia e por rega, com identificação do tipo, índices, cobertura e volumes conhecidos ou desconhecidos.
- **Medições brutas CSV**: mantém a exportação do arquivo completo de medições.

Os relatórios usam apenas a planta ativa e leituras reais no período. Exportações não podem substituir arquivos na pasta de dados ativa. O relatório explica lacunas e índices relativos; não inclui tokens ou chaves. Horários seguem o fuso atual do computador.

## Transferir entre computadores

1. No computador de origem, abra **Configurações → Transferir dados para outro computador → Exportar transferência**.
2. Escolha uma senha de 12 a 256 caracteres e confirme-a. Guarde-a separadamente: o Raiz não armazena nem recupera essa senha.
3. Leve o arquivo **.raiz** ao destino. Abra a versão 2.1, use **Importar transferência**, digite a senha e confira a quantidade de plantas e regas.
4. Confirme a importação. O app cria uma pasta separada e preserva os dados anteriores. Encerra após preparar a troca; abra o mesmo executável novamente.
5. Confira a porta USB e a calibração, valide a luz do novo local e retome os avisos somente depois de fechar o Raiz no computador anterior.

O arquivo contém plantas, calibrações, regas, medições e credenciais. É cifrado com AES-256-CBC, autenticado com HMAC-SHA256 e usa PBKDF2-HMAC-SHA256 com 180 mil iterações, sal e IV aleatórios. A autenticação é verificada antes da leitura. As chaves são reprotegidas para o usuário Windows do destino. Senha errada, arquivo alterado ou estrutura inválida não ativam uma importação.

Limite: histórico de até 100 MB. O arquivo não funciona como sincronização: a importação troca o conjunto ativo na próxima abertura, sem mesclar dois históricos. A pasta anterior permanece disponível. As sessões de rega não são retomadas, eventos antigos não são reenviados e avisos/receptores ficam pausados. A luz e o aprendizado precisam ser validados na montagem de destino; os registros e referências antigos são preservados. Credenciais iniciais de outro pacote não são importadas automaticamente sobre um conjunto transferido.

Instalações que usam `RAIZ_DATA` precisam abrir a versão portátil sem essa definição para trocar a pasta pelo app.

## Validação

Os testes usam pastas isoladas, credenciais fictícias e serviços simulados em loopback. Abrangem lacunas, calibração, precisão de horários, diagnósticos, PDF/CSV, arquivo cifrado, senha incorreta, alteração de conteúdo, preservação da origem, reproteção pelo Windows e bloqueio de retomadas automáticas. Não enviam mensagens reais.

A transferência foi verificada com pastas e reproteção no Windows desta máquina. Ainda precisa de validação física em um segundo computador. O resumo de secagem precisa acumular ciclos reais da planta para se tornar útil.
