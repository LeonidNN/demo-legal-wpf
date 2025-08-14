using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using DemoLegal.Infrastructure.Import;

namespace DemoLegal.Wpf;

public partial class MainWindow : Window
{
    private string? _lastReportText;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл отчёта (CSV или Excel)",
            Filter = "CSV или Excel (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                SetBusy(true, "Импорт выполняется... Это может занять некоторое время.");
                BtnSaveReport.IsEnabled = false;
                _lastReportText = null;

                int? rowLimit = null;
                var limitText = RowLimitBox.Text?.Trim();
                if (!string.IsNullOrEmpty(limitText) && int.TryParse(limitText, out var parsed) && parsed > 0)
                    rowLimit = parsed;

                ImportSummary result;
                var ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();

                if (ext == ".xlsx")
                {
                    result = await Task.Run(async () =>
                    {
                        var svc = new XlsxImportService();
                        return await svc.ImportXlsxAsync(dlg.FileName, rowLimit);
                    });
                }
                else
                {
                    result = await Task.Run(async () =>
                    {
                        var svc = new CsvImportService();
                        return await svc.ImportCsvAsync(dlg.FileName, rowLimit);
                    });
                }

                _lastReportText = result.BuildReportString();
                BtnSaveReport.IsEnabled = true;
                StatusText.Text = $"Готово: прочитано {result.RowsRead}, импортировано {result.RowsImported}, несхождений {result.BalanceMismatches}. Нажмите «Скачать отчёт».";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка импорта: " + ex.Message;
                _lastReportText = "ОШИБКА ИМПОРТА:\r\n" + ex;
                BtnSaveReport.IsEnabled = true; // позволим сохранить лог ошибки
            }
            finally
            {
                SetBusy(false);
            }
        }
    }

    private void SaveReport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastReportText))
        {
            StatusText.Text = "Нечего сохранять: отчёт ещё не сформирован.";
            return;
        }

        var sfd = new SaveFileDialog
        {
            Title = "Сохранить отчёт импорта",
            FileName = $"import-{DateTime.Now:yyyyMMdd_HHmmss}.txt",
            Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
            OverwritePrompt = true
        };
        if (sfd.ShowDialog(this) == true)
        {
            System.IO.File.WriteAllText(sfd.FileName, _lastReportText!, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            StatusText.Text = $"Отчёт сохранён: {sfd.FileName}";
        }
    }

    private void OpenCandidates_Click(object sender, RoutedEventArgs e)
    {
        var wnd = new CandidatesWindow { Owner = this };
        wnd.ShowDialog();
    }

    private void SetBusy(bool isBusy, string? message = null)
    {
        BusyBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        BtnImport.IsEnabled = !isBusy;
        BtnCandidates.IsEnabled = !isBusy;
        if (message != null) StatusText.Text = message;
    }
}
