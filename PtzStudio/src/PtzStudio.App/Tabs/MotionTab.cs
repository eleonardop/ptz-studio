using System.Windows;
using System.Windows.Controls;
using PtzStudio.App.Engine;
using PtzStudio.App.Ui;
using PtzStudio.Core.Models;

namespace PtzStudio.App.Tabs;

/// <summary>"Sensibilidade e movimentos": Pan e Tilt independentes + níveis de velocidade.</summary>
internal sealed class MotionTab : TabBase
{
    public MotionTab(AppEngine e) : base(e) { Init(); }

    protected override void Build()
    {
        var m = E.Profile.Motion;
        var levels = Ux.V();
        for (int i = 0; i < m.SpeedLevels.Length; i++)
        {
            int idx = i;
            levels.Children.Add(Ux.SliderRow($"Nível {i + 1} (fração da velocidade máxima)", 0.05, 1.0, m.SpeedLevels[i], v => { m.SpeedLevels[idx] = v; E.Save(); }));
        }
        levels.Children.Add(Ux.Labeled("Nível inicial do perfil",
            Ux.Combo(Enumerable.Range(1, m.SpeedLevels.Length).Select(i => $"Nível {i}").ToList(), m.DefaultSpeedLevel, i => { m.DefaultSpeedLevel = i; E.Save(); }, 160)));
        levels.Children.Add(Ux.Text("O botão “Alternar velocidade” percorre estes níveis. Eles multiplicam as velocidades de Pan, Tilt e Zoom.", 13, false, Ux.Muted));

        Content = Ux.Scroll(Ux.V(
            Ux.Group("Pan (horizontal)", new AxisSettingsPanel(m.Pan, 24, E.Save)),
            Ux.Group("Tilt (vertical)", new AxisSettingsPanel(m.Tilt, 20, E.Save)),
            Ux.Group("Níveis de velocidade", levels),
            Ux.Text("Referência: com a curva Progressiva, 10–20% de inclinação gera movimento muito lento e 100% chega à velocidade máxima configurada. A zona morta é descontada antes da curva.", 13, false, Ux.Muted)));
    }
}

internal sealed class ZoomTab : TabBase
{
    public ZoomTab(AppEngine e) : base(e) { Init(); }

    protected override void Build()
    {
        var m = E.Profile.Motion;
        Content = Ux.Scroll(Ux.V(
            Ux.Group("Zoom progressivo", new AxisSettingsPanel(m.Zoom, 8, E.Save)),
            Ux.Text("O zoom usa velocidade variável do VISCA (8 níveis). Soltar o controle envia parada imediata, a menos que “Parada suave” esteja marcada.", 13, false, Ux.Muted)));
    }
}
