using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PtzStudio.App.Ui;

/// <summary>Joystick virtual para o mouse: valores X (direita +) e Y (cima +) de -1 a 1, proporcionais ao deslocamento.</summary>
internal sealed class VirtualJoystick : Canvas
{
    private const double Size = 260, KnobR = 34;
    private readonly Ellipse _knob;
    public double X { get; private set; }
    public double Y { get; private set; }
    public event Action<double, double>? ValueChanged;

    public VirtualJoystick()
    {
        Width = Size; Height = Size; Background = Brushes.Transparent; Cursor = Cursors.Hand;
        var ring = new Ellipse { Width = Size - 6, Height = Size - 6, Stroke = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)), StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)) };
        SetLeft(ring, 3); SetTop(ring, 3);
        Children.Add(ring);
        foreach (var f in new[] { 0.5 })
        {
            var h = new Line { X1 = 20, X2 = Size - 20, Y1 = Size / 2, Y2 = Size / 2, Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 } };
            var v = new Line { Y1 = 20, Y2 = Size - 20, X1 = Size / 2, X2 = Size / 2, Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 } };
            Children.Add(h); Children.Add(v);
        }
        _knob = new Ellipse { Width = KnobR * 2, Height = KnobR * 2, Fill = Ux.Accent, Stroke = Brushes.White, StrokeThickness = 3 };
        Children.Add(_knob);
        Place(0, 0);

        MouseLeftButtonDown += (_, e) => { CaptureMouse(); Move(e.GetPosition(this)); };
        MouseMove += (_, e) => { if (IsMouseCaptured) Move(e.GetPosition(this)); };
        MouseLeftButtonUp += (_, _) => Release();
        LostMouseCapture += (_, _) => Release();
    }

    private void Move(Point p)
    {
        double c = Size / 2, max = c - KnobR - 4;
        double dx = p.X - c, dy = p.Y - c;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len > max) { dx = dx / len * max; dy = dy / len * max; }
        Set(dx / max, -dy / max);
    }

    public void Release()
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        Set(0, 0);
    }

    private void Set(double x, double y)
    {
        if (x == X && y == Y) return;
        X = x; Y = y;
        Place(x, y);
        ValueChanged?.Invoke(x, y);
    }

    private void Place(double x, double y)
    {
        double c = Size / 2, max = c - KnobR - 4;
        SetLeft(_knob, c + x * max - KnobR);
        SetTop(_knob, c - y * max - KnobR);
    }
}
