namespace SortingLab4.Core.Models;

/// <summary>
/// Поддерживаемые методы внешней сортировки (Задание №2).
/// </summary>
public enum ExternalSortMethod
{
    DirectMerge,
    NaturalMerge,
    MultiwayMerge
}

/// <summary>
/// Тип сравнения ключевого столбца.
/// </summary>
public enum KeyComparisonType
{
    Auto,
    Number,
    Text
}

/// <summary>
/// Загруженная таблица с произвольным количеством столбцов.
/// </summary>
public sealed class TableDocument
{
    public string FileName { get; init; } = string.Empty;
    public bool IsSingleColumn { get; init; }
    public List<string> Headers { get; init; } = new();
    public List<string[]> Rows { get; init; } = new();
    public List<KeyComparisonType> ColumnTypes { get; init; } = new();
    public Dictionary<int, List<string>> FilterOptions { get; init; } = new();
}

/// <summary>
/// Краткий снимок одной записи в файле на диске для визуализации.
/// </summary>
public sealed class FileRecordPreview
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DisplayText => string.IsNullOrEmpty(Label) || Label == Key ? Key : $"{Label}: {Key}";
}

/// <summary>
/// Снимок одного вспомогательного файла на диске.
/// </summary>
public sealed class AuxFileSnapshot
{
    public string Name { get; init; } = string.Empty;
    public List<FileRecordPreview> Records { get; init; } = new();
    public bool IsActiveSource { get; init; }
}

public sealed record ExternalMergeCandidate(string FileName, FileRecordPreview Record);

public sealed record ExternalMergeDecision(
    IReadOnlyList<ExternalMergeCandidate> Candidates,
    ExternalMergeCandidate Chosen,
    int OutputPosition,
    bool HasEqualMinimum);

/// <summary>
/// Один шаг визуализации внешней сортировки.
/// </summary>
public sealed class ExternalSortStep
{
    public int StepNumber { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public string OperationLabel { get; init; } = string.Empty;
    public int PassNumber { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public List<FileRecordPreview> MainFileRecords { get; init; } = new();
    public List<AuxFileSnapshot> AuxFiles { get; init; } = new();
    public ExternalMergeDecision? MergeDecision { get; init; }
}

/// <summary>
/// Статистика внешней сортировки.
/// </summary>
public sealed class ExternalSortStats
{
    public int Passes { get; set; }
    public int Comparisons { get; set; }
    public int Moves { get; set; }
    public int TotalRecords { get; set; }
    public int Ways { get; set; } = 2;
}

/// <summary>
/// Результат работы внешней сортировки.
/// </summary>
public sealed class ExternalSortResult
{
    public ExternalSortMethod Method { get; init; }
    public string MethodName { get; init; } = string.Empty;
    public List<string> Headers { get; init; } = new();
    public int KeyColumn { get; init; }
    public KeyComparisonType ResolvedKeyType { get; init; }
    public List<string[]> SortedRows { get; init; } = new();
    public ExternalSortStats Stats { get; init; } = new();
    public List<ExternalSortStep> Steps { get; init; } = new();
    public string? SavedFilePath { get; set; }
}
