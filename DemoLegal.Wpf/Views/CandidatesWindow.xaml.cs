using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using DemoLegal.Infrastructure.Docs;
using DemoLegal.Infrastructure.Repositories;

namespace DemoLegal.Wpf;

public partial class CandidatesWindow : Window
{
    private readonly ICaseQueryRepository _repo = new CaseQueryRepository();
    private readonly IAccountRepository _accountRepo = new AccountRepository();
    private string? _lastExportFolder;
    private string? _idKey; // найденное имя столбца/свойства с ID

    public CandidatesWindow()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var data = await _repo.GetCandidatesAsync(500);
        GridCases.ItemsSource = data;
        StatusText.Text = $"Загружено записей: {data?.Count ?? 0}";

        _idKey = null;
        var first = data?.Cast<object?>().FirstOrDefault();
        if (first is not null)
            _idKey = DetectIdKey(first);

        UpdateExportButtonState();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private void GridCases_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateExportButtonState();
    }

    private void UpdateExportButtonState()
    {
        var count = GridCases.SelectedItems?.Count ?? 0;
        ExportBtn.IsEnabled = count > 0;
        if (count > 0) StatusText.Text = $"Выделено: {count}";
    }

    // --------- ключевая логика определения ID ----------
    private string? DetectIdKey(object row)
    {
        var candidates = new[] { "AccountId", "account_id", "accountId", "Id", "id", "ACCOUNT_ID" };

        if (row is IDictionary<string, object> d)
        {
            foreach (var k in candidates)
            {
                var exact = d.Keys.FirstOrDefault(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;
            }
            var guess = d.Keys.FirstOrDefault(x => x.IndexOf("account", StringComparison.OrdinalIgnoreCase) >= 0
                                                   && x.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0);
            if (guess != null) return guess;
        }
        else if (row is DataRowView drv)
        {
            var cols = drv.Row?.Table?.Columns;
            if (cols != null)
            {
                foreach (var k in candidates)
                {
                    var col = cols.Cast<DataColumn>().FirstOrDefault(c => string.Equals(c.ColumnName, k, StringComparison.OrdinalIgnoreCase));
                    if (col != null) return col.ColumnName;
                }
                var guess = cols.Cast<DataColumn>()
                    .FirstOrDefault(c => c.ColumnName.IndexOf("account", StringComparison.OrdinalIgnoreCase) >= 0
                                         && c.ColumnName.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0);
                if (guess != null) return guess.ColumnName;
            }
        }
        else
        {
            var t = row.GetType();
            foreach (var k in candidates)
            {
                var p = t.GetProperty(k, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (p != null) return p.Name;
            }
            var guess = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => p.Name.IndexOf("account", StringComparison.OrdinalIgnoreCase) >= 0
                                     && p.Name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0);
            if (guess != null) return guess.Name;
        }
        return null;
    }

    private object? GetValue(object row, string key)
    {
        if (row is IDictionary<string, object> d)
        {
            if (d.TryGetValue(key, out var v)) return v;
            var alt = d.Keys.FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
            if (alt != null && d.TryGetValue(alt, out v)) return v;
            return null;
        }
        if (row is DataRowView drv)
        {
            var table = drv.Row?.Table;
            if (table != null)
            {
                if (table.Columns.Contains(key)) return drv[key];
                var col = table.Columns.Cast<DataColumn>()
                    .FirstOrDefault(c => string.Equals(c.ColumnName, key, StringComparison.OrdinalIgnoreCase));
                if (col != null) return drv[col.ColumnName];
            }
            return null;
        }
        var p = row.GetType().GetProperty(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return p?.GetValue(row);
    }

    private string DebugDescribeRow(object row)
    {
        try
        {
            if (row is IDictionary<string, object> d)
                return "IDictionary keys: " + string.Join(", ", d.Keys);

            if (row is DataRowView drv && drv.Row?.Table != null)
                return "DataRowView columns: " + string.Join(", ", drv.Row.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));

            return "POCO props: " + string.Join(", ", row.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public).Select(p => p.Name));
        }
        catch { return "(debug failed)"; }
    }

    private async Task<int?> ResolveAccountIdByLsAsync(object row)
    {
        // фолбэк: попробуем найти счёт по ЛС/LS
        foreach (var key in new[] { "ЛС", "LS", "Ls", "ls" })
        {
            var v = GetValue(row, key);
            if (v is string s && !string.IsNullOrWhiteSpace(s))
            {
                var id = await _accountRepo.TryGetIdByLsAsync(s.Trim());
                if (id.HasValue) return id.Value;
            }
        }
        return null;
    }

    private async Task<List<int>> GetSelectedAccountIdsAsync()
    {
        var ids = new List<int>();

        if (GridCases.SelectedItems is IEnumerable sel)
        {
            foreach (var row in sel)
            {
                if (row is null) continue;

                object? val = null;

                if (!string.IsNullOrWhiteSpace(_idKey))
                    val = GetValue(row, _idKey!);

                if (val is null)
                {
                    foreach (var k in new[] { "AccountId", "account_id", "accountId", "Id", "id", "ACCOUNT_ID" })
                    {
                        val = GetValue(row, k);
                        if (val is not null) break;
                    }
                }

                if (val is int i) { ids.Add(i); continue; }
                if (val is long l) { ids.Add(checked((int)l)); continue; }
                if (val is string s && int.TryParse(s, out var si)) { ids.Add(si); continue; }

                // Фолбэк: по ЛС
                var resolved = await ResolveAccountIdByLsAsync(row);
                if (resolved.HasValue) { ids.Add(resolved.Value); continue; }
            }
        }

        return ids.Distinct().ToList();
    }
    // ---------------------------------------------------

    private async void ExportDocs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedCount = GridCases.SelectedItems?.Count ?? 0;
            var ids = await GetSelectedAccountIdsAsync();

            if (selectedCount == 0)
            {
                System.Windows.MessageBox.Show(
                    "Не выбраны строки. Выделите одну или несколько записей (Ctrl/Shift + клик).",
                    "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ids.Count == 0)
            {
                // Диагностика — покажем, какие поля видим в первой выделенной строке
                var first = GridCases.SelectedItems[0];
                var fields = DebugDescribeRow(first);
                var hint = string.IsNullOrWhiteSpace(_idKey) ? "Ключ AccountId не обнаружен." : $"Ожидаемый ключ: '{_idKey}'.";
                System.Windows.MessageBox.Show(
                    $"Не удалось определить идентификатор для выбранных строк.\n{hint}\n\nДиагностика первой строки:\n{fields}",
                    "Экспорт", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var dlg = new FolderBrowserDialog
            {
                Description = "Выберите папку для сохранения документов",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
                return;

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var exportFolder = System.IO.Path.Combine(dlg.SelectedPath, $"DemoLegal_{stamp}");
            System.IO.Directory.CreateDirectory(exportFolder);

            StatusText.Text = $"Формирование документов для {ids.Count} записей...";
            var svc = new WordExportService();
            var created = await svc.ExportAsync(ids, exportFolder);

            var zipPath = exportFolder + ".zip";
            if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);
            System.IO.Compression.ZipFile.CreateFromDirectory(exportFolder, zipPath);

            _lastExportFolder = exportFolder;
            StatusText.Text = $"Создано документов: {created}. Папка: {exportFolder}. ZIP: {zipPath}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Ошибка при формировании документов: " + ex.Message, "Экспорт",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folder = _lastExportFolder;
            if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
            {
                System.Windows.MessageBox.Show("Папка экспорта ещё не создана. Сначала сформируйте документы.",
                    "Открыть папку", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Не удалось открыть папку: " + ex.Message, "Открыть папку",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
