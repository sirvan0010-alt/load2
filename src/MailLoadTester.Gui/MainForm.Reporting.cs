using System.Text;

namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private Button btnExportReport = null!;
    private RunReport? _lastRunReport;

    private async Task OnExportReportAsync()
    {
        var report = _lastRunReport;
        if (report is null)
        {
            MessageBox.Show(
                "Nejdříve dokončete testovací běh.",
                "Export reportu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export diagnostického reportu",
            Filter = "JSON report (*.json)|*.json|Všechny soubory (*.*)|*.*",
            DefaultExt = "json",
            AddExtension = true,
            FileName = $"MailLoadTester_{report.RunId[..Math.Min(8, report.RunId.Length)]}.json"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var json = RunReportBuilder.ToJson(report);
            await File.WriteAllTextAsync(dialog.FileName, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AppendLog($"Diagnostický report uložen: {dialog.FileName}", Color.LightGreen);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Report se nepodařilo uložit:\n{ex.Message}",
                "Export reportu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
