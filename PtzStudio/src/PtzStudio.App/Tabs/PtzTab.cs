using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PtzStudio.App.Engine;
using PtzStudio.App.Ui;
using PtzStudio.Core.Models;

namespace PtzStudio.App.Tabs;

internal sealed class PtzTab : TabBase
{
    private VirtualJoystick _joy = new();
    private readonly TextBlock _info = Ux.Text("", 15);
    private double _zoomIntensity = 0.5;
    private ComboBox? _source;

    public PtzTab(AppEngine e) : base(e) { Init(); }

    private static Button Hold(string text, Action down, Action up)
    {
        var b = new Button { Content = text, MinHeight = 80, MinWidth = 150, FontSize = 20, Margin = new Thickness(4), FontWeight = FontWeights.SemiBold };
        b.PreviewMouseLeftButtonDown += (_, _) => { b.CaptureMouse(); down(); };
        b.PreviewMouseLeftButtonUp += (_, _) => { b.ReleaseMouseCapture(); up(); };
        b.LostMouseCapture += (_, _) => up();
        return b;
    }

    protected override void Build()
    {
        var g = new Grid { Margin = new Thickness(16) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 300 });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 280 });

        _joy = new VirtualJoystick { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
        _joy.ValueChanged += (x, y) => { E.VirtualX = x; E.VirtualY = y; };
        var left = Ux.V(
            Ux.Text("Joystick virtual (Pan / Tilt)", 17, true),
            _joy,
            Ux.Btn("CENTRALIZAR / PARAR", () => { _joy.Release(); E.VirtualX = E.VirtualY = 0; }, 260),
            Ux.Text("Quanto mais longe do centro, mais rápido. Usa a sensibilidade, curva e zona morta do perfil ativo.", 13, false, Ux.Muted));
        Grid.SetColumn(left, 0); g.Children.Add(left);

        var zoomIn = Hold("ZOOM  +  (aproximar)", () => E.VirtualZ = _zoomIntensity, () => E.VirtualZ = 0);
        var zoomOut = Hold("ZOOM  −  (afastar)", () => E.VirtualZ = -_zoomIntensity, () => E.VirtualZ = 0);
        var mid = Ux.V(
            Ux.Text("Zoom", 17, true), zoomIn, zoomOut,
            Ux.SliderRow("Intensidade", 0.1, 1.0, _zoomIntensity, v => _zoomIntensity = v),
            Ux.Text("Entrada ativa", 15, true));
        _source = Ux.Combo(new[] { "Joystick físico (GameSir)", "Joystick virtual (mouse)" }, (int)E.Controller.ActiveSource, i => E.SetSource((InputSource)i), 260);
        mid.Children.Add(_source);
        Grid.SetColumn(mid, 1); g.Children.Add(mid);

        var levels = E.Profile.Motion.SpeedLevels;
        var speedBtns = new WrapPanel();
        for (int i = 0; i < levels.Length; i++)
        {
            int idx = i;
            speedBtns.Children.Add(Ux.Btn($"Nível {i + 1} ({levels[i]:P0})", () => { E.Controller.SpeedLevelIndex = idx; }, 120));
        }
        var presets = new WrapPanel();
        foreach (var p in E.Settings.Presets.Where(x => x.CameraId == E.Camera.Id))
        {
            int slot = p.Slot;
            presets.Children.Add(Ux.Btn($"{p.Slot}: {p.Name}", () => E.Controller.RecallPreset(slot), 120));
        }
        if (presets.Children.Count == 0) presets.Children.Add(Ux.Text("Nenhum preset cadastrado (aba Presets).", 13, false, Ux.Muted));

        var right = Ux.V(
            Ux.Btn("■  PARAR TUDO", () => E.Controller.StopAll("botão Parar da tela"), 260, Ux.Danger),
            Ux.Btn("Posição inicial (Home)", () => E.Controller.Home(), 260),
            Ux.Text("Velocidade", 15, true), speedBtns,
            Ux.Text("Presets rápidos", 15, true), presets,
            _info);
        ((Button)right.Children[0]).MinHeight = 70;
        Grid.SetColumn(right, 2); g.Children.Add(right);

        Content = Ux.Scroll(g);
    }

    protected override void Refresh()
    {
        var c = E.Controller;
        var lv = E.Profile.Motion.SpeedLevels;
        int li = Math.Clamp(c.SpeedLevelIndex, 0, Math.Max(0, lv.Length - 1));
        _info.Text = $"Perfil: {E.Settings.ActiveProfile}\n" +
                     $"Nível de velocidade: {li + 1} de {lv.Length}\n" +
                     $"Enviado agora → Pan {c.SentPan:+0;-0;0}   Tilt {c.SentTilt:+0;-0;0}   Zoom {c.SentZoom:+0;-0;0}" +
                     (c.EmergencyLatched ? "\nEMERGÊNCIA ativa: leve os controles ao neutro para liberar." : "") +
                     (c.ActiveSource != InputSource.Virtual ? "\nJoystick e zoom da TELA estão desativados: em “Entrada ativa” escolha “Joystick virtual (mouse)”." : "");
        if (_source != null && _source.SelectedIndex != (int)c.ActiveSource) _source.SelectedIndex = (int)c.ActiveSource;
    }
}
