namespace SortingLab4.Core.Models;

/// <summary>
/// Алгоритмы лексикографической сортировки слов (Задание №3).
/// </summary>
public enum LexicographicAlgorithm
{
    QuickSortHoare,
    MergeSort,
    AbcRadixSort
}

public sealed record LexicographicTraceStep(string Title, string Explanation, IReadOnlyList<string> Items);

/// <summary>
/// Строка таблицы частот слов.
/// </summary>
public sealed class WordFrequencyEntry
{
    public string Word { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>
/// Результат лексикографической сортировки и подсчёта частоты слов.
/// </summary>
public sealed class LexicographicSortResult
{
    public LexicographicAlgorithm Algorithm { get; init; }
    public string AlgorithmName { get; init; } = string.Empty;
    public int TotalWords { get; init; }
    public int UniqueWords { get; init; }
    public string[] SortedWords { get; init; } = Array.Empty<string>();
    public List<WordFrequencyEntry> Frequencies { get; init; } = new();
    public double ElapsedMs { get; init; }
    public string Explanation { get; init; } = string.Empty;
}

/// <summary>
/// Результат одного эксперимента производительности для заданного размера текста.
/// </summary>
public sealed class BenchmarkRow
{
    public int WordCount { get; init; }
    public string Algorithm1Name { get; init; } = string.Empty;
    public double Time1Ms { get; init; }
    public string Algorithm2Name { get; init; } = string.Empty;
    public double Time2Ms { get; init; }
    public bool ResultsMatch { get; init; }
    public string MatchLabel => ResultsMatch ? "Да" : "Нет";
    public string Time1Formatted => $"{Time1Ms:F3} мс";
    public string Time2Formatted => $"{Time2Ms:F3} мс";
}

/// <summary>
/// Итоги серии экспериментов производительности.
/// </summary>
public sealed class BenchmarkReport
{
    public string Algorithm1Name { get; init; } = string.Empty;
    public string Algorithm2Name { get; init; } = string.Empty;
    public int Repeats { get; init; }
    public List<BenchmarkRow> Rows { get; init; } = new();
}
