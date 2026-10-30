using System.Windows;
using System.Windows.Controls;
using PtzStudio.App.Engine;
using PtzStudio.App.Ui;
using PtzStudio.Core.Models;
using PtzStudio.Core.Visca;

namespace PtzStudio.App.Tabs;

internal sealed class CameraTab : TabBase
{
    private CameraConfig _cur = null!;
    private readonly TextBlock _status = Ux.Text("", 15, true);
    private ListBox _list = new();

    public CameraTab(AppEngine e) : base(e) { Init(); }

    private static int ParseInt(string s, int def, int min, int max) =>
        int.TryParse(s, out int v) ? Math.Clamp(v, min, max) : def;

    private void FillList()
    {
        _list.Items.Clear();
        foreach (var c in E.Settings.Cameras) _list.Items.Add(c.Name + (c.Id == E.Settings.ActiveCameraId ? "  (ativa)" : ""));
    }

    protected override void Build()
    {
        _cur = E.Camera;
        _list = new ListBox { FontSize = 15, MinHeight = 200, Margin = new Thickness(0, 0, 12, 0) };
        FillList();
        _list.SelectedIndex = E.Settings.Cameras.IndexOf(_cur);
        _list.SelectionChanged += (_, _) =>
        {
            if (_filling || _list.SelectedIndex < 0) return;
            _cur = E.Settings.Cameras[_list.SelectedIndex];
            BuildFields();
        };

        var left = Ux.V(Ux.Text("Câmeras cadastradas", 16, true), _list,
            Ux.Btn("Adicionar câmera", () =>
            {
                var c = new CameraConfig { Name = $"Câmera {E.Settings.Cameras.Count + 1}" };
                E.Settings.Cameras.Add(c); E.Save(); Rebuild(); _list.SelectedIndex = E.Settings.Cameras.Count - 1;
            }, 200),
            Ux.Btn("Remover câmera", () =>
            {
                if (E.Settings.Cameras.Count <= 1) { MessageBox.Show("É preciso manter pelo menos uma câmera.", "Câmeras"); return; }
                if (MessageBox.Show($"Remover “{_cur.Name}” e seus presets locais?", "Câmeras", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                E.Settings.Presets.RemoveAll(p => p.CameraId == _cur.Id);
                E.Settings.Cameras.Remove(_cur);
                if (E.Settings.ActiveCameraId == _cur.Id) { E.Settings.ActiveCameraId = E.Settings.Cameras[0].Id; E.ConnectCamera(); }
                E.Save(); Rebuild();
            }, 200));

        _right.Content = null;
        var g = new Grid { Margin = new Thickness(16) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0); g.Children.Add(left);
        Grid.SetColumn(_right, 1); g.Children.Add(_right);
        Content = g;
        BuildFields();
    }

    private readonly ScrollViewer _right = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

    private void BuildFields()
    {
        var c = _cur;
        var panel = Ux.V(
            Ux.Text("Configuração da câmera", 17, true),
            Ux.Labeled("Nome", Ux.Input(c.Name, v => { c.Name = v; E.Save(); FillListKeep(); })),
            Ux.Labeled("Endereço IP", Ux.Input(c.IpAddress, v => { c.IpAddress = v.Trim(); E.Save(); })),
            Ux.Labeled("Porta UDP", Ux.Input(c.Port.ToString(), v => { c.Port = ParseInt(v, c.Port, 1, 65535); E.Save(); }, 100)),
            Ux.Labeled("Protocolo", Ux.Combo(new[] { "VISCA sobre UDP puro", "VISCA over IP (cabeçalho de 8 bytes)" }, (int)c.Transport, i => { c.Transport = (ViscaTransportMode)i; E.Save(); }, 320)),
            Ux.Labeled("Endereço VISCA (1 a 7)", Ux.Input(c.ViscaAddress.ToString(), v => { c.ViscaAddress = ParseInt(v, c.ViscaAddress, 1, 7); E.Save(); }, 80)),
            Ux.Labeled("Velocidade Pan máx. (1–24)", Ux.Input(c.MaxPanSpeed.ToString(), v => { c.MaxPanSpeed = ParseInt(v, c.MaxPanSpeed, 1, 24); E.Save(); }, 80)),
            Ux.Labeled("Velocidade Tilt máx. (1–20)", Ux.Input(c.MaxTiltSpeed.ToString(), v => { c.MaxTiltSpeed = ParseInt(v, c.MaxTiltSpeed, 1, 20); E.Save(); }, 80)),
            Ux.Labeled("Velocidade Zoom máx. (1–8)", Ux.Input(c.MaxZoomSpeed.ToString(), v => { c.MaxZoomSpeed = ParseInt(v, c.MaxZoomSpeed, 1, 8); E.Save(); }, 80)),
            Ux.Labeled("Timeout (ms)", Ux.Input(c.TimeoutMs.ToString(), v => { c.TimeoutMs = ParseInt(v, c.TimeoutMs, 100, 10000); E.Save(); }, 100)),
            Ux.Labeled("Intervalo mínimo entre comandos (ms)", Ux.Input(c.CommandIntervalMs.ToString(), v => { c.CommandIntervalMs = ParseInt(v, c.CommandIntervalMs, 0, 500); E.Save(); }, 100)),
            Ux.Labeled("Tentativas antes de “desconectado”", Ux.Input(c.ReconnectAttempts.ToString(), v => { c.ReconnectAttempts = ParseInt(v, c.ReconnectAttempts, 1, 50); E.Save(); }, 80)),
            Ux.Check("Verificar periodicamente se a câmera responde (consulta de versão)", c.HealthCheck, v => { c.HealthCheck = v; E.Save(); }),
            Ux.Btn("USAR ESTA CÂMERA, CONECTAR E TESTAR", TestAsync, 380, Ux.Accent),
            _status,
            Ux.Text("Atenção: a porta e o formato (UDP puro ou com cabeçalho) variam entre fabricantes. Confirme no manual da LJ510-30X-N; se um modo não obtiver resposta, tente o outro. Algumas câmeras aceitam comandos mesmo sem responder à consulta — nesse caso desmarque a verificação periódica.", 13, false, Ux.Muted));
        _right.Content = panel;
    }

    private bool _filling;
    private void FillListKeep() { int i = _list.SelectedIndex; _filling = true; try { FillList(); _list.SelectedIndex = i; } finally { _filling = false; } }

    private async void TestAsync()
    {
        E.Settings.ActiveCameraId = _cur.Id; E.Save();
        _status.Text = "Conectando…"; _status.Foreground = Ux.Warn;
        E.ConnectCamera();
        bool ok = await E.Client.TestConnectionAsync();
        FillListKeep();
        if (ok) { _status.Text = $"Conectado. Tempo de resposta: {E.Client.LastRoundTripMs:0} ms"; _status.Foreground = Ux.Ok; }
        else
        {
            _status.Text = "Sem resposta da câmera. Verifique IP, porta, protocolo (UDP puro × VISCA over IP) e se o computador está na mesma rede.";
            _status.Foreground = Ux.Danger;
        }
    }

    protected override void Refresh() { }
}
