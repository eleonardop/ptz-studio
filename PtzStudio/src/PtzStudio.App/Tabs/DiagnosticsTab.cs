using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PtzStudio.App.Engine;
using PtzStudio.App.Ui;
using PtzStudio.Core.Input;
using PtzStudio.Core.Visca;

namespace PtzStudio.App.Tabs;

internal sealed class DiagnosticsTab : TabBase
{
    private readonly TextBlock _pad = Ux.Text("", 14);
    private readonly TextBlock _cam = Ux.Text("", 14);
    private readonly TextBlock _test = Ux.Text("", 14, true);
    private TextBox _log = new();
    private LogEntry? _lastShown;

    public DiagnosticsTab(AppEngine e) : base(e) { Init(); }

    protected override void Build()
    {
        _log = new TextBox { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12, Height = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        _lastShown = null;
        void Pulse(double x, double y, double z) => E.Controller.Pulse(x, y, z, 700, DateTime.UtcNow);

        Content = Ux.Scroll(Ux.V(
            Ux.Group("Controle", _pad),
            Ux.Group("Câmera", _cam),
            Ux.Group("Testes individuais",
                Ux.V(
                    Ux.Wrap(
                        Ux.Btn("Testar conexão", async () =>
                        {
                            _test.Text = "Testando…";
                            bool ok = await E.Client.TestConnectionAsync();
                            _test.Text = ok ? $"Câmera respondeu em {E.Client.LastRoundTripMs:0} ms" : "SEM resposta da câmera";
                        }, 150),
                        Ux.Btn("Pan →", () => Pulse(1, 0, 0), 90), Ux.Btn("Pan ←", () => Pulse(-1, 0, 0), 90),
                        Ux.Btn("Tilt ↑", () => Pulse(0, 1, 0), 90), Ux.Btn("Tilt ↓", () => Pulse(0, -1, 0), 90),
                        Ux.Btn("Zoom +", () => Pulse(0, 0, 0.6), 90), Ux.Btn("Zoom −", () => Pulse(0, 0, -0.6), 90),
                        Ux.Btn("Home", () => E.Controller.Home(), 90),
                        Ux.Btn("PARAR", () => E.Controller.StopAll("teste de parada"), 100, Ux.Danger)),
                    Ux.Wrap(
                        Ux.Btn("Recalibrar repouso dos eixos", () => E.RecalibrateRest(), 250),
                        Ux.Btn("Salvar registro em arquivo…", SaveLog, 230),
                        Ux.Btn("Abrir pasta de registros", () => { Directory.CreateDirectory(E.Store.LogDirectory); System.Diagnostics.Process.Start("explorer.exe", E.Store.LogDirectory); }, 230)),
                    _test)),
            Ux.Group("Registro de comandos enviados (TX), respostas (RX) e erros", _log)));
    }

    private void SaveLog()
    {
        var d = new SaveFileDialog { Filter = "Texto (*.txt)|*.txt", FileName = $"ptz-studio-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
        if (d.ShowDialog() == true) File.WriteAllLines(d.FileName, E.Client.RecentLog.Select(x => x.ToString()));
    }

    protected override void Refresh()
    {
        var p = E.Pad;
        if (!p.Connected) _pad.Text = "Nenhum controle detectado.\nConecte o GameSir por USB ou Bluetooth. Se ele estiver pareado mas não aparece, tente trocar o modo do controle.";
        else
        {
            var pressed = string.Join(", ", E.Last.Pressed.OrderBy(i => i).Select(VirtualButtons.Describe));
            var axes = E.LastSnapshot == null ? "" : string.Join("   ", E.LastSnapshot.Axes.Select((v, i) => $"{InputSnapshot.AxisNames[i]}={v:0.00}").Take(Math.Max(p.AxisCount, 1)));
            _pad.Text = $"Nome: {p.Name}\nVID:PID = {p.Vid:X4}:{p.Pid:X4}  (o par muda conforme o modo/conexão; compare USB × Bluetooth)\n" +
                        $"Tipo de conexão: o Windows não informa diretamente; use VID:PID e o modo do controle.\n" +
                        $"Capacidades: {p.ButtonCount} botões, {p.AxisCount} eixos, {p.PovCount} direcional(is)\n" +
                        $"Eixos: {axes}\nPressionados agora: {(pressed.Length == 0 ? "nenhum" : pressed)}";
        }
        var c = E.Camera; var cl = E.Client;
        _cam.Text = $"{c.Name} — {c.IpAddress}:{c.Port} — {(c.Transport == PtzStudio.Core.Models.ViscaTransportMode.UdpPuro ? "UDP puro" : "VISCA over IP")}\n" +
                    $"Estado: {cl.State} — {cl.StateDetail}\n" +
                    $"Tempo de resposta: {(cl.LastRoundTripMs >= 0 ? cl.LastRoundTripMs.ToString("0") + " ms" : "—")}\n" +
                    $"Enviado agora: Pan {E.Controller.SentPan}  Tilt {E.Controller.SentTilt}  Zoom {E.Controller.SentZoom}";
        var log = cl.RecentLog;
        var last = log.LastOrDefault();
        if (!ReferenceEquals(last, _lastShown))
        {
            _lastShown = last;
            _log.Text = string.Join("\n", log.Reverse().Take(200).Select(x => x.ToString()));
        }
    }
}
