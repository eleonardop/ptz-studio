using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PtzStudio.App.Engine;
using PtzStudio.App.Ui;

namespace PtzStudio.App.Tabs;

internal sealed class BackupTab : TabBase
{
    private readonly TextBlock _msg = Ux.Text("", 14, true, Ux.Ok);
    public BackupTab(AppEngine e) : base(e) { Init(); }

    protected override void Build()
    {
        Content = Ux.Scroll(Ux.V(
            Ux.Text("Backup e restauração", 17, true),
            Ux.Text($"As configurações são salvas automaticamente em:\n{E.Store.Directory}\nUm backup automático é criado cada vez que o programa abre (os 10 mais recentes são mantidos).", 14),
            Ux.Wrap(
                Ux.Btn("Criar backup agora…", () =>
                {
                    var d = new SaveFileDialog { Filter = "Backup PTZ Studio (*.zip)|*.zip", FileName = $"ptz-studio-backup-{DateTime.Now:yyyyMMdd-HHmm}.zip" };
                    if (d.ShowDialog() != true) return;
                    try { E.Save(); E.Store.CreateBackup(d.FileName); _msg.Text = "Backup criado."; } catch (Exception ex) { _msg.Text = "Erro: " + ex.Message; }
                }, 220),
                Ux.Btn("Restaurar backup…", () =>
                {
                    var d = new OpenFileDialog { Filter = "Backup PTZ Studio (*.zip)|*.zip" };
                    if (d.ShowDialog() != true) return;
                    if (MessageBox.Show("Substituir TODAS as configurações atuais pelas do backup?", "Restaurar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                    try { E.ReplaceSettings(E.Store.Restore(d.FileName)); _msg.Text = "Backup restaurado."; } catch (Exception ex) { _msg.Text = "Erro: " + ex.Message; }
                }, 220)),
            Ux.Wrap(
                Ux.Btn("Exportar tudo (arquivo .json)…", () =>
                {
                    var d = new SaveFileDialog { Filter = "Configurações (*.json)|*.json", FileName = "ptz-studio-config.json" };
                    if (d.ShowDialog() != true) return;
                    try { E.Store.ExportAll(E.Settings, d.FileName); _msg.Text = "Configurações exportadas."; } catch (Exception ex) { _msg.Text = "Erro: " + ex.Message; }
                }, 260),
                Ux.Btn("Importar tudo (arquivo .json)…", () =>
                {
                    var d = new OpenFileDialog { Filter = "Configurações (*.json)|*.json" };
                    if (d.ShowDialog() != true) return;
                    if (MessageBox.Show("Substituir TODAS as configurações atuais pelas do arquivo?", "Importar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                    try { var s = E.Store.ImportAll(d.FileName); E.ReplaceSettings(s); E.Save(); _msg.Text = "Configurações importadas."; } catch (Exception ex) { _msg.Text = "Erro: " + ex.Message; }
                }, 260)),
            Ux.Btn("Abrir pasta de configurações", () => System.Diagnostics.Process.Start("explorer.exe", E.Store.Directory), 260),
            _msg));
    }
}
