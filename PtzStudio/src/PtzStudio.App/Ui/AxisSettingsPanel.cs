using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PtzStudio.Core.Models;
using PtzStudio.Core.Motion;

namespace PtzStudio.App.Ui;

/// <summary>Todos os ajustes de um eixo (Pan, Tilt ou Zoom) com pré-visualização da curva entrada -> velocidade.</summary>
internal sealed class AxisSettingsPanel : StackPanel
{
    private readonly AxisSettings _s;
    private readonly int _limit;
    private readonly Polyline _line = new() { Stroke = Ux.Accent, StrokeThickness = 3 };
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(700) };

    public AxisSettingsPanel(AxisSettings s, int speedLimit, Action save)
    {
        _s = s; _limit = speedLimit;
        _debounce.Tick += (_, _) => { _debounce.Stop(); save(); };
        void Changed() { Redraw(); _debounce.Stop(); _debounce.Start(); }

        Children.Add(Ux.Check("Inverter direção", s.Invert, v => { s.Invert = v; Changed(); }));
        Children.Add(Ux.SliderRow("Sensibilidade (multiplicador da entrada)", 0.2, 2.0, s.Sensitivity, v => { s.Sensitivity = v; Changed(); }));
        Children.Add(Ux.SliderRow("Zona morta (deadzone)", 0.0, 0.5, s.Deadzone, v => { s.Deadzone = v; Changed(); }));
        Children.Add(Ux.SliderRow("Velocidade mínima", 1, speedLimit, s.MinSpeed, v => { s.MinSpeed = (int)Math.Round(v); Changed(); }, "0"));
        Children.Add(Ux.SliderRow("Velocidade máxima", 1, speedLimit, s.MaxSpeed, v => { s.MaxSpeed = (int)Math.Round(v); Changed(); }, "0"));
        var curveRow = Ux.Labeled("Curva de resposta", Ux.Combo(new[] { "Linear", "Progressiva (suave no início)", "Suave (S)", "Agressiva" }, (int)s.Curve, i => { s.Curve = (ResponseCurve)i; Changed(); }, 260));
        Children.Add(curveRow);
        Children.Add(Ux.SliderRow("Intensidade da curva (expoente)", 1.0, 4.0, s.CurveExponent, v => { s.CurveExponent = v; Changed(); }));
        Children.Add(Ux.SliderRow("Aceleração (unid./s; 0 = instantânea)", 0, 200, s.AccelPerSecond, v => { s.AccelPerSecond = v; Changed(); }, "0"));
        Children.Add(Ux.SliderRow("Desaceleração (unid./s; 0 = instantânea)", 0, 400, s.DecelPerSecond, v => { s.DecelPerSecond = v; Changed(); }, "0"));
        Children.Add(Ux.SliderRow("Suavização", 0.0, 0.9, s.Smoothing, v => { s.Smoothing = v; Changed(); }));
        Children.Add(Ux.SliderRow("Filtro de ruído", 0.0, 0.1, s.NoiseThreshold, v => { s.NoiseThreshold = v; Changed(); }, "0.000"));
        Children.Add(Ux.Check("Parada suave ao soltar o analógico (por padrão a câmera para na hora)", s.SoftStopOnRelease, v => { s.SoftStopOnRelease = v; Changed(); }));

        var box = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Width = 260, Height = 150, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), Background = Brushes.White };
        var cv = new Canvas { Width = 258, Height = 148, ClipToBounds = true };
        cv.Children.Add(_line);
        box.Child = cv;
        Children.Add(Ux.Text("Prévia: inclinação do analógico (→) × velocidade da câmera (↑)", 13, false, Ux.Muted));
        Children.Add(box);
        Redraw();
    }

    private void Redraw()
    {
        var pts = new PointCollection();
        for (int i = 0; i <= 50; i++)
        {
            double input = i / 50.0;
            double v = AxisMath.Deadzone(Math.Clamp(input * _s.Sensitivity, 0, 1), _s.Deadzone);
            double speed = Math.Abs(AxisMath.TargetSpeed(v, _s));
            pts.Add(new Point(input * 256 + 1, 146 - Math.Min(1.0, speed / Math.Max(1, _limit)) * 142));
        }
        _line.Points = pts;
    }
}
