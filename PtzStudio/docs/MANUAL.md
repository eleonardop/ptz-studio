# PTZ Studio — Manual (português)

## Instalação
1. Execute **PTZStudio-Setup.exe** (ou abra direto o **PTZStudio.exe**, que não precisa de instalação).
2. Não é necessário instalar Python, .NET ou qualquer outra ferramenta.

## Primeiros passos
1. Aba **Câmera**: informe o IP da câmera (LJ510-30X-N), a porta UDP e o endereço VISCA. Escolha o protocolo (UDP puro ou VISCA over IP com cabeçalho). Se a câmera não responder, tente o outro protocolo e confira a porta no manual ou no menu de rede da câmera.
2. Conecte o GameSir (Bluetooth ou USB). O cabeçalho mostra o nome do controle quando ele é detectado.
3. Aba **Controle PTZ**: teste com o joystick virtual (mouse) e com o controle físico.
4. Aba **Analógicos**: confira a direção dos eixos e inverta o que estiver ao contrário (as configurações ficam salvas).

## Mapeamento do controle
- Escolha **Frente** ou **Traseira**, clique no botão desenhado, escolha a função e confirme.
- **Detectar botão**: pressione o botão físico; o programa destaca o botão correspondente.
- Botões traseiros (M1/M2) só aparecem se o Windows os enxergar. Se não aparecerem, veja a aba **Diagnóstico**: ela lista tudo o que o Windows recebe do controle.

## Segurança
- Analógico no neutro = parada. Controle desconectado ou câmera sem resposta = parada.
- **PARADA DE EMERGÊNCIA** (botão vermelho no topo) tem prioridade sobre qualquer comando e só libera depois que os controles voltam ao neutro.

## Presets
O preset é gravado na **memória da câmera** por comando VISCA. O programa só marca como confirmado quando a câmera responde; antes disso aparece como “aguardando confirmação”.

## Backup
Aba **Backup e restauração**: criar backup, restaurar, exportar e importar configurações. Um backup automático é feito ao abrir o programa.

## Onde ficam os dados
Pasta `%AppData%\PTZStudio` (configurações, backups e logs).
