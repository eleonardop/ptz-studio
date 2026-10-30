# Relatório de implementação

Base: projeto C#/.NET 8 + WPF, dividido em `PtzStudio.Core` (lógica, VISCA, perfis, armazenamento, sem tela) e `PtzStudio.App` (interface e leitura do controle via DirectInput em segundo plano).

Implementado no código: 10 abas; mapeamento visual frente/traseira com clique e detecção; curvas, zona morta, suavização, aceleração/desaceleração; correção de diagonais; inversão por eixo; zoom progressivo; joystick virtual; presets com confirmação pela câmera; perfis; backup/restauração; parada de emergência, watchdog e parada por desconexão; log de eventos e comandos; testes automatizados da lógica (xUnit).

## Limitações reais
- **Este código ainda NÃO foi compilado nem executado.** O ambiente onde foi escrito não tem .NET nem Windows. Podem existir erros de compilação; o primeiro build na nuvem (GitHub Actions) mostrará, e eles precisam ser corrigidos antes de gerar o .EXE.
- O desenho do controle é vetorial, não uma foto; o layout do Nova 2 Lite precisa ser conferido com o controle real.
- Botões traseiros dependem do que o Windows expõe; não é garantido.
- Porta e formato VISCA da LJ510-30X-N não foram confirmados em documentação; há a opção de escolher o protocolo e a porta, e não há detecção automática de porta.
- Não há integração com o OBS (apenas independência dele).
