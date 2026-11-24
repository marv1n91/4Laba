using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using SortingLab4.App.Helpers;
using SortingLab4.Core.Algorithms;
using SortingLab4.Core.Models;

namespace SortingLab4.App.Views;

public partial class LexicographicSortView : UserControl
{
    public LexicographicSortView()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadSampleText();
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

    private void LoadSample_Click(object sender, RoutedEventArgs e) => LoadSampleText();

    private void LoadSampleText()
    {
        try
        {
            ShowError(null);
            string local = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "sample_english.txt");
            if (!File.Exists(local))
                local = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "data", "sample_english.txt");

            if (File.Exists(local))
                TxtInput.Text = File.ReadAllText(local, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить пример текста: {ex.Message}");
        }
    }

    private void UploadTxt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите текстовый файл",
            Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            ShowError(null);
            TxtInput.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка чтения файла: {ex.Message}");
        }
    }

    private void SortText_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        string tag = (CmbLexAlgo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Quick";
        var algo = tag switch
        {
            "Merge" => LexicographicAlgorithm.MergeSort,
            "Radix" => LexicographicAlgorithm.AbcRadixSort,
            _ => LexicographicAlgorithm.QuickSortHoare
        };

        bool lower = ChkLowercase.IsChecked == true;
        var res = LexicographicSortingAlgorithms.ProcessText(TxtInput.Text, algo, lower);

        TxtLexBadge.Text = res.AlgorithmName;
        TxtLexStats.Text = $"Всего слов: {res.TotalWords}  |  Уникальных: {res.UniqueWords}  |  Время: {res.ElapsedMs:F3} мс";
        TxtLexExplanation.Text = res.Explanation;

        TxtSortedWordsCount.Text = $"{res.TotalWords} слов";
        SortedWordsPanel.Children.Clear();
        int previewLimit = Math.Min(res.SortedWords.Length, 450);
        for (int i = 0; i < previewLimit; i++)
        {
            SortedWordsPanel.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#CBD5E1")!,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 5, 5),
                Child = new TextBlock { Text = res.SortedWords[i], FontSize = 12.5 }
            });
        }
        if (res.SortedWords.Length > previewLimit)
        {
            SortedWordsPanel.Children.Add(new TextBlock
            {
                Text = $"... показано {previewLimit} из {res.SortedWords.Length} слов",
                Foreground = Brushes.SlateGray,
                Margin = new Thickness(4)
            });
        }

        TxtFreqCount.Text = $"{res.UniqueWords} уникальных";
        GridFrequencies.ItemsSource = res.Frequencies;
    }

    private async void RunBenchmarks_Click(object sender, RoutedEventArgs e)
    {
        BtnRunBench.IsEnabled = false;
        TxtBenchStatus.Text = "Выполняются реальные замеры на текстах от 100 до 5000 слов...";

        string tag1 = (CmbBenchAlgo1.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Quick";
        var algo1 = tag1 == "Merge" ? LexicographicAlgorithm.MergeSort : LexicographicAlgorithm.QuickSortHoare;
        var algo2 = LexicographicAlgorithm.AbcRadixSort;
        int repeats = int.TryParse((CmbRepeats.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out int r) ? r : 5;

        try
        {
            var report = await Task.Run(() =>
                LexicographicSortingAlgorithms.RunBenchmarks(new[] { 100, 500, 1000, 2000, 5000 }, algo1, algo2, repeats));

            GridBench.ItemsSource = report.Rows;
            DrawChart(report);
            UiAnimations.FadeIn(ChartCanvas, durationMs: 200);
            TxtBenchStatus.Text = $"Эксперименты завершены (среднее по {report.Repeats} прогонам на каждый размер текста).";
        }
        catch (Exception ex)
        {
            TxtBenchStatus.Text = $"Ошибка: {ex.Message}";
        }
        finally
        {
            BtnRunBench.IsEnabled = true;
        }
    }

    private void DrawChart(BenchmarkReport report)
    {
        ChartCanvas.Children.Clear();
        var rows = report.Rows;
        if (rows.Count == 0) return;

        double w = ChartCanvas.Width;
        double h = ChartCanvas.Height;
        double padL = 52, padR = 20, padT = 34, padB = 38;
        double plotW = w - padL - padR;
        double plotH = h - padT - padB;

        double maxTime = 0.1;
        foreach (var r in rows)
        {
            if (r.Time1Ms > maxTime) maxTime = r.Time1Ms;
            if (r.Time2Ms > maxTime) maxTime = r.Time2Ms;
        }
        double yMax = maxTime * 1.15;

        double XFor(int i) => rows.Count == 1 ? padL + plotW / 2 : padL + (double)i / (rows.Count - 1) * plotW;
        double YFor(double v) => padT + plotH - (v / yMax) * plotH;

        var blueBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#2563EB")!;
        var greenBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#16A34A")!;
        var gridBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#E2E8F0")!;
        var axisBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#94A3B8")!;

        // Легенда
        AddLegendItem(padL, 8, blueBrush, report.Algorithm1Name);
        AddLegendItem(padL + 195, 8, greenBrush, report.Algorithm2Name);

        // Сетка и подписи Y (мс)
        int gridLines = 4;
        for (int g = 0; g <= gridLines; g++)
        {
            double val = yMax * g / gridLines;
            double y = YFor(val);
            ChartCanvas.Children.Add(new Line { X1 = padL, Y1 = y, X2 = w - padR, Y2 = y, Stroke = gridBrush, StrokeThickness = 1 });
            var lbl = new TextBlock { Text = $"{val:F1}", FontSize = 10.5, Foreground = Brushes.SlateGray };
            Canvas.SetLeft(lbl, 6);
            Canvas.SetTop(lbl, y - 7);
            ChartCanvas.Children.Add(lbl);
        }

        // Оси
        ChartCanvas.Children.Add(new Line { X1 = padL, Y1 = padT, X2 = padL, Y2 = h - padB, Stroke = axisBrush, StrokeThickness = 1.2 });
        ChartCanvas.Children.Add(new Line { X1 = padL, Y1 = h - padB, X2 = w - padR, Y2 = h - padB, Stroke = axisBrush, StrokeThickness = 1.2 });

        var poly1 = new Polyline { Stroke = blueBrush, StrokeThickness = 2.4 };
        var poly2 = new Polyline { Stroke = greenBrush, StrokeThickness = 2.4 };

        for (int i = 0; i < rows.Count; i++)
        {
            double x = XFor(i);
            double y1 = YFor(rows[i].Time1Ms);
            double y2 = YFor(rows[i].Time2Ms);
            poly1.Points.Add(new Point(x, y1));
            poly2.Points.Add(new Point(x, y2));

            var xLbl = new TextBlock { Text = rows[i].WordCount.ToString(), FontSize = 10.5, Foreground = Brushes.SlateGray };
            Canvas.SetLeft(xLbl, x - 14);
            Canvas.SetTop(xLbl, h - padB + 6);
            ChartCanvas.Children.Add(xLbl);
        }

        ChartCanvas.Children.Add(poly1);
        ChartCanvas.Children.Add(poly2);

        // Точки данных
        for (int i = 0; i < rows.Count; i++)
        {
            double x = XFor(i);
            AddDot(x, YFor(rows[i].Time1Ms), blueBrush, $"{report.Algorithm1Name}: {rows[i].Time1Ms:F3} мс");
            AddDot(x, YFor(rows[i].Time2Ms), greenBrush, $"{report.Algorithm2Name}: {rows[i].Time2Ms:F3} мс");
        }

        var xTitle = new TextBlock { Text = "Количество слов", FontSize = 11, Foreground = Brushes.SlateGray };
        Canvas.SetLeft(xTitle, padL + plotW / 2 - 42);
        Canvas.SetTop(xTitle, h - 18);
        ChartCanvas.Children.Add(xTitle);
    }

    private void AddLegendItem(double x, double y, Brush brush, string text)
    {
        var rect = new Rectangle { Width = 11, Height = 11, RadiusX = 2, RadiusY = 2, Fill = brush };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        ChartCanvas.Children.Add(rect);

        var lbl = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeights.SemiBold };
        Canvas.SetLeft(lbl, x + 16);
        Canvas.SetTop(lbl, y - 2);
        ChartCanvas.Children.Add(lbl);
    }

    private void AddDot(double cx, double cy, Brush brush, string tooltip)
    {
        var dot = new Ellipse { Width = 8, Height = 8, Fill = brush, ToolTip = tooltip };
        Canvas.SetLeft(dot, cx - 4);
        Canvas.SetTop(dot, cy - 4);
        ChartCanvas.Children.Add(dot);
    }
}
