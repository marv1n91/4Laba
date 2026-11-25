using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SortingLab4.App.Helpers;
using SortingLab4.Core.Algorithms;
using SortingLab4.Core.Models;
using SortingLab4.Core.Services;

namespace SortingLab4.App.Views;

public partial class ExternalSortView : UserControl
{
    private sealed class LogEntryVm
    {
        public string StepHeader { get; init; } = string.Empty;
        public string TypeLabel { get; init; } = string.Empty;
        public string Explanation { get; init; } = string.Empty;
    }

    private sealed class ColumnChoice
    {
        public int? ColumnIndex { get; init; }
        public string Display { get; init; } = string.Empty;
        public override string ToString() => Display;
    }

    private TableDocument? _document;
    private ExternalSortResult? _result;
    private int _stepIndex = -1;
    private bool _isPlaying;
    private bool _isPaused;
    private CancellationTokenSource? _preparation;
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<LogEntryVm> _logs = new();

    public ExternalSortView()
    {
        InitializeComponent();
        LstLog.ItemsSource = _logs;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
        _timer.Tick += Timer_Tick;
        foreach (var combo in new[] { CmbMethod, CmbWays, CmbKeyCol, CmbKeyType, CmbFilterCol, CmbFilterVal })
            combo.SelectionChanged += SortSettings_SelectionChanged;
        UpdateButtons();
        Loaded += (_, _) => LoadPreset("countries.csv", defaultKeyCol: 4, defaultFilterCol: 1, defaultFilterVal: "Европа");
    }

    private static string ResolveDataPath(string fileName)
    {
        string local = Path.Combine(AppContext.BaseDirectory, "data", fileName);
        if (File.Exists(local)) return local;
        string fallback = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName);
        return fallback;
    }

    private void LoadCountries_Click(object sender, RoutedEventArgs e) =>
        LoadPreset("countries.csv", defaultKeyCol: 4, defaultFilterCol: 1, defaultFilterVal: "Европа");

    private void LoadChemicals_Click(object sender, RoutedEventArgs e) =>
        LoadPreset("chemicals.csv", defaultKeyCol: 2, defaultFilterCol: 0, defaultFilterVal: "Кислоты");

    private void LoadRussianWords_Click(object sender, RoutedEventArgs e) =>
        LoadPreset("russian_words.txt", defaultKeyCol: 0, defaultFilterCol: null, defaultFilterVal: null);

    private void LoadPreset(string fileName, int defaultKeyCol, int? defaultFilterCol, string? defaultFilterVal)
    {
        try
        {
            ShowError(null);
            string path = ResolveDataPath(fileName);
            var doc = TableFileService.LoadFromFile(path);
            ApplyDocument(doc, defaultKeyCol, defaultFilterCol, defaultFilterVal);
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить пример «{fileName}»: {ex.Message}");
        }
    }

    private void UploadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите входной файл таблицы или слов",
            Filter = "Таблицы и текст (*.csv;*.tsv;*.txt)|*.csv;*.tsv;*.txt|Все файлы (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            ShowError(null);
            var doc = TableFileService.LoadFromFile(dlg.FileName);
            ApplyDocument(doc, 0, null, null);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ApplyDocument(TableDocument doc, int defaultKeyCol, int? defaultFilterCol, string? defaultFilterVal)
    {
        _document = doc;
        ResetPlayback();

        TxtFileMeta.Text = $"{doc.FileName} ({doc.Rows.Count} записей, {doc.Headers.Count} столбц.)";
        TxtSrcCount.Text = $"{doc.Rows.Count} записей";
        GridSource.ItemsSource = BuildDataTable(doc.Headers, doc.Rows).DefaultView;

        CmbKeyCol.Items.Clear();
        for (int i = 0; i < doc.Headers.Count; i++)
        {
            string tLabel = doc.ColumnTypes[i] == KeyComparisonType.Number ? "число" : "текст";
            CmbKeyCol.Items.Add(new ColumnChoice { ColumnIndex = i, Display = $"{doc.Headers[i]} ({tLabel})" });
        }
        CmbKeyCol.SelectedIndex = Math.Clamp(defaultKeyCol, 0, Math.Max(0, doc.Headers.Count - 1));

        CmbFilterCol.Items.Clear();
        CmbFilterCol.Items.Add(new ColumnChoice { ColumnIndex = null, Display = "Без фильтра (все записи)" });
        int selectFilterIdx = 0;
        int curItem = 1;
        foreach (var kv in doc.FilterOptions)
        {
            CmbFilterCol.Items.Add(new ColumnChoice { ColumnIndex = kv.Key, Display = doc.Headers[kv.Key] });
            if (defaultFilterCol.HasValue && kv.Key == defaultFilterCol.Value) selectFilterIdx = curItem;
            curItem++;
        }
        CmbFilterCol.SelectedIndex = selectFilterIdx;
        PopulateFilterValues(defaultFilterVal);
        UpdateButtons();
    }

    private void CmbFilterCol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PopulateFilterValues(null);
    }

    private void PopulateFilterValues(string? defaultVal)
    {
        CmbFilterVal.Items.Clear();
        if (_document is null || CmbFilterCol.SelectedItem is not ColumnChoice choice || !choice.ColumnIndex.HasValue)
        {
            CmbFilterVal.IsEnabled = false;
            return;
        }
        if (!_document.FilterOptions.TryGetValue(choice.ColumnIndex.Value, out var values) || values.Count == 0)
        {
            CmbFilterVal.IsEnabled = false;
            return;
        }
        CmbFilterVal.IsEnabled = true;
        int selected = 0;
        for (int i = 0; i < values.Count; i++)
        {
            CmbFilterVal.Items.Add(values[i]);
            if (defaultVal is not null && string.Equals(values[i], defaultVal, StringComparison.OrdinalIgnoreCase))
                selected = i;
        }
        CmbFilterVal.SelectedIndex = selected;
    }

    private void CmbMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WaysPanel is null) return;
        bool isMulti = (CmbMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Multiway";
        WaysPanel.Visibility = isMulti ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SldSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int ms = (int)SldSpeed.Value;
        if (TxtSpeedVal is not null) TxtSpeedVal.Text = $"{ms} мс";
        if (_timer is not null) _timer.Interval = TimeSpan.FromMilliseconds(ms);
    }

    private void ShowError(string? msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
        {
            ErrBanner.Visibility = Visibility.Collapsed;
            TxtError.Text = string.Empty;
        }
        else
        {
            ErrBanner.Visibility = Visibility.Visible;
            TxtError.Text = msg;
        }
    }

    private void SortSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_result is not null || _preparation is { IsCancellationRequested: false })
            ResetPlayback();
    }

    private void UpdateButtons()
    {
        bool hasSteps = _result is not null && _result.Steps.Count > 0;
        bool finished = hasSteps && _stepIndex >= _result!.Steps.Count - 1;
        bool canStartOrStep = _document is not null && _preparation is null && !(_isPlaying && !_isPaused);
        BtnStart.IsEnabled = canStartOrStep;
        BtnPause.IsEnabled = _isPlaying && !_isPaused && !finished;
        BtnResume.IsEnabled = _isPaused && !finished;
        BtnStep.IsEnabled = canStartOrStep && !finished;
    }

    private void ResetPlayback()
    {
        _preparation?.Cancel();
        _timer.Stop();
        _isPlaying = false;
        _isPaused = false;
        _result = null;
        _stepIndex = -1;
        _logs.Clear();

        TxtStepBadge.Text = "Ожидание";
        TxtStepCounter.Text = "Шаг 0 / 0";
        TxtExplanation.Text = "Выберите метод и ключевой столбец. Нажмите «Запустить» для показа или «Следующий шаг» для пошагового разбора.";
        TxtStats.Text = "Проходов: 0  |  Сравнений: 0  |  Перемещений: 0";
        FilesVizPanel.Children.Clear();
        FilesVizPanel.Children.Add(new TextBlock
        {
            Text = "Здесь отобразятся основной и вспомогательные файлы во время сортировки.",
            Foreground = Brushes.SlateGray, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 12)
        });
        GridResult.ItemsSource = null;
        TxtResCount.Text = "0 записей";
        SavedBanner.Visibility = Visibility.Collapsed;
        BtnOpenFile.Visibility = Visibility.Collapsed;
        BtnSaveAs.Visibility = Visibility.Collapsed;
        UpdateButtons();
    }

    private async Task<bool> EnsureResultAsync()
    {
        if (_preparation is not null || _document is not { } doc) return false;
        if (_result is not null) return _result.Steps.Count > 0;

        ShowError(null);
        using var preparation = new CancellationTokenSource();
        _preparation = preparation;
        UpdateButtons();

        var methodTag = (CmbMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Direct";
        var method = methodTag switch
        {
            "Natural" => ExternalSortMethod.NaturalMerge,
            "Multiway" => ExternalSortMethod.MultiwayMerge,
            _ => ExternalSortMethod.DirectMerge
        };

        int ways = int.TryParse((CmbWays.SelectedItem as ComboBoxItem)?.Content?.ToString(), out int w) ? w : 3;
        int keyCol = (CmbKeyCol.SelectedItem as ColumnChoice)?.ColumnIndex ?? 0;

        var keyTypeTag = (CmbKeyType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";
        var keyType = keyTypeTag switch
        {
            "Number" => KeyComparisonType.Number,
            "Text" => KeyComparisonType.Text,
            _ => KeyComparisonType.Auto
        };

        int? filterCol = (CmbFilterCol.SelectedItem as ColumnChoice)?.ColumnIndex;
        string? filterVal = filterCol.HasValue ? CmbFilterVal.SelectedItem?.ToString() : null;

        try
        {
            var result = await Task.Run(() =>
            {
                var workingRows = TableFileService.FilterRows(doc.Rows, filterCol, filterVal);
                if (workingRows.Count == 0)
                    throw new InvalidOperationException("После применения фильтра не осталось ни одной записи для сортировки.");

                var sortRes = ExternalSortingAlgorithms.Sort(method, doc.Headers, workingRows, keyCol, keyType, ways);
                preparation.Token.ThrowIfCancellationRequested();
                string outDir = Path.Combine(AppContext.BaseDirectory, "output");
                sortRes.SavedFilePath = TableFileService.SaveSortedTable(outDir, doc.Headers, sortRes.SortedRows, method, doc.IsSingleColumn);
                return sortRes;
            }, preparation.Token);

            if (preparation.IsCancellationRequested) return false;
            _result = result;

            var st = result.Stats;
            TxtStats.Text = $"Проходов: {st.Passes}  |  Сравнений: {st.Comparisons}  |  Перемещений: {st.Moves}";
            TxtResCount.Text = $"{result.SortedRows.Count} записей";
            GridResult.ItemsSource = BuildDataTable(result.Headers, result.SortedRows).DefaultView;

            SavedBanner.Visibility = Visibility.Visible;
            TxtSavedInfo.Text = $"Результат сохранён в файл: {result.SavedFilePath} ({result.SortedRows.Count} записей).";
            BtnOpenFile.Visibility = Visibility.Visible;
            BtnSaveAs.Visibility = Visibility.Visible;

            return result.Steps.Count > 0;
        }
        catch (Exception ex)
        {
            if (!preparation.IsCancellationRequested) ShowError(ex.Message);
            return false;
        }
        finally
        {
            _preparation = null;
            UpdateButtons();
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!BtnStart.IsEnabled) return;
        if (_result is not null && _stepIndex >= _result.Steps.Count - 1)
            ResetPlayback();
        if (!await EnsureResultAsync()) return;

        if (_stepIndex < 0)
        {
            _stepIndex = 0;
            RenderStep(0);
        }
        _isPaused = false;
        _isPlaying = _stepIndex < _result!.Steps.Count - 1;
        if (_isPlaying) _timer.Start();
        UpdateButtons();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = true;
        _timer.Stop();
        UpdateButtons();
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPaused) return;
        _isPaused = false;
        UpdateButtons();
        _timer.Start();
    }

    private async void NextStep_Click(object sender, RoutedEventArgs e)
    {
        if (!BtnStep.IsEnabled || !await EnsureResultAsync()) return;
        _timer.Stop();
        _isPlaying = false;
        _isPaused = false;
        if (_result is not null && _stepIndex < _result.Steps.Count - 1)
        {
            _stepIndex++;
            RenderStep(_stepIndex);
        }
        UpdateButtons();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetPlayback();

    private void OpenResult_Click(object sender, RoutedEventArgs e)
    {
        if (_result?.SavedFilePath is null || !File.Exists(_result.SavedFilePath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_result.SavedFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось открыть файл: {ex.Message}");
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_result?.SavedFilePath is null || !File.Exists(_result.SavedFilePath)) return;
        string ext = Path.GetExtension(_result.SavedFilePath);
        var dlg = new SaveFileDialog
        {
            FileName = Path.GetFileName(_result.SavedFilePath),
            Filter = ext == ".txt" ? "Текстовый файл (*.txt)|*.txt" : "CSV таблица (*.csv)|*.csv"
        };
        if (dlg.ShowDialog() == true)
        {
            File.Copy(_result.SavedFilePath, dlg.FileName, overwrite: true);
            TxtSavedInfo.Text = $"Результат дополнительно сохранён в: {dlg.FileName}";
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_result is null || !_isPlaying || _isPaused)
        {
            _timer.Stop();
            return;
        }
        if (_stepIndex < _result.Steps.Count - 1)
        {
            _stepIndex++;
            RenderStep(_stepIndex);
        }
        if (_stepIndex >= _result.Steps.Count - 1)
        {
            _timer.Stop();
            _isPlaying = false;
            _isPaused = false;
            UpdateButtons();
        }
    }

    private void RenderStep(int idx)
    {
        if (_result is null || idx < 0 || idx >= _result.Steps.Count) return;
        var st = _result.Steps[idx];

        TxtStepBadge.Text = st.OperationLabel;
        TxtStepCounter.Text = $"Шаг {st.StepNumber} / {_result.Steps.Count}";
        TxtExplanation.Text = st.Explanation;

        FilesVizPanel.Children.Clear();
        var mainCard = MakeFileCard("Основной файл (main.jsonl)", st.MainFileRecords, "#EFF6FF", "#93C5FD");
        FilesVizPanel.Children.Add(mainCard);

        var auxGrid = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var af in st.AuxFiles)
        {
            string bg = af.IsActiveSource ? "#F0FDF4" : "#F8FAFC";
            string br = af.IsActiveSource ? "#16A34A" : "#CBD5E1";
            var card = MakeFileCard(af.Name, af.Records, bg, br);
            card.MinWidth = 230;
            card.Margin = new Thickness(0, 0, 8, 8);
            if (st.OperationType == "split") UiAnimations.FadeIn(card, durationMs: 180);
            auxGrid.Children.Add(card);
        }
        FilesVizPanel.Children.Add(auxGrid);

        while (_logs.Count <= idx)
        {
            var entry = _result.Steps[_logs.Count];
            _logs.Add(new LogEntryVm
            {
                StepHeader = $"Шаг {entry.StepNumber}",
                TypeLabel = entry.OperationLabel,
                Explanation = entry.Explanation
            });
        }
        if (_logs.Count > 0) LstLog.ScrollIntoView(_logs[^1]);
    }

    private static Border MakeFileCard(string title, List<FileRecordPreview> records, string bgHex, string borderHex)
    {
        var stack = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        head.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 12.5 });
        head.Children.Add(new TextBlock
        {
            Text = $"{records.Count} записей",
            FontSize = 11.5,
            Foreground = Brushes.SlateGray,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        stack.Children.Add(head);

        var chips = new WrapPanel();
        if (records.Count == 0)
        {
            chips.Children.Add(new TextBlock { Text = "файл пуст", FontSize = 12, Foreground = Brushes.SlateGray });
        }
        else
        {
            foreach (var r in records)
            {
                chips.Children.Add(new Border
                {
                    Background = Brushes.White,
                    BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#CBD5E1")!,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 5, 4),
                    Child = new TextBlock { Text = r.DisplayText, FontSize = 11.5 }
                });
            }
        }
        stack.Children.Add(chips);

        return new Border
        {
            Background = (SolidColorBrush)new BrushConverter().ConvertFromString(bgHex)!,
            BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(borderHex)!,
            BorderThickness = new Thickness(1.3),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Child = stack
        };
    }

    private static DataTable BuildDataTable(List<string> headers, List<string[]> rows)
    {
        var dt = new DataTable();
        for (int i = 0; i < headers.Count; i++)
        {
            string colName = headers[i];
            if (dt.Columns.Contains(colName)) colName = $"{colName}_{i + 1}";
            dt.Columns.Add(colName);
        }
        int limit = Math.Min(rows.Count, 500);
        for (int r = 0; r < limit; r++)
        {
            var dr = dt.NewRow();
            for (int c = 0; c < headers.Count && c < rows[r].Length; c++)
                dr[c] = rows[r][c];
            dt.Rows.Add(dr);
        }
        return dt;
    }
}
