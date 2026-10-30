using System.Windows;
using System.Windows.Controls;
using PtzStudio.App.Engine;
using PtzStudio.App.Ui;
using PtzStudio.Core.Input;
using PtzStudio.Core.Models;

namespace PtzStudio.App.Tabs;

internal sealed class AnalogTab : TabBase
{
    private readonly List<ProgressBar> _bars = new();
    private readonly TextBlock _live = Ux.Text("", 14);
    private readonly TextBlock _detectInfo = Ux.Text("", 14, false, Ux.Warn);

    public AnalogTab(AppEngine e) : base(e) { Init(); }

    private static List<string> AxisItems()
    {
        var l = new List<string> { "(nenhum)" };
        l.AddRange(InputSnapshot.AxisNames);
        return l;
    }

    private UIElement AxisRow(string label, Func<int> get, Action<int> set)
    {
        ComboBox? combo = null;
        combo = Ux.Combo(AxisItems(), get() + 1, i => { set(i - 1); E.Save(); }, 160);
        var btn = Ux.Btn("Detectar eixo", () =>
        {
            if (!E.Pad.Connected) { MessageBox.Show("Nenhum controle detectado.", "Detectar eixo"); return; }
            _detectInfo.Text = $"Mova o analógico/gatilho de “{label}” até o fim…";
            E.BeginDetectAxis(a => { set(a); combo!.SelectedIndex = a + 1; E.Save(); _detectInfo.Text = $"Detectado: eixo {InputSnapshot.AxisNames[a]}."; });
        }, 130);
        return Ux.H(Ux.Text(label, 14).Also(t => t.Width = 230), combo, btn);
    }

    protected override void Build()
    {
        var m = E.Profile.Motion;
        var map = E.Profile.AxisMap;

        var axes = Ux.Group("Qual eixo do controle comanda cada movimento",
            Ux.V(
                AxisRow("Pan (esquerda/direita)", () => map.PanAxis, v => map.PanAxis = v),
                AxisRow("Tilt (cima/baixo)", () => map.TiltAxis, v => map.TiltAxis = v),
                AxisRow("Zoom (analógico)", () => map.ZoomAxis, v => map.ZoomAxis = v),
                AxisRow("Zoom aproximar (gatilho)", () => map.ZoomTeleTriggerAxis, v => map.ZoomTeleTriggerAxis = v),
                AxisRow("Zoom afastar (gatilho)", () => map.ZoomWideTriggerAxis, v => map.ZoomWideTriggerAxis = v),
                _detectInfo,
                Ux.Text("Os números dos eixos dependem do modo do controle (XInput/DInput). Use “Detectar eixo” e confira nas barras abaixo.", 13, false, Ux.Muted)));

        _bars.Clear();
        var barPanel = new StackPanel();
        for (int i = 0; i < 8; i++)
        {
            var pb = new ProgressBar { Minimum = 0, Maximum = 1, Height = 16, Width = 260, Margin = new Thickness(4) };
            _bars.Add(pb);
            barPanel.Children.Add(Ux.H(Ux.Text(InputSnapshot.AxisNames[i], 13).Also(t => t.Width = 70), pb));
        }
        var liveGroup = Ux.Group("Valores em tempo real", Ux.V(barPanel, _live));

        var dir = Ux.Group("Diagonais, zonas mortas e tolerância",
            Ux.V(
                Ux.SliderRow("Zona morta horizontal (Pan)", 0, 0.5, m.Pan.Deadzone, v => { m.Pan.Deadzone = v; E.Save(); }),
                Ux.SliderRow("Zona morta vertical (Tilt)", 0, 0.5, m.Tilt.Deadzone, v => { m.Tilt.Deadzone = v; E.Save(); }),
                Ux.SliderRow("Tolerância angular (graus)", 0, 44, m.AngularToleranceDeg, v => { m.AngularToleranceDeg = v; E.Save(); }, "0"),
                Ux.Labeled("Prioridade de eixo", Ux.Combo(new[] { "Nenhuma", "Horizontal (favorece Pan)", "Vertical (favorece Tilt)" }, (int)m.Priority, i => { m.Priority = (AxisPriority)i; E.Save(); }, 260)),
                Ux.Check("Restringir a UM eixo por vez (sem diagonal)", m.LockSingleAxis, v => { m.LockSingleAxis = v; E.Save(); }),
                Ux.Text("Tolerância angular: se o analógico estiver quase na horizontal (ou vertical), o desvio pequeno no outro eixo é ignorado e o movimento não vira diagonal.", 13, false, Ux.Muted)));

        var inv = Ux.Group("Inversão de direção",
            Ux.H(
                Ux.Check("Inverter Pan", m.Pan.Invert, v => { m.Pan.Invert = v; E.Save(); }),
                Ux.Check("Inverter Tilt", m.Tilt.Invert, v => { m.Tilt.Invert = v; E.Save(); }),
                Ux.Check("Inverter Zoom", m.Zoom.Invert, v => { m.Zoom.Invert = v; E.Save(); })));

        void Pulse(double x, double y, double z) => E.Controller.Pulse(x, y, z, 700, DateTime.UtcNow);
        var calib = Ux.Group("Calibração: teste a direção real na câmera",
            Ux.V(
                Ux.Text("Cada botão simula o analógico naquela direção por 0,7 s (já com as inversões ativas). Se a câmera for para o lado errado, marque a inversão correspondente acima.", 13, false, Ux.Muted),
                Ux.Wrap(
                    Ux.Btn("Analógico → DIREITA", () => Pulse(1, 0, 0), 190), Ux.Btn("Analógico ← ESQUERDA", () => Pulse(-1, 0, 0), 190),
                    Ux.Btn("Analógico ↑ CIMA", () => Pulse(0, 1, 0), 190), Ux.Btn("Analógico ↓ BAIXO", () => Pulse(0, -1, 0), 190),
                    Ux.Btn("Zoom: aproximar", () => Pulse(0, 0, 0.6), 190), Ux.Btn("Zoom: afastar", () => Pulse(0, 0, -0.6), 190))));

        Content = Ux.Scroll(Ux.V(axes, liveGroup, dir, inv, calib));
    }

    protected override void Refresh()
    {
        var s = E.LastSnapshot;
        if (s == null) { _live.Text = "Controle não detectado."; return; }
        for (int i = 0; i < _bars.Count && i < s.Axes.Length; i++) _bars[i].Value = s.Axes[i];
        string Dir(double v, string neg, string pos) => Math.Abs(v) < 0.05 ? "neutro" : (v > 0 ? pos : neg);
        _live.Text = $"Entrada processada → X {E.Last.X:+0.00;-0.00;0.00} ({Dir(E.Last.X, "esquerda", "direita")})   " +
                     $"Y {E.Last.Y:+0.00;-0.00;0.00} ({Dir(E.Last.Y, "baixo", "cima")})   Zoom {E.Last.Z:+0.00;-0.00;0.00} ({Dir(E.Last.Z, "afastar", "aproximar")})\n" +
                     $"Enviado à câmera → Pan {E.Controller.SentPan:+0;-0;0}  Tilt {E.Controller.SentTilt:+0;-0;0}  Zoom {E.Controller.SentZoom:+0;-0;0}";
    }
}

internal static class Ext
{
    public static T Also<T>(this T x, Action<T> a) { a(x); return x; }
}
