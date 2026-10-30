using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PtzStudio.App.Ui;

internal enum PartKind { Button, Trigger, Dpad, StickInfo }

internal sealed record PartDef(string Id, string Label, PartKind Kind, double X, double Y, double W, double H, bool Round);

/// <summary>
/// Desenho esquemático (vetorial) do controle, com cada peça clicável. É um desenho aproximado, não uma foto do modelo.
/// A ligação peça -> botão físico é feita pela detecção automática e fica salva em Settings.Layout.
/// </summary>
internal sealed class ControllerView : Viewbox
{
    private readonly Canvas _c = new() { Width = 720, Height = 440 };
    private readonly Dictionary<string, Shape> _shapes = new();
    private readonly Dictionary<string, Rectangle> _fills = new();
    private Ellipse? _knobL, _knobR;
    public string? Selected { get; private set; }
    public bool Front { get; private set; } = true;
    public event Action<string>? PartClicked;

    private static readonly Brush Body = new SolidColorBrush(Color.FromRgb(0x37, 0x3D, 0x4A));
    private static readonly Brush PartBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x73, 0x85));
    private static readonly Brush Pressed = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly Brush Sel = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));

    public static readonly PartDef[] FrontParts =
    {
        new("LT", "LT", PartKind.Trigger, 110, 28, 120, 46, false),
        new("RT", "RT", PartKind.Trigger, 490, 28, 120, 46, false),
        new("LB", "LB", PartKind.Button, 110, 82, 120, 26, false),
        new("RB", "RB", PartKind.Button, 490, 82, 120, 26, false),
        new("VIEW", "◀", PartKind.Button, 305, 168, 26, 26, true),
        new("MENU", "▶", PartKind.Button, 389, 168, 26, 26, true),
        new("Y", "Y", PartKind.Button, 538, 126, 44, 44, true),
        new("X", "X", PartKind.Button, 490, 172, 44, 44, true),
        new("B", "B", PartKind.Button, 586, 172, 44, 44, true),
        new("A", "A", PartKind.Button, 538, 218, 44, 44, true),
        new("DPAD_UP", "↑", PartKind.Dpad, 248, 262, 32, 32, false),
        new("DPAD_LEFT", "←", PartKind.Dpad, 214, 296, 32, 32, false),
        new("DPAD_RIGHT", "→", PartKind.Dpad, 282, 296, 32, 32, false),
        new("DPAD_DOWN", "↓", PartKind.Dpad, 248, 330, 32, 32, false),
        new("L3", "L3", PartKind.Button, 148, 168, 44, 44, true),
        new("R3", "R3", PartKind.Button, 408, 262, 44, 44, true),
    };

    public static readonly PartDef[] RearParts =
    {
        new("EXTRA1", "Extra 1", PartKind.Button, 150, 210, 90, 60, false),
        new("EXTRA2", "Extra 2", PartKind.Button, 270, 210, 90, 60, false),
        new("EXTRA3", "Extra 3", PartKind.Button, 360, 210, 90, 60, false),
        new("EXTRA4", "Extra 4", PartKind.Button, 480, 210, 90, 60, false),
    };

    public static string Describe(string id) => id switch
    {
        "LT" => "Gatilho esquerdo (LT)", "RT" => "Gatilho direito (RT)", "LB" => "Botão superior esquerdo (LB)",
        "RB" => "Botão superior direito (RB)", "VIEW" => "Botão central esquerdo (Select/View)", "MENU" => "Botão central direito (Start/Menu)",
        "L3" => "Clique do analógico esquerdo (L3)", "R3" => "Clique do analógico direito (R3)",
        "DPAD_UP" => "Direcional ↑", "DPAD_DOWN" => "Direcional ↓", "DPAD_LEFT" => "Direcional ←", "DPAD_RIGHT" => "Direcional →",
        "STICK_L" => "Analógico esquerdo (Pan/Tilt)", "STICK_R" => "Analógico direito",
        "EXTRA1" or "EXTRA2" or "EXTRA3" or "EXTRA4" => "Botão extra/traseiro " + id[^1],
        _ => "Botão " + id
    };

    public ControllerView()
    {
        Child = _c; Stretch = Stretch.Uniform; MinHeight = 300;
        Build(true);
    }

    public void Build(bool front)
    {
        Front = front;
        _c.Children.Clear(); _shapes.Clear(); _fills.Clear(); _knobL = _knobR = null;
        var body = new Rectangle { Width = 600, Height = 300, RadiusX = 90, RadiusY = 90, Fill = Body, Stroke = Brushes.Black, StrokeThickness = 3 };
        Canvas.SetLeft(body, 60); Canvas.SetTop(body, 70); _c.Children.Add(body);
        foreach (double gx in new[] { 90.0, 520.0 })
        {
            var grip = new Ellipse { Width = 110, Height = 150, Fill = Body, Stroke = Brushes.Black, StrokeThickness = 3 };
            Canvas.SetLeft(grip, gx); Canvas.SetTop(grip, 270); _c.Children.Add(grip);
        }
        var title = new TextBlock { Text = front ? "VISTA FRONTAL (esquemática)" : "VISTA TRASEIRA", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold };
        Canvas.SetLeft(title, 240); Canvas.SetTop(title, 100); _c.Children.Add(title);

        if (front)
        {
            AddStick("STICK_L", 170, 190, true);
            AddStick("STICK_R", 430, 284, false);
            foreach (var p in FrontParts) AddPart(p);
        }
        else
        {
            var note = new TextBlock
            {
                Text = "Posições livres: o programa não presume botões traseiros.\nSelecione uma posição e use DETECTAR BOTÃO para ligar um botão que o Windows enxergar.",
                Foreground = Brushes.White, FontSize = 13, TextAlignment = TextAlignment.Center, Width = 520
            };
            Canvas.SetLeft(note, 100); Canvas.SetTop(note, 140); _c.Children.Add(note);
            foreach (var p in RearParts) AddPart(p, dashed: true);
        }
        Highlight();
    }

    private void AddStick(string id, double cx, double cy, bool left)
    {
        var ring = new Ellipse { Width = 96, Height = 96, Fill = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x2B)), Stroke = Brushes.Black, StrokeThickness = 2, Cursor = System.Windows.Input.Cursors.Hand };
        Canvas.SetLeft(ring, cx - 48); Canvas.SetTop(ring, cy - 48);
        ring.MouseLeftButtonDown += (_, _) => Select(id);
        _c.Children.Add(ring); _shapes[id] = ring;
        var knob = new Ellipse { Width = 52, Height = 52, Fill = PartBrush, IsHitTestVisible = false };
        Canvas.SetLeft(knob, cx - 26); Canvas.SetTop(knob, cy - 26);
        _c.Children.Add(knob);
        if (left) { _knobL = knob; _stickCenterL = (cx, cy); } else { _knobR = knob; _stickCenterR = (cx, cy); }
    }

    private (double, double) _stickCenterL, _stickCenterR;

    private void AddPart(PartDef p, bool dashed = false)
    {
        Shape s = p.Round
            ? new Ellipse { Width = p.W, Height = p.H }
            : new Rectangle { Width = p.W, Height = p.H, RadiusX = 8, RadiusY = 8 };
        s.Fill = PartBrush; s.Stroke = Brushes.White; s.StrokeThickness = 1.5; s.Cursor = System.Windows.Input.Cursors.Hand;
        if (dashed) s.StrokeDashArray = new DoubleCollection { 4, 3 };
        Canvas.SetLeft(s, p.X); Canvas.SetTop(s, p.Y);
        s.MouseLeftButtonDown += (_, _) => Select(p.Id);
        _c.Children.Add(s); _shapes[p.Id] = s;

        var t = new TextBlock { Text = p.Label, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = p.W < 40 ? 13 : 15, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, Width = p.W };
        Canvas.SetLeft(t, p.X); Canvas.SetTop(t, p.Y + p.H / 2 - 10); _c.Children.Add(t);

        if (p.Kind == PartKind.Trigger)
        {
            var fill = new Rectangle { Width = p.W - 6, Height = 0, Fill = Pressed, IsHitTestVisible = false };
            Canvas.SetLeft(fill, p.X + 3); Canvas.SetTop(fill, p.Y + p.H - 3); _c.Children.Add(fill);
            _fills[p.Id] = fill;
        }
    }

    public void Select(string id)
    {
        Selected = id; Highlight(); PartClicked?.Invoke(id);
    }

    private void Highlight()
    {
        foreach (var kv in _shapes)
        {
            kv.Value.Stroke = kv.Key == Selected ? Sel : Brushes.White;
            kv.Value.StrokeThickness = kv.Key == Selected ? 4 : 1.5;
        }
    }

    public void SetPressed(ISet<string> parts, IDictionary<string, double> triggerAmount)
    {
        foreach (var kv in _shapes)
        {
            if (kv.Key.StartsWith("STICK")) continue;
            kv.Value.Fill = parts.Contains(kv.Key) ? Pressed : PartBrush;
        }
        foreach (var kv in _fills)
        {
            double a = triggerAmount.TryGetValue(kv.Key, out var v) ? Math.Clamp(v, 0, 1) : 0;
            var p = FrontParts.First(x => x.Id == kv.Key);
            kv.Value.Height = (p.H - 6) * a;
            Canvas.SetTop(kv.Value, p.Y + p.H - 3 - kv.Value.Height);
        }
    }

    public void SetSticks(double lx, double ly, double rx, double ry)
    {
        const double r = 22;
        if (_knobL != null) { Canvas.SetLeft(_knobL, _stickCenterL.Item1 - 26 + lx * r); Canvas.SetTop(_knobL, _stickCenterL.Item2 - 26 - ly * r); }
        if (_knobR != null) { Canvas.SetLeft(_knobR, _stickCenterR.Item1 - 26 + rx * r); Canvas.SetTop(_knobR, _stickCenterR.Item2 - 26 - ry * r); }
    }

    public void SetToolTip(string id, string text)
    {
        if (_shapes.TryGetValue(id, out var s)) s.ToolTip = text;
    }
}
