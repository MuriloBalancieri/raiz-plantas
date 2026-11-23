# Apresentação do Raiz 2.4.1

Slides prontos para a mostra de conhecimento, com notas do orador em cada tela.

| Arquivo | Para quê |
| --- | --- |
| `Raiz-Apresentacao.pdf` | Visualizar em qualquer computador, celular ou tablet, sem instalar nada. Também serve para imprimir. |
| `Raiz-Apresentacao.pptx` | Editar no PowerPoint, LibreOffice Impress ou Google Apresentações, e apresentar com as notas do orador. |
| `imagens/` | Capturas de tela do app usadas nos slides, para reaproveitar em outros materiais. |
| `dados-simulados/` | Histórico de sete dias, regas, resumo e relatório gerados pelo app para a mostra, com origem simulada. |

## Abrir em outro computador

1. Copie a pasta `Apresentacao` inteira para o outro computador.
2. Para só mostrar: abra o **PDF** no navegador ou no visualizador do sistema e use tela cheia.
3. Para editar ou usar as notas: abra o **PPTX** no PowerPoint.
4. No celular, o PDF abre em qualquer leitor de PDF depois de baixado; o PPTX precisa de um aplicativo de apresentações.

A fonte usada é **Segoe UI**, já incluída no Windows. Em outro sistema, o PowerPoint ou o LibreOffice substitui a fonte e pode alterar levemente os espaços.

## Como apresentar

- **PowerPoint:** aba *Apresentação de Slides* → *Do Começo* (F5). No modo Apresentador, as notas aparecem na tela do palestrante.
- **PDF:** tela cheia no visualizador. As notas não aparecem no PDF, por isso ele serve para consulta ou impressão.
- **Duração:** 14 slides, cerca de 7 a 9 minutos. Para uma fala curta, use os slides 2, 3, 9, 10 e 12, fechando no 14.
- **Demonstração ao vivo:** o roteiro de cinco minutos do aplicativo está em [../ROTEIRO-DA-MOSTRA.md](../ROTEIRO-DA-MOSTRA.md). Os slides não substituem a demonstração no Raiz.

## Estrutura

1. Capa
2. O problema: quando vale conferir a planta
3. A proposta: medir, enviar, entender e cuidar
4. Arquitetura: sensores, aplicativo e serviços externos
5. Hardware: Arduino Uno, solo capacitivo e LDR
6. Calibração local e o que o índice significa
7. Regras de continuidade antes de avisar
8. Espécie e clima como orientação
9. Aviso e confirmação pelo Telegram
10. Histórico e evolução do vaso
11. Relatórios, diagnósticos e transferência
12. Modo Apresentação do aplicativo
13. Engenharia e validação
14. Encerramento

## Observações

- As telas do aplicativo são renderizações com **dados simulados**, identificadas nos slides e nas notas.
- Nenhuma chave de API ou token está incluída nesta pasta.
- Os arquivos de `dados-simulados/` foram gerados pelo próprio app a partir do perfil da planta. A origem está marcada como simulada e não são medições reais.
- Os números de validação citados vêm da documentação do projeto: 475 verificações na versão 2.4 e 24 verificações focadas na 2.4.1. Eles não representam precisão agronômica.

Versão dos slides: novembro de 2026, alinhada ao Raiz 2.4.1.
