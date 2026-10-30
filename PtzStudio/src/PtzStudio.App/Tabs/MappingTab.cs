using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PtzStudio.App.Engine;
using PtzStudio.App.Ui;
using PtzStudio.Core.Input;
using PtzStudio.Core.Models;

namespace PtzStudio.App.Tabs;

internal sealed class MappingTab : TabBase
{
    private ControllerView _view = new();
    private string? _sel;
    private readonly TextBlock _title = Ux.Text("Clique em um botão do desenho.", 17, true);
    private readonly TextBlock _idLbl = Ux.Text("", 14);
    private readonly TextBlock _funcLbl = Ux.Text("", 14);
    private readonly TextBlock _live = Ux.Text("", 14);
    private readonly TextBlock _events = Ux.Text("", 13, false, Ux.Muted);
    private ComboBox _func = new();
    private ComboBox _mod = new();
    private readonly TextBox _param = new() { Width = 80, FontSize = 15, MinHeight = 34, Margin = new Thickness(4), Text = "1" };
    private Button _detectBtn = new();
    private readonly List<string> _log = new();
    private HashSet<string> _prev = new();
    private List<string> _modParts = new();
    private bool _detecting;

    public MappingTab(AppEngine e) : base(e) { Init(); }

    private ButtonBinding? Current()
    {
        if (_sel == null || !E.Settings.Layout.TryGetValue(_sel, out int idx) || idx < 0) return null;
        int mod = ModifierId();
        return E.Profile.Bindings.FirstOrDefault(b => b.Button == idx && b.Modifier == mod);
    }

    private int ModifierId()
    {
        if (_mod.SelectedIndex <= 0 || _mod.SelectedIndex - 1 >= _modParts.Count) return -1;
        return E.Settings.Layout[_modParts[_mod.SelectedIndex - 1]];
    }

    protected override void Build()
    {
        bool front = _view.Front;
        _view = new ControllerView();
        _view.Build(front);
        _view.PartClicked += id => { _sel = id; ShowSelection(); };
        RefreshTooltips();

        var front_ = new RadioButton { Content = "Frente", IsChecked = front, FontSize = 15, Margin = new Thickness(6), GroupName = "vista" };
        var rear = new RadioButton { Content = "Traseira", IsChecked = !front, FontSize = 15, Margin = new Thickness(6), GroupName = "vista" };
        front_.Checked += (_, _) => { _view.Build(true); _sel = null; ShowSelection(); RefreshTooltips(); };
        rear.Checked += (_, _) => { _view.Build(false); _sel = null; ShowSelection(); RefreshTooltips(); };

        _modParts = E.Settings.Layout.Where(kv => kv.Value >= 0).Select(kv => kv.Key).OrderBy(x => x).ToList();
        var modItems = new List<string> { "(nenhum — botão isolado)" };
        modItems.AddRange(_modParts.Select(ControllerView.Describe));
        _mod = Ux.Combo(modItems, 0, _ => ShowSelection(), 330);

        var names = ActionCatalog.Todas.Select(t => t.Nome).ToList();
        _func = Ux.Combo(names, 0, _ => { }, 330);
        _detectBtn = Ux.Btn("DETECTAR BOTÃO", ToggleDetect, 330, Ux.Accent);

        var panel = Ux.V(
            _title, _idLbl, _funcLbl,
            Ux.Text("Função do botão", 14, true), _func,
            Ux.Labeled("Parâmetro (nº do preset)", _param, 200),
            Ux.Text("Combinação (segurar este botão + o botão selecionado)", 14, true), _mod,
            Ux.Wrap(Ux.Btn("Aplicar", Apply, 110), Ux.Btn("Remover", Remove, 110), Ux.Btn("Testar", TestFunction, 110)),
            _detectBtn, _live, Ux.Text("Eventos do controle", 14, true), _events);

        var g = new Grid { Margin = new Thickness(16) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 380 });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
        var leftSide = new DockPanel();
        var top = Ux.H(Ux.Text("MAPEAMENTO DO CONTROLE", 18, true), front_, rear);
        DockPanel.SetDock(top, Dock.Top);
        leftSide.Children.Add(top);
        leftSide.Children.Add(_view);
        Grid.SetColumn(leftSide, 0); g.Children.Add(leftSide);
        var sc = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(sc, 1); g.Children.Add(sc);
        Content = g;
        ShowSelection();
    }

    private void RefreshTooltips()
    {
        foreach (var kv in E.Settings.Layout)
        {
            var b = E.Profile.Bindings.FirstOrDefault(x => x.Button == kv.Value && x.Modifier == -1 && x.Action != ButtonAction.Nenhuma);
            _view.SetToolTip(kv.Key, ControllerView.Describe(kv.Key) + "\n" + (b == null ? "Sem função" : ActionCatalog.Nome(b.Action)));
        }
    }

    private void ShowSelection()
    {
        if (_sel == null) { _title.Text = "Clique em um botão do desenho."; _idLbl.Text = ""; _funcLbl.Text = ""; return; }
        _title.Text = ControllerView.Describe(_sel);
        if (_sel.StartsWith("STICK"))
        {
            _idLbl.Text = "Os analógicos são configurados na aba “Configuração dos analógicos”.";
            _funcLbl.Text = ""; return;
        }
        int idx = E.Settings.Layout.TryGetValue(_sel, out var v) ? v : -1;
        _idLbl.Text = "Identificador detectado: " + VirtualButtons.Describe(idx);
        var cur = Current();
        _funcLbl.Text = "Função atual: " + (cur == null ? "(nenhuma)" : ActionCatalog.Nome(cur.Action) + (ActionCatalog.UsesParameter(cur.Action) ? $" — preset {cur.Parameter}" : ""));
        if (cur != null)
        {
            _func.SelectedIndex = ActionCatalog.Todas.ToList().FindIndex(t => t.Action == cur.Action);
            _param.Text = cur.Parameter.ToString();
        }
        else _func.SelectedIndex = 0;
    }

    private void Apply()
    {
        if (_sel == null || _sel.StartsWith("STICK")) return;
        int idx = E.Settings.Layout[_sel];
        if (idx < 0) { MessageBox.Show("Este botão ainda não foi detectado. Use DETECTAR BOTÃO e pressione o botão físico.", "Mapeamento"); return; }
        var action = ActionCatalog.Todas[Math.Max(0, _func.SelectedIndex)].Action;
        int mod = ModifierId();
        int.TryParse(_param.Text, out int prm);
        var existing = BindingValidator.FindConflict(E.Profile, idx, mod);
        if (action == ButtonAction.Nenhuma) { Remove(); return; }
        if (existing != null && existing.Action != action)
        {
            var r = MessageBox.Show($"Este botão já tem a função “{ActionCatalog.Nome(existing.Action)}”.\n\nSim = substituir pela nova função\nNão = manter as duas (duplicar de propósito)\nCancelar = não alterar",
                "Atribuição já existente", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) return;
            if (r == MessageBoxResult.Yes) { existing.Action = action; existing.Parameter = prm; }
            else E.Profile.Bindings.Add(new ButtonBinding { Button = idx, Modifier = mod, Action = action, Parameter = prm });
        }
        else if (existing != null) existing.Parameter = prm;
        else E.Profile.Bindings.Add(new ButtonBinding { Button = idx, Modifier = mod, Action = action, Parameter = prm });
        E.Save(); RefreshTooltips(); ShowSelection();
    }

    private void Remove()
    {
        if (_sel == null || !E.Settings.Layout.TryGetValue(_sel, out int idx) || idx < 0) return;
        int mod = ModifierId();
        E.Profile.Bindings.RemoveAll(b => b.Button == idx && b.Modifier == mod);
        E.Save(); RefreshTooltips(); ShowSelection();
    }

    private void TestFunction()
    {
        var action = ActionCatalog.Todas[Math.Max(0, _func.SelectedIndex)].Action;
        int.TryParse(_param.Text, out int prm);
        var now = DateTime.UtcNow;
        switch (action)
        {
            case ButtonAction.PanEsquerda: E.Controller.Pulse(-1, 0, 0, 800, now); break;
            case ButtonAction.PanDireita: E.Controller.Pulse(1, 0, 0, 800, now); break;
            case ButtonAction.TiltCima: E.Controller.Pulse(0, 1, 0, 800, now); break;
            case ButtonAction.TiltBaixo: E.Controller.Pulse(0, -1, 0, 800, now); break;
            case ButtonAction.ZoomAproximar: E.Controller.Pulse(0, 0, 0.6, 800, now); break;
            case ButtonAction.ZoomAfastar: E.Controller.Pulse(0, 0, -0.6, 800, now); break;
            case ButtonAction.FocoProximo:
            case ButtonAction.FocoDistante:
                E.Controller.PulseFocus(action == ButtonAction.FocoDistante ? 1 : -1, 700, now);
                break;
            case ButtonAction.Nenhuma: break;
            default: E.Execute(action, prm); break;
        }
    }

    private void ToggleDetect()
    {
        if (_sel == null || _sel.StartsWith("STICK")) { MessageBox.Show("Selecione um botão no desenho primeiro.", "Detectar botão"); return; }
        if (!E.Pad.Connected) { MessageBox.Show("Nenhum controle detectado. Conecte o GameSir (USB ou Bluetooth) e tente de novo.", "Detectar botão"); return; }
        if (_detecting) { E.CancelDetect(); _detecting = false; _detectBtn.Content = "DETECTAR BOTÃO"; _live.Text = "Detecção cancelada."; return; }
        _detecting = true;
        _detectBtn.Content = "Pressione o botão físico agora… (clique para cancelar)";
        _live.Text = $"Aguardando: pressione o botão que corresponde a “{ControllerView.Describe(_sel)}”.";
        string part = _sel;
        E.BeginDetectButton(id =>
        {
            _detecting = false; _detectBtn.Content = "DETECTAR BOTÃO";
            var other = E.Settings.Layout.FirstOrDefault(kv => kv.Value == id && kv.Key != part);
            if (other.Key != null)
            {
                var r = MessageBox.Show($"{VirtualButtons.Describe(id)} já estava ligado a “{ControllerView.Describe(other.Key)}”. Mover para “{ControllerView.Describe(part)}”?",
                    "Botão já usado", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) { _live.Text = "Mantido como estava."; return; }
                E.Settings.Layout[other.Key] = -1;
            }
            E.Settings.Layout[part] = id;
            E.Save(); RefreshTooltips(); ShowSelection();
            _live.Text = $"Detectado: {VirtualButtons.Describe(id)} → {ControllerView.Describe(part)}. Escolha a função e clique em Aplicar.";
        });
    }

    protected override void Refresh()
    {
        var pressedIds = E.Last.Pressed;
        var parts = new HashSet<string>();
        var trig = new Dictionary<string, double>();
        foreach (var kv in E.Settings.Layout)
        {
            if (kv.Value < 0) continue;
            if (pressedIds.Contains(kv.Value)) parts.Add(kv.Key);
            if (kv.Value >= 2000 && E.LastSnapshot != null)
            {
                int axis = kv.Value % 1000;
                if (axis < E.LastSnapshot.Axes.Length)
                {
                    double d = E.LastSnapshot.Axes[axis] - E.Rest[axis];
                    trig[kv.Key] = kv.Value < 3000 ? d : -d;
                }
            }
        }
        _view.SetPressed(parts, trig);
        _view.SetSticks(E.Last.X, E.Last.Y, 0, E.Last.Z);

        foreach (var n in parts.Except(_prev)) AddEvent($"▼ {ControllerView.Describe(n)} ({VirtualButtons.Describe(E.Settings.Layout[n])}) pressionado");
        foreach (var n in _prev.Except(parts)) AddEvent($"▲ {ControllerView.Describe(n)} liberado");
        _prev = parts;

        var known = new HashSet<int>(E.Settings.Layout.Values);
        var unknown = pressedIds.Where(i => !known.Contains(i)).ToList();
        string status = E.Pad.Connected ? $"Controle: {E.Pad.Name}" : "Controle não detectado";
        if (!_detecting)
            _live.Text = status + (unknown.Count > 0 ? "\nSem peça no desenho: " + string.Join(", ", unknown.Select(VirtualButtons.Describe)) : "");
    }

    private void AddEvent(string s)
    {
        _log.Add($"{DateTime.Now:HH:mm:ss} {s}");
        while (_log.Count > 6) _log.RemoveAt(0);
        _events.Text = string.Join("\n", _log);
    }
}
