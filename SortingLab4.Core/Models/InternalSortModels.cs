namespace SortingLab4.Core.Models;

/// <summary>
/// Поддерживаемые алгоритмы внутренней сортировки (Задание №1).
/// </summary>
public enum InternalSortAlgorithm
{
    Bubble,
    Insertion,
    Merge,
    QuickHoare
}

/// <summary>
/// Узел дерева рекурсивного разделения и слияния для Merge Sort.
/// </summary>
public sealed class MergeTreeNode
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Side { get; init; } = "root"; // "root", "left", "right"
    public int Depth { get; init; }
    public int LeftIndex { get; init; }
    public int RightIndex { get; init; }
    public List<double> Values { get; set; } = new();
    public string State { get; set; } = "active"; // "active", "split", "single", "merging", "merged"

    public MergeTreeNode Clone() => new()
    {
        Id = Id,
        ParentId = ParentId,
        Side = Side,
        Depth = Depth,
        LeftIndex = LeftIndex,
        RightIndex = RightIndex,
        Values = new List<double>(Values),
        State = State
    };
}

/// <summary>
/// Состояние активного слияния двух отсортированных подмассивов в Merge Sort.
/// </summary>
public sealed class MergeContext
{
    public int NodeId { get; init; }
    public List<double> LeftValues { get; init; } = new();
    public List<double> RightValues { get; init; } = new();
    public List<double> ResultValues { get; init; } = new();
    public int LeftPtr { get; init; }
    public int RightPtr { get; init; }
    public int? ComparingLeft { get; init; }
    public int? ComparingRight { get; init; }
}

/// <summary>
/// Один шаг пошаговой визуализации алгоритма внутренней сортировки.
/// </summary>
public sealed class InternalSortStep
{
    public int StepNumber { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public string OperationLabel { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public double[] ArraySnapshot { get; init; } = Array.Empty<double>();

    // Общие подсветки
    public List<int> ComparingIndices { get; init; } = new();
    public List<int> SwappingIndices { get; init; } = new();
    public List<int> SortedIndices { get; init; } = new();

    // Bubble Sort
    public int PassNumber { get; init; }

    // Insertion Sort
    public int? CurrentIndex { get; init; }
    public double? KeyValue { get; init; }
    public int? ShiftedIndex { get; init; }
    public int? InsertedIndex { get; init; }

    // Quick Sort (Hoare)
    public int? RangeLeft { get; init; }
    public int? RangeRight { get; init; }
    public double? PivotValue { get; init; }
    public int? PivotIndex { get; init; }
    public int? PointerI { get; init; }
    public int? PointerJ { get; init; }
    public List<int> LeftPartIndices { get; init; } = new();
    public List<int> RightPartIndices { get; init; } = new();

    // Merge Sort
    public int? ActiveNodeId { get; init; }
    public List<MergeTreeNode>? TreeNodes { get; init; }
    public MergeContext? MergeContext { get; init; }
}

/// <summary>
/// Статистика выполнения алгоритма внутренней сортировки.
/// </summary>
public sealed class InternalSortStats
{
    public int Comparisons { get; set; }
    public int Swaps { get; set; }
    public int Shifts { get; set; }
    public int Splits { get; set; }
    public int Merges { get; set; }
    public int StepsCount { get; set; }
}

/// <summary>
/// Итоговый результат работы алгоритма внутренней сортировки.
/// </summary>
public sealed class InternalSortResult
{
    public InternalSortAlgorithm Algorithm { get; init; }
    public string AlgorithmName { get; init; } = string.Empty;
    public double[] InitialArray { get; init; } = Array.Empty<double>();
    public double[] SortedArray { get; init; } = Array.Empty<double>();
    public InternalSortStats Stats { get; init; } = new();
    public List<InternalSortStep> Steps { get; init; } = new();
}
