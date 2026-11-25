using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SortingLab4.App.Helpers;
using SortingLab4.Core.Algorithms;
using SortingLab4.Core.Models;

namespace SortingLab4.App.Views;

public partial class InternalSortView : UserControl
{
    private const int MinGeneratedArraySize = 4;
    private const int MaxArraySize = 50;
    private const int ArrayChipsLimit = 40;

    private sealed class LogEntryVm
    {
        public string StepHeader { get; init; } = string.Empty;
        public string TypeLabel { get; init; } = string.Empty;
        public string Explanation { get; init; } = string.Empty;
    }

    /// <summary>
    /// Постоянный визуальный столбец на Canvas (перемещается плавно без пересоздания).
    /// </summary>
    private sealed class BarVisual
    {
        public double Value { get; set; }
        public StackPanel Root { get; init; } = null!;
        public Border BarRect { get; init; } = null!;
        public SolidColorBrush FillBrush { get; init; } = null!;
        public TextBlock ValueText { get; init; } = null!;
        public TextBlock IndexText { get; init; } = null!;
    }

    private InternalSortAlgorithm _currentAlgo = InternalSortAlgorithm.Bubble;
    private double[] _sourceArray = { 8, 3, -2, 11, 5, 3, 1, 9 };
    private InternalSortResult? _currentResult;
    private int _stepIndex = -1;
    private bool _isPlaying;
    private bool _isPaused;
    private readonly DispatcherTimer _timer;
    private readonly Random _rng = new();

    // Постоянные элементы на Canvas для плавной анимации столбцов и указателей
    private readonly List<BarVisual> _bars = new();
    private Border? _tagI;
    private Border? _tagJ;
    private Border? _tagPivot;
    private Border? _tagKey;
    private double _barMin;
    private double _barSpan = 1;
    private double _colWidth = 46;
    private double _colStep = 54;
    private double _startX = 12;

    // Отдельный журнал операций для каждого алгоритма
    private readonly Dictionary<InternalSortAlgorithm, ObservableCollection<LogEntryVm>> _logsByAlgo = new()
    {
        [InternalSortAlgorithm.Bubble] = new(),
        [InternalSortAlgorithm.Insertion] = new(),
        [InternalSortAlgorithm.Merge] = new(),
        [InternalSortAlgorithm.QuickHoare] = new()
    };

    private static readonly Color ColDefault = ColorFromHex("#94A3B8");
    private static readonly Color ColInRange = ColorFromHex("#60A5FA");
    private static readonly Color ColComparing = ColorFromHex("#F59E0B");
    private static readonly Color ColSwapping = ColorFromHex("#EF4444");
    private static readonly Color ColSorted = ColorFromHex("#22C55E");
    private static readonly Color ColKey = ColorFromHex("#7C3AED");
    private static readonly Color ColShifted = ColorFromHex("#0EA5E9");
    private static readonly Color ColInserted = ColorFromHex("#10B981");
    private static readonly Color ColLeftPart = ColorFromHex("#0D9488");
    private static readonly Color ColRightPart = ColorFromHex("#EA580C");

    public InternalSortView()
    {
        InitializeComponent();
        SldSize.Minimum = MinGeneratedArraySize;
        SldSize.Maximum = MaxArraySize;
        TxtSizeInput.ToolTip = $"Введите N от {MinGeneratedArraySize} до {MaxArraySize}. Enter создаёт массив.";
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _timer.Tick += Timer_Tick;
        Loaded += (_, _) => ResetState(false);
    }

    private void AlgoRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (RbBubble.IsChecked == true) _currentAlgo = InternalSortAlgorithm.Bubble;
        else if (RbInsertion.IsChecked == true) _currentAlgo = InternalSortAlgorithm.Insertion;
        else if (RbMerge.IsChecked == true) _currentAlgo = InternalSortAlgorithm.Merge;
        else if (RbQuick.IsChecked == true) _currentAlgo = InternalSortAlgorithm.QuickHoare;

        ResetState(keepLogs: true);
    }

    private void SldSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int size = (int)e.NewValue;
        if (TxtSizeInput is not null && (!TryReadArraySize(out int typedSize) || typedSize != size))
            TxtSizeInput.Text = size.ToString(CultureInfo.InvariantCulture);
    }

    private bool TryReadArraySize(out int size) =>
        int.TryParse(TxtSizeInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out size)
        && size is >= MinGeneratedArraySize and <= MaxArraySize;

    private void TxtSizeInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SldSize is not null && TryReadArraySize(out int size))
            SldSize.Value = size;
    }

    private void TxtSizeInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        GenerateRandomArray();
    }

    private void SldSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int ms = (int)SldSpeed.Value;
        if (TxtSpeedVal is not null) TxtSpeedVal.Text = $"{ms} мс";
        if (_timer is not null) _timer.Interval = TimeSpan.FromMilliseconds(ms);
    }

    private void BarsCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_bars.Count == 0 || BarsCanvas.Visibility != Visibility.Visible) return;
        LayoutBars(animated: false);
        if (_currentResult is not null && _stepIndex >= 0 && _stepIndex < _currentResult.Steps.Count)
            UpdatePointers(_currentResult.Steps[_stepIndex], animated: false);
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

    private bool TryParseInputArray(out double[] parsed)
    {
        string raw = TxtArrayInput.Text.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            parsed = Array.Empty<double>();
            return true;
        }

        string[] tokens = raw.Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > MaxArraySize)
        {
            ShowError($"Введите не более {MaxArraySize} чисел.");
            parsed = Array.Empty<double>();
            return false;
        }

        var list = new List<double>(tokens.Length);
        foreach (var tok in tokens)
        {
            if (!double.TryParse(tok.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ||
                double.IsNaN(val) || double.IsInfinity(val))
            {
                ShowError($"Некорректное число: «{tok}». Вводите числа через пробел или запятую.");
                parsed = Array.Empty<double>();
                return false;
            }
            list.Add(val);
        }

        parsed = list.ToArray();
        return true;
    }

    private void ApplyArray_Click(object sender, RoutedEventArgs e)
    {
        ShowError(null);
        if (TryParseInputArray(out var parsed))
        {
            _sourceArray = parsed;
            ResetState(false);
        }
    }

    private void RandomArray_Click(object sender, RoutedEventArgs e) => GenerateRandomArray();

    private void GenerateRandomArray()
    {
        if (!TryReadArraySize(out int size))
        {
            ShowError($"Введите целое N от {MinGeneratedArraySize} до {MaxArraySize}.");
            TxtSizeInput.Focus();
            return;
        }

        ShowError(null);
        bool allowNeg = ChkNegative.IsChecked == true;
        _sourceArray = new double[size];
        for (int i = 0; i < size; i++)
            _sourceArray[i] = allowNeg ? _rng.Next(-15, 26) : _rng.Next(1, 45);

        TxtArrayInput.Text = string.Join(", ", _sourceArray.Select(InternalSortingAlgorithms.FormatNumber));
        ResetState(false);
    }

    private void UpdateButtons()
    {
        bool finished = _currentResult is not null && _stepIndex >= _currentResult.Steps.Count - 1;
        BtnStart.IsEnabled = !(_isPlaying && !_isPaused);
        BtnPause.IsEnabled = _isPlaying && !_isPaused && !finished;
        BtnResume.IsEnabled = _isPaused && !finished;
        BtnStep.IsEnabled = !(_isPlaying && !_isPaused);
    }

    private void ResetState(bool keepLogs)
    {
        _timer.Stop();
        _isPlaying = false;
        _isPaused = false;
        _currentResult = null;
        _stepIndex = -1;

        if (!keepLogs) _logsByAlgo[_currentAlgo].Clear();
        LstLog.ItemsSource = _logsByAlgo[_currentAlgo];

        TxtStepBadge.Text = "Готово";
        BadgeStepBorder.Background = BrushFromHex("#DBEAFE");
        TxtStepBadge.Foreground = BrushFromHex("#1D4ED8");
        TxtStepCounter.Text = "Шаг 0 / 0";
        TxtExplanation.Text = "Нажмите «Запуск» для автоматического показа или «Следующий шаг» для пошагового разбора.";
        TxtStats.Text = "Сравнений: 0  |  Перестановок: 0";
        TxtAuxInfo.Text = string.Empty;

        RenderArray(CurrentArrayPanel, _sourceArray, false);
        FinalArrayPanel.Children.Clear();
        FinalArrayPanel.Children.Add(new TextBlock { Text = "после завершения сортировки", FontSize = 12.5, Foreground = Brushes.SlateGray });

        RenderInitialVisualization();
        UpdateButtons();
    }

    private void RenderInitialVisualization()
    {
        RenderLegendAndHeader();
        bool showMergeTree = _currentAlgo == InternalSortAlgorithm.Merge;
        BarsCanvas.Visibility = (!showMergeTree && _sourceArray.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
        MergeScroll.Visibility = (showMergeTree && _sourceArray.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
        TxtEmptyStage.Visibility = _sourceArray.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (showMergeTree)
            RenderMergeTree(null);
        else
            BuildInitialBars(_sourceArray);
    }

    private bool EnsureResultComputed()
    {
        if (_currentResult is not null) return true;
        ShowError(null);
        if (!TryParseInputArray(out var parsed)) return false;
        _sourceArray = parsed;
        _logsByAlgo[_currentAlgo].Clear();

        _currentResult = InternalSortingAlgorithms.Run(_currentAlgo, _sourceArray);
        var st = _currentResult.Stats;
        TxtStats.Text = _currentAlgo == InternalSortAlgorithm.Merge
            ? $"Сравнений: {st.Comparisons}  |  Разделений: {st.Splits}  |  Слияний: {st.Merges}"
            : $"Сравнений: {st.Comparisons}  |  Перестановок/сдвигов: {st.Swaps + st.Shifts}";

        RenderInitialVisualization();
        return true;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        if (_currentResult is not null && _stepIndex >= _currentResult.Steps.Count - 1)
        {
            _currentResult = null;
            _stepIndex = -1;
            _logsByAlgo[_currentAlgo].Clear();
        }
        if (!EnsureResultComputed() || _currentResult!.Steps.Count == 0) return;

        _isPlaying = true;
        _isPaused = false;
        UpdateButtons();

        if (_stepIndex < 0)
        {
            _stepIndex = 0;
            RenderStep(0);
        }
        if (_stepIndex < _currentResult.Steps.Count - 1)
            _timer.Start();
        else
        {
            _isPlaying = false;
            UpdateButtons();
        }
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

    private void NextStep_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _isPlaying = false;
        _isPaused = false;
        if (!EnsureResultComputed() || _currentResult!.Steps.Count == 0) return;

        if (_stepIndex < _currentResult.Steps.Count - 1)
        {
            _stepIndex++;
            RenderStep(_stepIndex);
        }
        UpdateButtons();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetState(false);

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_currentResult is null || !_isPlaying || _isPaused)
        {
            _timer.Stop();
            return;
        }
        if (_stepIndex < _currentResult.Steps.Count - 1)
        {
            _stepIndex++;
            RenderStep(_stepIndex);
        }
        if (_stepIndex >= _currentResult.Steps.Count - 1)
        {
            _timer.Stop();
            _isPlaying = false;
            _isPaused = false;
            UpdateButtons();
        }
    }

    // =========================================================================
    // ПОСТОЯННЫЕ СТОЛБЦЫ НА CANVAS С ПЛАВНЫМИ АНИМАЦИЯМИ
    // =========================================================================
    private void BuildInitialBars(double[] arr)
    {
        BarsCanvas.Children.Clear();
        _bars.Clear();

        if (arr.Length == 0) return;

        _barMin = arr[0];
        double maxVal = arr[0];
        for (int i = 1; i < arr.Length; i++)
        {
            if (arr[i] < _barMin) _barMin = arr[i];
            if (arr[i] > maxVal) maxVal = arr[i];
        }
        _barSpan = maxVal - _barMin;
        if (_barSpan < 1e-9) _barSpan = 1;

        for (int idx = 0; idx < arr.Length; idx++)
        {
            var brush = new SolidColorBrush(ColDefault);
            var valTxt = new TextBlock
            {
                Text = InternalSortingAlgorithms.FormatNumber(arr[idx]),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var rect = new Border
            {
                Background = brush,
                Child = valTxt
            };
            var idxTxt = new TextBlock
            {
                Text = (idx + 1).ToString(),
                FontSize = 11,
                Foreground = BrushFromHex("#64748B"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            stack.Children.Add(rect);
            stack.Children.Add(idxTxt);

            var bv = new BarVisual
            {
                Value = arr[idx],
                Root = stack,
                BarRect = rect,
                FillBrush = brush,
                ValueText = valTxt,
                IndexText = idxTxt
            };
            _bars.Add(bv);
            BarsCanvas.Children.Add(stack);
        }

        // Плавающие метки-указатели поверх столбцов
        _tagI = MakeFloatingTag("i", "#2563EB");
        _tagJ = MakeFloatingTag("j", "#DC2626");
        _tagPivot = MakeFloatingTag("pivot", "#7C3AED");
        _tagKey = MakeFloatingTag("ключ", "#7C3AED");
        BarsCanvas.Children.Add(_tagI);
        BarsCanvas.Children.Add(_tagJ);
        BarsCanvas.Children.Add(_tagPivot);
        BarsCanvas.Children.Add(_tagKey);

        LayoutBars(animated: false);
    }

    private void ComputeBarMetrics()
    {
        int n = _bars.Count;
        double canvasW = BarsCanvas.ActualWidth > 0 ? BarsCanvas.ActualWidth : 680;
        _colStep = Math.Min(60, Math.Max(1, canvasW - 24) / n);
        _colWidth = _colStep - Math.Min(8, _colStep * 0.2);
        double totalW = (n - 1) * _colStep + _colWidth;
        _startX = (canvasW - totalW) / 2.0;
    }

    private double ComputeBarHeight(double val)
    {
        double canvasH = BarsCanvas.ActualHeight > 0 ? BarsCanvas.ActualHeight : 280;
        double verticalReserve = _currentAlgo == InternalSortAlgorithm.QuickHoare ? 96 : 68;
        double maxBarH = Math.Max(1, canvasH - verticalReserve);
        double minBarH = Math.Min(30, maxBarH);
        double norm = (val - _barMin) / _barSpan;
        return minBarH + norm * (maxBarH - minBarH);
    }

    private double ComputeBarTop(double barHeight)
    {
        double canvasH = BarsCanvas.ActualHeight > 0 ? BarsCanvas.ActualHeight : 280;
        return Math.Max(0, canvasH - barHeight - 24);
    }

    private void LayoutBars(bool animated, int durationMs = 220)
    {
        if (_bars.Count == 0) return;
        ComputeBarMetrics();
        bool showLabels = _colWidth >= 24;
        var labelVisibility = showLabels ? Visibility.Visible : Visibility.Collapsed;
        var cornerRadius = showLabels ? new CornerRadius(5, 5, 2, 2) : new CornerRadius(2);
        var padding = showLabels ? new Thickness(2, 5, 2, 2) : new Thickness(0);

        for (int i = 0; i < _bars.Count; i++)
        {
            var b = _bars[i];
            double h = ComputeBarHeight(b.Value);
            double x = _startX + i * _colStep;
            double y = ComputeBarTop(h);

            b.Root.Width = _colWidth;
            b.BarRect.Height = h;
            b.BarRect.CornerRadius = cornerRadius;
            b.BarRect.Padding = padding;
            b.ValueText.Visibility = labelVisibility;
            int position = i + 1;
            bool showIndex = showLabels || position == 1 || position % 5 == 0 || position == _bars.Count;
            b.IndexText.Visibility = showIndex ? Visibility.Visible : Visibility.Collapsed;
            b.IndexText.FontSize = showLabels ? 11 : Math.Min(9, _colWidth * 0.9);
            b.IndexText.Text = position.ToString();
            b.Root.ToolTip = $"Позиция {position}, значение {b.ValueText.Text}";

            Canvas.SetTop(b.Root, y);
            if (animated)
                UiAnimations.AnimateCanvasLeft(b.Root, x, durationMs);
            else
            {
                b.Root.BeginAnimation(Canvas.LeftProperty, null);
                Canvas.SetLeft(b.Root, x);
            }
        }
    }

    private void AnimateBarsForStep(InternalSortStep st)
    {
        if (_bars.Count != st.ArraySnapshot.Length)
        {
            BuildInitialBars(st.ArraySnapshot);
        }

        int animMs = StepAnimationDuration();

        // 1. Обработка физических перемещений столбцов (swap / shift / insert)
        if (st.OperationType == "swap" && st.SwappingIndices.Count == 2)
        {
            int a = st.SwappingIndices[0];
            int b = st.SwappingIndices[1];
            if (a >= 0 && a < _bars.Count && b >= 0 && b < _bars.Count)
            {
                (_bars[a], _bars[b]) = (_bars[b], _bars[a]);
            }
        }
        else if (st.OperationType == "shift" && st.ShiftedIndex.HasValue)
        {
            int dst = st.ShiftedIndex.Value;
            int src = dst - 1;
            if (src >= 0 && dst < _bars.Count)
            {
                (_bars[src], _bars[dst]) = (_bars[dst], _bars[src]);
            }
        }

        // Синхронизируем значения с реальным снимком массива (гарантия точности на Insertion Sort и др.)
        for (int i = 0; i < _bars.Count; i++)
        {
            double expected = st.ArraySnapshot[i];
            if (_bars[i].Value != expected)
            {
                _bars[i].Value = expected;
                _bars[i].ValueText.Text = InternalSortingAlgorithms.FormatNumber(expected);
            }
        }

        LayoutBars(animated: true, durationMs: animMs);

        // 2. Плавное обновление цветов, прозрачности и приподнятия столбцов
        var comparing = new HashSet<int>(st.ComparingIndices);
        var swapping = new HashSet<int>(st.SwappingIndices);
        var sorted = new HashSet<int>(st.SortedIndices);
        var leftPart = new HashSet<int>(st.LeftPartIndices);
        var rightPart = new HashSet<int>(st.RightPartIndices);

        for (int idx = 0; idx < _bars.Count; idx++)
        {
            var bar = _bars[idx];
            Color targetColor = ColDefault;
            double targetOpacity = 1.0;
            double targetLiftY = 0;

            if (_currentAlgo == InternalSortAlgorithm.QuickHoare && st.RangeLeft is not null && st.RangeRight is not null)
            {
                if (idx >= st.RangeLeft.Value && idx <= st.RangeRight.Value) targetColor = ColInRange;
                else if (!sorted.Contains(idx)) targetOpacity = 0.32;
            }

            if (sorted.Contains(idx) || st.OperationType == "done") targetColor = ColSorted;
            if (leftPart.Contains(idx)) targetColor = ColLeftPart;
            if (rightPart.Contains(idx)) targetColor = ColRightPart;
            if (_currentAlgo == InternalSortAlgorithm.QuickHoare && st.PivotIndex == idx) targetColor = ColKey;
            if (_currentAlgo == InternalSortAlgorithm.Insertion && st.CurrentIndex == idx)
            {
                targetColor = ColKey;
                targetLiftY = -10;
            }
            if (_currentAlgo == InternalSortAlgorithm.Insertion && st.ShiftedIndex == idx) targetColor = ColShifted;
            if (_currentAlgo == InternalSortAlgorithm.Insertion && st.InsertedIndex == idx) targetColor = ColInserted;

            if (comparing.Contains(idx))
            {
                targetColor = ColComparing;
                targetLiftY = -7;
            }
            if (swapping.Contains(idx))
            {
                targetColor = ColSwapping;
                targetLiftY = -10;
            }

            UiAnimations.AnimateBrushColor(bar.FillBrush, targetColor, Math.Min(animMs, 180));
            UiAnimations.AnimateOpacity(bar.Root, targetOpacity, Math.Min(animMs, 180));
            UiAnimations.AnimateOffsetY(bar.Root, targetLiftY, Math.Min(animMs, 200));
        }

        UpdatePointers(st, animated: true, durationMs: animMs);
    }

    private int StepAnimationDuration() => Math.Clamp((int)(SldSpeed.Value * 0.65), 60, 280);

    private void UpdatePointers(InternalSortStep st, bool animated, int durationMs = 200)
    {
        if (_tagI is null || _tagJ is null || _tagPivot is null || _tagKey is null) return;

        void PositionTag(Border tag, string name, int? idx, double topOffset)
        {
            if (idx is null || idx.Value < 0 || idx.Value >= _bars.Count)
            {
                tag.Visibility = Visibility.Collapsed;
                return;
            }
            bool wasHidden = tag.Visibility != Visibility.Visible;
            tag.Visibility = Visibility.Visible;
            if (tag.Child is TextBlock label) label.Text = $"{name} · {idx.Value + 1}";
            tag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double center = _startX + idx.Value * _colStep + _colWidth / 2;
            double tagWidth = tag.DesiredSize.Width;
            double maxLeft = Math.Max(0, BarsCanvas.ActualWidth - tagWidth);
            double x = Math.Clamp(center - tagWidth / 2, 0, maxLeft);
            Canvas.SetTop(tag, topOffset);
            if (animated && !wasHidden)
                UiAnimations.AnimateCanvasLeft(tag, x, durationMs);
            else
            {
                tag.BeginAnimation(Canvas.LeftProperty, null);
                Canvas.SetLeft(tag, x);
            }
        }

        if (_currentAlgo == InternalSortAlgorithm.QuickHoare)
        {
            // Соседние указатели могут смотреть на один столбец или на узкие соседние столбцы.
            PositionTag(_tagI, "i", st.PointerI, 4);
            PositionTag(_tagJ, "j", st.PointerJ, 24);
            PositionTag(_tagPivot, "pivot", st.PivotIndex, 44);
            _tagKey.Visibility = Visibility.Collapsed;
        }
        else if (_currentAlgo == InternalSortAlgorithm.Insertion)
        {
            _tagI.Visibility = Visibility.Collapsed;
            _tagJ.Visibility = Visibility.Collapsed;
            _tagPivot.Visibility = Visibility.Collapsed;
            PositionTag(_tagKey, "ключ", st.CurrentIndex ?? st.InsertedIndex, 4);
        }
        else
        {
            _tagI.Visibility = Visibility.Collapsed;
            _tagJ.Visibility = Visibility.Collapsed;
            _tagPivot.Visibility = Visibility.Collapsed;
            _tagKey.Visibility = Visibility.Collapsed;
        }
    }

    // =========================================================================
    // MERGE SORT (Дерево рекурсивного разделения и панель слияния)
    // =========================================================================
    private void RenderMergeTree(InternalSortStep? st)
    {
        MergeStage.Children.Clear();
        var outer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        if (st?.TreeNodes is null || st.TreeNodes.Count == 0)
        {
            if (_sourceArray.Length == 0) return;
            outer.Children.Add(MakeMergeNodeCard(new MergeTreeNode
            {
                Side = "root", LeftIndex = 0, RightIndex = _sourceArray.Length - 1,
                Values = new List<double>(_sourceArray), State = "active"
            }, isCurrent: true));
            MergeStage.Children.Add(outer);
            MergeScroll.ScrollToTop();
            return;
        }

        int maxDepth = 0;
        var levels = new Dictionary<int, List<MergeTreeNode>>();
        foreach (var nd in st.TreeNodes)
        {
            if (!levels.TryGetValue(nd.Depth, out var list))
            {
                list = new List<MergeTreeNode>();
                levels[nd.Depth] = list;
            }
            list.Add(nd);
            if (nd.Depth > maxDepth) maxDepth = nd.Depth;
        }

        FrameworkElement? highlighted = null;
        for (int d = 0; d <= maxDepth; d++)
        {
            if (!levels.TryGetValue(d, out var levelNodes)) continue;
            var levelRow = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            foreach (var nd in levelNodes)
            {
                bool isCur = nd.Id == st.ActiveNodeId;
                var card = MakeMergeNodeCard(nd, isCur);
                if (isCur) highlighted = card;
                if (st.OperationType == "split" && nd.ParentId == st.ActiveNodeId)
                    UiAnimations.FadeIn(card, durationMs: StepAnimationDuration());
                levelRow.Children.Add(card);
            }
            outer.Children.Add(levelRow);
        }

        if (st.MergeContext is not null)
        {
            var mc = st.MergeContext;
            var mergeBox = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromHex("#F59E0B"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 8, 12, 9),
                Margin = new Thickness(0, 4, 0, 0)
            };
            var mergeStack = new StackPanel();
            mergeStack.Children.Add(new TextBlock
            {
                Text = "Слияние двух отсортированных частей",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromHex("#B45309"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var row = new UniformGrid { Rows = 1, Columns = 3 };
            row.Children.Add(MakeMergePartPanel("Левая", mc.LeftValues, mc.LeftPtr, mc.ComparingLeft, false));
            row.Children.Add(MakeMergePartPanel("Правая", mc.RightValues, mc.RightPtr, mc.ComparingRight, false));
            row.Children.Add(MakeMergePartPanel("Результат", mc.ResultValues, 0, null, true));

            mergeStack.Children.Add(row);
            mergeBox.Child = mergeStack;
            outer.Children.Insert(0, mergeBox);
            highlighted = mergeBox;
        }

        MergeStage.Children.Add(outer);
        if (highlighted is not null)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (highlighted.IsLoaded && highlighted.IsVisible) highlighted.BringIntoView();
            }, DispatcherPriority.Loaded);
        }
    }

    private void RenderLegendAndHeader()
    {
        LegendPanel.Children.Clear();
        (string Title, string ShortName, (string Color, string Label)[] Legend) meta = _currentAlgo switch
        {
            InternalSortAlgorithm.Bubble => (
                "Bubble Sort (Сортировка пузырьком)", "Bubble Sort",
                new[] { ("#94A3B8", "Обычный"), ("#F59E0B", "Сравнение"), ("#EF4444", "Перестановка"), ("#22C55E", "Отсортировано") }),
            InternalSortAlgorithm.Insertion => (
                "Insertion Sort (Сортировка вставками)", "Insertion Sort",
                new[] { ("#22C55E", "Отсортированная часть"), ("#7C3AED", "Ключ"), ("#F59E0B", "Сравнение"), ("#0EA5E9", "Сдвиг"), ("#94A3B8", "Остальные") }),
            InternalSortAlgorithm.Merge => (
                "Merge Sort (Дерево разделения и слияния)", "Merge Sort",
                new[] { ("#2563EB", "Активный"), ("#0D9488", "Одно число"), ("#D97706", "Слияние"), ("#16A34A", "Готово") }),
            _ => (
                "Quick Sort (Быстрая сортировка Хоара)", "Quick Sort (Хоара)",
                new[] { ("#60A5FA", "Участок"), ("#7C3AED", "Опорное"), ("#F59E0B", "Сравнение"), ("#EF4444", "Обмен"), ("#0D9488", "Левая часть"), ("#EA580C", "Правая часть"), ("#22C55E", "Готово") })
        };

        TxtVizTitle.Text = meta.Title;
        TxtLogAlgo.Text = meta.ShortName;

        foreach (var (hex, label) in meta.Legend)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 2) };
            item.Children.Add(new Border
            {
                Width = 10, Height = 10, CornerRadius = new CornerRadius(2),
                Background = BrushFromHex(hex), Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            item.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = BrushFromHex("#52525B") });
            LegendPanel.Children.Add(item);
        }
    }

    private void RenderStep(int idx)
    {
        if (_currentResult is null || idx < 0 || idx >= _currentResult.Steps.Count) return;
        var st = _currentResult.Steps[idx];

        TxtStepBadge.Text = st.OperationLabel;
        var (badgeBg, badgeFg) = st.OperationType switch
        {
            "done" => ("#DCFCE7", "#15803D"),
            "swap" or "shift" => ("#FEE2E2", "#B91C1C"),
            "compare" or "merge_compare" => ("#FEF3C7", "#B45309"),
            "pivot" or "select" or "split" => ("#F3E8FF", "#6D28D9"),
            _ => ("#DBEAFE", "#1D4ED8")
        };
        BadgeStepBorder.Background = BrushFromHex(badgeBg);
        TxtStepBadge.Foreground = BrushFromHex(badgeFg);

        TxtStepCounter.Text = $"Шаг {st.StepNumber} / {_currentResult.Steps.Count}";
        TxtExplanation.Text = st.Explanation;

        if (_currentAlgo == InternalSortAlgorithm.Insertion && st.KeyValue.HasValue)
            TxtAuxInfo.Text = $"Ключ: {InternalSortingAlgorithms.FormatNumber(st.KeyValue.Value)}";
        else if (_currentAlgo == InternalSortAlgorithm.QuickHoare && st.PivotValue.HasValue)
            TxtAuxInfo.Text = $"Pivot: {InternalSortingAlgorithms.FormatNumber(st.PivotValue.Value)}" +
                (st.RangeLeft.HasValue && st.RangeRight.HasValue ? $"   Участок: [{st.RangeLeft.Value + 1}..{st.RangeRight.Value + 1}]" : "");
        else if (_currentAlgo == InternalSortAlgorithm.Bubble && st.PassNumber > 0)
            TxtAuxInfo.Text = $"Проход {st.PassNumber}";
        else
            TxtAuxInfo.Text = string.Empty;

        if (_currentAlgo == InternalSortAlgorithm.Merge)
            RenderMergeTree(st);
        else
            AnimateBarsForStep(st);

        RenderArray(CurrentArrayPanel, st.ArraySnapshot, st.OperationType == "done");
        if (st.OperationType == "done")
            RenderArray(FinalArrayPanel, _currentResult.SortedArray, true);

        var logCol = _logsByAlgo[_currentAlgo];
        while (logCol.Count <= idx)
        {
            var entry = _currentResult.Steps[logCol.Count];
            logCol.Add(new LogEntryVm
            {
                StepHeader = $"Шаг {entry.StepNumber}",
                TypeLabel = entry.OperationLabel,
                Explanation = entry.Explanation
            });
        }
        if (logCol.Count > 0) LstLog.ScrollIntoView(logCol[^1]);
    }

    private static Border MakeMergeNodeCard(MergeTreeNode nd, bool isCurrent)
    {
        var (bgHex, borderHex, cellBgHex) = nd.State switch
        {
            "active" => ("#EFF6FF", "#2563EB", "#DBEAFE"),
            "single" => ("#F0FDFA", "#0D9488", "#CCFBF1"),
            "merging" => ("#FFFBEB", "#D97706", "#FEF3C7"),
            "merged" => ("#F0FDF4", "#16A34A", "#BBF7D0"),
            _ => ("#F8FAFC", "#94A3B8", "#E2E8F0")
        };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var valsPanel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var v in nd.Values)
        {
            valsPanel.Children.Add(new Border
            {
                Background = BrushFromHex(cellBgHex),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(2),
                Child = new TextBlock
                {
                    Text = InternalSortingAlgorithms.FormatNumber(v),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12
                }
            });
        }
        stack.Children.Add(valsPanel);
        string sideLabel = nd.Side == "root" ? "Исходный" : nd.Side == "left" ? "Левая" : "Правая";
        stack.Children.Add(new TextBlock
        {
            Text = $"{sideLabel} [{nd.LeftIndex + 1}..{nd.RightIndex + 1}]",
            FontSize = 10, Foreground = BrushFromHex("#64748B"),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0)
        });

        return new Border
        {
            Background = BrushFromHex(bgHex),
            BorderBrush = BrushFromHex(borderHex),
            BorderThickness = new Thickness(isCurrent ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 4, 7, 4),
            Margin = new Thickness(4),
            Child = stack
        };
    }

    private static StackPanel MakeMergePartPanel(string title, List<double> values, int ptr, int? comparingIdx, bool isResult)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = title, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = BrushFromHex("#64748B"), HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });
        var cells = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        if (values.Count == 0)
        {
            cells.Children.Add(new TextBlock { Text = "пусто", FontSize = 11.5, Foreground = Brushes.SlateGray });
        }
        else
        {
            for (int i = 0; i < values.Count; i++)
            {
                string bg = isResult ? "#22C55E" : (comparingIdx == i ? "#F59E0B" : "#E2E8F0");
                Brush fg = (isResult || comparingIdx == i) ? Brushes.White : BrushFromHex("#18181B");
                double op = (!isResult && i < ptr) ? 0.3 : 1.0;

                cells.Children.Add(new Border
                {
                    Background = BrushFromHex(bg),
                    Opacity = op,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(2),
                    Child = new TextBlock
                    {
                        Text = InternalSortingAlgorithms.FormatNumber(values[i]),
                        Foreground = fg, FontWeight = FontWeights.SemiBold, FontSize = 12
                    }
                });
            }
        }
        panel.Children.Add(cells);
        return panel;
    }

    private static void RenderArray(Panel panel, double[] arr, bool isDone)
    {
        if (arr.Length > ArrayChipsLimit)
        {
            var field = panel.Children.Count == 1 ? panel.Children[0] as TextBox : null;
            if (field is null)
            {
                field = new TextBox { IsReadOnly = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
                panel.Children.Clear();
                panel.Children.Add(field);
            }
            field.Text = string.Join(", ", arr.Select(InternalSortingAlgorithms.FormatNumber));
            return;
        }

        panel.Children.Clear();
        if (arr.Length == 0)
        {
            panel.Children.Add(new TextBlock { Text = "[]", FontSize = 12.5, Foreground = Brushes.SlateGray });
            return;
        }
        var chips = new WrapPanel();
        panel.Children.Add(chips);
        foreach (var v in arr)
        {
            chips.Children.Add(new Border
            {
                Background = BrushFromHex(isDone ? "#DCFCE7" : "#F1F5F9"),
                BorderBrush = BrushFromHex(isDone ? "#86EFAC" : "#E2E8F0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 4, 3),
                Child = new TextBlock
                {
                    Text = InternalSortingAlgorithms.FormatNumber(v),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = BrushFromHex(isDone ? "#166534" : "#18181B")
                }
            });
        }
    }

    private static Border MakeFloatingTag(string text, string bgHex) => new()
    {
        Background = BrushFromHex(bgHex),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(6, 1, 6, 2),
        Visibility = Visibility.Collapsed,
        Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold }
    };

    private static Color ColorFromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
    private static SolidColorBrush BrushFromHex(string hex) => new(ColorFromHex(hex));
}
