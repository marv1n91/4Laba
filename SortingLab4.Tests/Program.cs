using SortingLab4.Core.Algorithms;
using SortingLab4.Core.Models;
using SortingLab4.Core.Services;

int passed = 0;
int failed = 0;

void RunTest(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"[PASS] {name}");
        passed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {name}: {ex.Message}");
        failed++;
    }
}

void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

bool IsNonDecreasing(IReadOnlyList<double> seq)
{
    for (int i = 0; i < seq.Count - 1; i++)
        if (seq[i] > seq[i + 1]) return false;
    return true;
}

bool ArraysEqual<T>(IReadOnlyList<T> a, IReadOnlyList<T> b) where T : IEquatable<T>
{
    if (a.Count != b.Count) return false;
    for (int i = 0; i < a.Count; i++)
        if (!a[i].Equals(b[i])) return false;
    return true;
}

string FindDataFile(string fileName)
{
    string dir = AppContext.BaseDirectory;
    for (int i = 0; i < 6; i++)
    {
        string candidate = Path.Combine(dir, "data", fileName);
        if (File.Exists(candidate)) return candidate;
        var parent = Directory.GetParent(dir);
        if (parent is null) break;
        dir = parent.FullName;
    }
    return Path.Combine(Directory.GetCurrentDirectory(), "data", fileName);
}

var allInternal = new[]
{
    InternalSortAlgorithm.Bubble,
    InternalSortAlgorithm.Insertion,
    InternalSortAlgorithm.Merge,
    InternalSortAlgorithm.QuickHoare
};

var allExternal = new[]
{
    ExternalSortMethod.DirectMerge,
    ExternalSortMethod.NaturalMerge,
    ExternalSortMethod.MultiwayMerge
};

Console.WriteLine("=== 1. Тесты внутренней сортировки (п. 7.1 ТЗ) ===");

RunTest("Пустой массив (все 4 алгоритма)", () =>
{
    foreach (var algo in allInternal)
    {
        var res = InternalSortingAlgorithms.Run(algo, Array.Empty<double>());
        Assert(res.SortedArray.Length == 0, $"{algo}: ожидался пустой массив");
        Assert(res.Steps.Count >= 1 && res.Steps[^1].OperationType == "done", $"{algo}: нет шага done");
    }
});

RunTest("Один элемент (все 4 алгоритма)", () =>
{
    foreach (var algo in allInternal)
    {
        var res = InternalSortingAlgorithms.Run(algo, new double[] { 42 });
        Assert(ArraysEqual(res.SortedArray, new double[] { 42 }), $"{algo}: неверный результат");
    }
});

RunTest("Уже отсортированный массив (все 4 алгоритма)", () =>
{
    double[] input = { 1, 2, 3, 4, 5, 6 };
    foreach (var algo in allInternal)
    {
        var res = InternalSortingAlgorithms.Run(algo, input);
        Assert(ArraysEqual(res.SortedArray, input), $"{algo}: неверный результат");
    }
});

RunTest("Обратный порядок (все 4 алгоритма)", () =>
{
    double[] input = { 9, 7, 5, 3, 1, -1 };
    double[] expected = { -1, 1, 3, 5, 7, 9 };
    foreach (var algo in allInternal)
    {
        var res = InternalSortingAlgorithms.Run(algo, input);
        Assert(ArraysEqual(res.SortedArray, expected), $"{algo}: неверный результат");
    }
});

RunTest("Повторяющиеся элементы (все 4 алгоритма)", () =>
{
    double[] input = { 5, 2, 5, 1, 2, 5, 1 };
    double[] expected = { 1, 1, 2, 2, 5, 5, 5 };
    foreach (var algo in allInternal)
    {
        var res = InternalSortingAlgorithms.Run(algo, input);
        Assert(ArraysEqual(res.SortedArray, expected), $"{algo}: неверный результат");
    }
});

RunTest("Отрицательные числа (все 4 алгоритма)", () =>
{
    double[] input = { -10, 5, -3, 0, -10, 2, -1 };
    double[] expected = { -10, -10, -3, -1, 0, 2, 5 };
    foreach (var algo in allInternal)
    {
        var res = InternalSortingAlgorithms.Run(algo, input);
        Assert(ArraysEqual(res.SortedArray, expected), $"{algo}: неверный результат");
    }
});

RunTest("Случайные массивы (согласованность всех 4 алгоритмов)", () =>
{
    var rng = new Random(12345);
    foreach (int size in new[] { 8, 15, 25 })
    {
        var arr = new double[size];
        for (int i = 0; i < size; i++) arr[i] = rng.Next(-50, 51);
        var baseline = InternalSortingAlgorithms.Run(InternalSortAlgorithm.Bubble, arr).SortedArray;
        Assert(IsNonDecreasing(baseline), "Bubble Sort не упорядочил массив");
        foreach (var algo in allInternal)
        {
            var res = InternalSortingAlgorithms.Run(algo, arr);
            Assert(ArraysEqual(res.SortedArray, baseline), $"{algo}: расхождение с эталоном");
        }
    }
});

RunTest("1000 элементов: сортировка с основными этапами", () =>
{
    double[] input = Enumerable.Range(1, 1000).Reverse().Select(value => (double)value).ToArray();
    double[] expected = input.Reverse().ToArray();
    foreach (var algo in allInternal)
    {
        var result = InternalSortingAlgorithms.Run(algo, input, detailedSteps: false);
        Assert(ArraysEqual(result.SortedArray, expected), $"{algo}: неверный итог сортировки");
        Assert(result.Steps.Count >= 3 && result.Steps.Count <= 82, $"{algo}: неверное число этапов");
        Assert(ArraysEqual(result.Steps[0].ArraySnapshot, input), $"{algo}: неверный первый снимок");
        Assert(result.Steps[^1].OperationType == "done" && ArraysEqual(result.Steps[^1].ArraySnapshot, expected),
            $"{algo}: неверный последний снимок");
        if (algo is InternalSortAlgorithm.Bubble or InternalSortAlgorithm.Insertion)
            Assert(result.Stats.Comparisons == 499500, $"{algo}: пропущены сравнения");
    }
});

RunTest("Merge Sort: дерево разделения и слияния", () =>
{
    var res = InternalSortingAlgorithms.MergeSort(new double[] { 8, 3, 5, 1 });
    var types = res.Steps.Select(s => s.OperationType).ToHashSet();
    Assert(types.Contains("split"), "Нет шага split");
    Assert(types.Contains("merge_compare"), "Нет шага merge_compare");
    Assert(types.Contains("merge_end"), "Нет шага merge_end");
    Assert(res.Steps[^1].TreeNodes is not null && res.Steps[^1].TreeNodes!.Count >= 7, "Неполное дерево рекурсии");
});

RunTest("Quick Sort (Хоара): опорный элемент и разделение", () =>
{
    var res = InternalSortingAlgorithms.QuickSortHoare(new double[] { 8, 3, 5, 1, 9, 2 });
    var types = res.Steps.Select(s => s.OperationType).ToHashSet();
    Assert(types.Contains("pivot"), "Нет шага pivot");
    Assert(types.Contains("partition_end"), "Нет шага partition_end");
});

Console.WriteLine("\n=== 2. Тесты внешней сортировки (п. 7.2 ТЗ) ===");

RunTest("Сценарий 1: Страны Европы по населению (все 3 метода + целостность записей)", () =>
{
    var doc = TableFileService.LoadFromFile(FindDataFile("countries.csv"));
    var europe = TableFileService.FilterRows(doc.Rows, 1, "Европа");
    Assert(europe.Count >= 5, "Ожидалось >= 5 стран Европы");
    var expectedSet = europe.Select(r => string.Join("|", r)).ToHashSet();

    foreach (var method in allExternal)
    {
        var res = ExternalSortingAlgorithms.Sort(method, doc.Headers, europe, 4, KeyComparisonType.Number, ways: 3);
        Assert(res.SortedRows.Count == europe.Count, $"{method}: потеряны записи");
        var pops = res.SortedRows.Select(r => double.Parse(r[4])).ToList();
        Assert(IsNonDecreasing(pops), $"{method}: население не отсортировано");
        var actualSet = res.SortedRows.Select(r => string.Join("|", r)).ToHashSet();
        Assert(actualSet.SetEquals(expectedSet), $"{method}: нарушена целостность атрибутов");
    }
});

RunTest("Сценарий 2: Химические вещества (Кислоты по массе, все 3 метода)", () =>
{
    var doc = TableFileService.LoadFromFile(FindDataFile("chemicals.csv"));
    var acids = TableFileService.FilterRows(doc.Rows, 0, "Кислоты");
    foreach (var method in allExternal)
    {
        var res = ExternalSortingAlgorithms.Sort(method, doc.Headers, acids, 2, KeyComparisonType.Number, ways: 3);
        var masses = res.SortedRows.Select(r => double.Parse(r[2], System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert(IsNonDecreasing(masses), $"{method}: массы не отсортированы");
    }
});

RunTest("Сценарий 3: Русские слова с буквой «ё» (все 3 метода)", () =>
{
    var doc = TableFileService.LoadFromFile(FindDataFile("russian_words.txt"));
    foreach (var method in allExternal)
    {
        var res = ExternalSortingAlgorithms.Sort(method, doc.Headers, doc.Rows, 0, KeyComparisonType.Text, ways: 3);
        var words = res.SortedRows.Select(r => r[0]).ToList();
        Assert(words.Count == doc.Rows.Count, $"{method}: неверное число слов");
        Assert(words.IndexOf("ель") < words.IndexOf("ёжик"), $"{method}: «ёжик» должен идти после «ель»");
        Assert(words.IndexOf("ёжик") < words.IndexOf("задача"), $"{method}: «ёжик» должен идти перед «задача»");
    }
});

RunTest("Большая таблица (120 записей, 6 столбцов, повторяющиеся ключи, все 3 метода)", () =>
{
    var headers = new List<string> { "ID", "Name", "Cat", "Score", "Note", "Extra" };
    var rng = new Random(77);
    var rows = new List<string[]>();
    for (int i = 0; i < 120; i++)
        rows.Add(new[] { i.ToString(), $"Item_{i}", $"C{i % 4}", rng.Next(1, 20).ToString(), "ok", $"ex_{i}" });

    var expectedSet = rows.Select(r => string.Join("|", r)).ToHashSet();
    foreach (var method in allExternal)
    {
        var res = ExternalSortingAlgorithms.Sort(method, headers, rows, 3, KeyComparisonType.Number, ways: 4);
        Assert(res.SortedRows.Count == 120, $"{method}: неверное число записей");
        var scores = res.SortedRows.Select(r => double.Parse(r[3])).ToList();
        Assert(IsNonDecreasing(scores), $"{method}: ключи не отсортированы");
        var actualSet = res.SortedRows.Select(r => string.Join("|", r)).ToHashSet();
        Assert(actualSet.SetEquals(expectedSet), $"{method}: нарушена целостность записей");
    }
});

RunTest("Внешнее слияние показывает сравниваемые записи и дописывание остатка", () =>
{
    var rows = new[] { 3, 5, 1, 3, 4, 0, 6 }.Select((key, index) => new[] { $"Запись {index + 1}", key.ToString() }).ToList();
    foreach (var method in allExternal)
    {
        var result = ExternalSortingAlgorithms.Sort(method, ["Имя", "Ключ"], rows, 1, KeyComparisonType.Number);
        var writes = result.Steps.Where(step => step.MergeDecision is not null).ToList();
        Assert(writes.Any(step => step.OperationType == "merge_compare" && step.MergeDecision!.HasEqualMinimum), $"{method}: не показано равенство ключей");
        Assert(writes.Any(step => step.OperationType == "merge_append" && step.MergeDecision!.Candidates.Count == 1), $"{method}: не показан остаток серии");
        foreach (var step in writes)
        {
            var decision = step.MergeDecision!;
            var written = step.MainFileRecords[decision.OutputPosition - 1];
            Assert(written.DisplayText == decision.Chosen.Record.DisplayText, $"{method}: записана другая строка");
            Assert(int.Parse(decision.Chosen.Record.Key) == decision.Candidates.Min(item => int.Parse(item.Record.Key)),
                $"{method}: выбран неверный ключ");
        }
    }
});

RunTest("Пустой и некорректный файл выбрасывают ошибку", () =>
{
    bool threwEmpty = false;
    try { TableFileService.ParseTableText("   \n ", "empty.csv"); }
    catch (InvalidDataException) { threwEmpty = true; }
    Assert(threwEmpty, "Пустой файл не вызвал исключение");

    bool threwBad = false;
    try { TableFileService.ParseTableText("A,B,C\n1,2\n3,4,5", "bad.csv"); }
    catch (InvalidDataException) { threwBad = true; }
    Assert(threwBad, "Некорректная таблица не вызвала исключение");
});

Console.WriteLine("\n=== 3. Тесты лексикографической сортировки (п. 7.3 ТЗ) ===");

RunTest("Пустой текст", () =>
{
    var res = LexicographicSortingAlgorithms.ProcessText("", LexicographicAlgorithm.QuickSortHoare);
    Assert(res.TotalWords == 0 && res.UniqueWords == 0, "Ожидался пустой результат");
});

RunTest("Повторы, регистр, пунктуация, частоты и совпадение алгоритмов", () =>
{
    string text = "Apple, banana! apple? Orange; BANANA, banana. Computer, house, apple!";
    var words = LexicographicSortingAlgorithms.ExtractWords(text, true);
    var q = LexicographicSortingAlgorithms.QuickSortWords(words);
    var m = LexicographicSortingAlgorithms.MergeSortWords(words);
    var r = LexicographicSortingAlgorithms.AbcRadixSortWords(words);

    Assert(ArraysEqual(q, m), "Quick и Merge не совпали");
    Assert(ArraysEqual(q, r), "Quick и ABC/Radix не совпали");

    var freqs = LexicographicSortingAlgorithms.CountFrequenciesFromSorted(q)
        .ToDictionary(x => x.Word, x => x.Count);
    Assert(freqs["apple"] == 3 && freqs["banana"] == 3 && freqs["orange"] == 1, "Неверный подсчёт частот");
});

RunTest("Русские и английские слова: полное совпадение алгоритмов", () =>
{
    var words = LexicographicSortingAlgorithms.ExtractWords("ёжик ель яблоко арбуз банан ель ёжик apple zebra alpha", true);
    var q = LexicographicSortingAlgorithms.QuickSortWords(words);
    var r = LexicographicSortingAlgorithms.AbcRadixSortWords(words);
    Assert(ArraysEqual(q, r), "Расхождение на смешанном тексте");
});

RunTest("Лексикографические шаги показывают причину порядка и дают тот же итог", () =>
{
    string[] words = ["котик", "кит", "кот", "ёж", "еж"];
    foreach (var algorithm in Enum.GetValues<LexicographicAlgorithm>())
    {
        var trace = LexicographicSortingAlgorithms.TraceWords(algorithm, words);
        Assert(ArraysEqual(trace[0].Items, words), $"{algorithm}: неверный исходный шаг");
        Assert(ArraysEqual(trace[^1].Items, LexicographicSortingAlgorithms.SortWords(algorithm, words)), $"{algorithm}: неверный итог разбора");
        Assert(algorithm == LexicographicAlgorithm.AbcRadixSort
                ? trace.Any(step => step.Items.Contains("конец слова → кот"))
                : trace.Any(step => step.Explanation.Contains("На позиции 2 различаются")),
            $"{algorithm}: не показано правило сравнения");
    }
});

RunTest("Эксперименты производительности (100, 500, 1000, 2000, 5000 слов)", () =>
{
    var report = LexicographicSortingAlgorithms.RunBenchmarks(
        new[] { 100, 500, 1000, 2000, 5000 },
        LexicographicAlgorithm.QuickSortHoare,
        LexicographicAlgorithm.AbcRadixSort,
        repeats: 3);

    Assert(report.Rows.Count == 5, "Ожидалось 5 строк замеров");
    foreach (var row in report.Rows)
    {
        Assert(row.ResultsMatch, $"Результаты не совпали на {row.WordCount} словах");
        Assert(row.Time1Ms >= 0 && row.Time2Ms >= 0, "Некорректное время");
        Console.WriteLine($"    {row.WordCount,5} слов | Quick: {row.Time1Ms,6:F3} мс | ABC/Radix: {row.Time2Ms,6:F3} мс");
    }
});

Console.WriteLine($"\nИтого: пройдено {passed}, провалено {failed}.");
return failed == 0 ? 0 : 1;
