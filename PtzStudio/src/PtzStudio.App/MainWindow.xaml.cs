using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PtzStudio.App.Engine;
using PtzStudio.App.Tabs;
using PtzStudio.App.Ui;
using PtzStudio.Core.Models;
using PtzStudio.Core.Visca;

namespace PtzStudio.App;

/// <summary>Janela principal: cabeçalho fixo (status, perfil, entrada, EMERGÊNCIA) e as 10 abas.</summary>
public partial class MainWindow : Window
{
    private readonly AppEngine _engine = new();
    private readonly List<TabBase> _tabs = new();
    private readonly TextBlock _cam = Ux.Text("", 15, true);
    private readonly TextBlock _pad = Ux.Text("", 15, true);
    private readonly TextBlock _profile = Ux.Text("", 15, true);
    private readonly TextBlock _source = Ux.Text("", 15, true);
    private readonly TextBlock _notice = Ux.Text("", 14, false, Ux.Muted);
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string _lastNotice = "";

    public MainWindow()
    {
        InitializeComponent();
        Title = "PTZ Studio";

        var tabs = new TabControl { Margin = new Thickness(8, 0, 8, 8), FontSize = 14 };
        void Add(string header, TabBase tab)
        {
            _tabs.Add(tab);
            tabs.Items.Add(new TabItem { Header = header, Content = tab, Padding = new Thickness(10, 6, 10, 6) });
        }
        Add("Controle PTZ", new PtzTab(_engine));
        Add("Mapeamento do controle", new MappingTab(_engine));
        Add("Analógicos", new AnalogTab(_engine));
        Add("Sensibilidade e movimentos", new MotionTab(_engine));
        Add("Zoom", new ZoomTab(_engine));
        Add("Câmera", new CameraTab(_engine));
        Add("Presets", new PresetsTab(_engine));
        Add("Perfis", new ProfilesTab(_engine));
        Add("Diagnóstico", new DiagnosticsTab(_engine));
        Add("Backup e restauração", new BackupTab(_engine));

        tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, tabs)) return; // ignora eventos vindos de ComboBox/ListBox dentro das abas
            if (tabs.SelectedItem is TabItem { Content: TabBase tb } && (tb is PtzTab || tb is PresetsTab))
            {
                try { tb.Rebuild(); } catch { }
            }
        };

        var emergency = new Button
        {
            Content = "PARADA DE EMERGÊNCIA",
            MinHeight = 48, MinWidth = 240, FontSize = 16, FontWeight = FontWeights.Bold,
            Background = Ux.Danger, Foreground = Brushes.White, Margin = new Thickness(8, 4, 8, 4)
        };
        emergency.Click += (_, _) => _engine.Controller.EmergencyStop("botão de emergência (tela)");

        var header = new DockPanel { Margin = new Thickness(8, 8, 8, 6) };
        DockPanel.SetDock(emergency, Dock.Right);
        header.Children.Add(emergency);
        var info = Ux.Wrap(
            Pair("Câmera: ", _cam), Pair("Controle: ", _pad), Pair("Perfil: ", _profile), Pair("Entrada: ", _source));
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(info);
        left.Children.Add(_notice);
        header.Children.Add(left);

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(tabs);
        Content = root;

        _engine.Notice += m => { _lastNotice = m; };
        _engine.ProfileChanged += RebuildTabs;
        _engine.SettingsReplaced += RebuildTabs;

        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();

        SourceInitialized += (_, _) => _engine.Start(new WindowInteropHelper(this).Handle);
        Closing += OnClosing;
        UpdateStatus();
    }

    private static UIElement Pair(string label, TextBlock value)
    {
        var l = Ux.Text(label, 15, false, Ux.Muted);
        var p = Ux.H(l, value);
        p.Margin = new Thickness(0, 0, 22, 0);
        return p;
    }

    private void RebuildTabs()
    {
        foreach (var t in _tabs)
        {
            try { t.Rebuild(); } catch { }
        }
    }

    private void UpdateStatus()
    {
        var st = _engine.Client.State;
        (_cam.Text, _cam.Foreground) = st switch
        {
            ConnectionState.Conectado => ("Conectada", Ux.Ok),
            ConnectionState.Conectando => ("Conectando...", Ux.Warn),
            ConnectionState.ErroDeComunicacao => ("Erro de comunicação", Ux.Danger),
            _ => ("Desconectada", Ux.Danger)
        };
        _cam.Text += $" ({_engine.Camera.Name})";

        if (_engine.Pad.Connected) { _pad.Text = _engine.Pad.Name ?? "Conectado"; _pad.Foreground = Ux.Ok; }
        else { _pad.Text = "Não detectado"; _pad.Foreground = Ux.Warn; }

        _profile.Text = _engine.Settings.ActiveProfile;
        _source.Text = _engine.Controller.ActiveSource == InputSource.Fisico ? "Joystick físico" : "Joystick virtual";
        _source.Foreground = Ux.Accent;
        _notice.Text = _lastNotice;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _statusTimer.Stop();
        try { _engine.Dispose(); } catch { }
    }
}
