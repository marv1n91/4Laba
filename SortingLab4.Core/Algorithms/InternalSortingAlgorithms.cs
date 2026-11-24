using System.Globalization;
using SortingLab4.Core.Models;

namespace SortingLab4.Core.Algorithms;

/// <summary>
/// Реализация четырех алгоритмов внутренней сортировки:
/// Bubble Sort, Insertion Sort, Merge Sort и Quick Sort (схема Хоара).
/// </summary>
public static class InternalSortingAlgorithms
{
    public static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 1e-9)
            return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static InternalSortResult Run(InternalSortAlgorithm algorithm, IReadOnlyList<double> input)
    {
        return algorithm switch
        {
            InternalSortAlgorithm.Bubble => BubbleSort(input),
            InternalSortAlgorithm.Insertion => InsertionSort(input),
            InternalSortAlgorithm.Merge => MergeSort(input),
            InternalSortAlgorithm.QuickHoare => QuickSortHoare(input),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Неизвестный алгоритм сортировки.")
        };
    }

    // 1. Bubble Sort (сортировка пузырьком)
    public static InternalSortResult BubbleSort(IReadOnlyList<double> input)
    {
        var arr = new double[input.Count];
        for (int k = 0; k < input.Count; k++) arr[k] = input[k];

        var steps = new List<InternalSortStep>();
        var stats = new InternalSortStats();
        var sortedIndices = new List<int>();
        int n = arr.Length;

        void AddStep(string type, string label, string explanation,
            List<int>? comparing = null, List<int>? swapping = null, int passNum = 0)
        {
            steps.Add(new InternalSortStep
            {
                StepNumber = steps.Count + 1,
                OperationType = type,
                OperationLabel = label,
                Explanation = explanation,
                ArraySnapshot = (double[])arr.Clone(),
                ComparingIndices = comparing ?? new List<int>(),
                SwappingIndices = swapping ?? new List<int>(),
                SortedIndices = new List<int>(sortedIndices),
                PassNumber = passNum
            });
        }

        if (n == 0)
        {
            AddStep("done", "Готово", "Массив пустой, сортировать нечего.");
        }
        else if (n == 1)
        {
            sortedIndices.Add(0);
            AddStep("done", "Готово", $"В массиве всего одно число ({FormatNumber(arr[0])}), оно уже на своем месте.");
        }
        else
        {
            AddStep("start", "Старт",
                "Начинаем сортировку пузырьком. Идем слева направо, сравниваем пары соседних чисел и двигаем большие числа вправо.");

            bool earlyDone = false;
            for (int i = 0; i < n - 1; i++)
            {
                bool swappedInPass = false;
                int passNum = i + 1;

                for (int j = 0; j < n - 1 - i; j++)
                {
                    stats.Comparisons++;
                    double leftVal = arr[j];
                    double rightVal = arr[j + 1];

                    if (leftVal > rightVal)
                    {
                        AddStep("compare", "Сравнение",
                            $"Проход {passNum}: сравниваем соседние числа {FormatNumber(leftVal)} и {FormatNumber(rightVal)}. " +
                            $"{FormatNumber(leftVal)} больше {FormatNumber(rightVal)}, поэтому поменяем их местами.",
                            comparing: new List<int> { j, j + 1 }, passNum: passNum);

                        (arr[j], arr[j + 1]) = (arr[j + 1], arr[j]);
                        stats.Swaps++;
                        swappedInPass = true;

                        AddStep("swap", "Перестановка",
                            $"Поменяли местами {FormatNumber(leftVal)} и {FormatNumber(rightVal)}. Большее число {FormatNumber(leftVal)} сдвинулось вправо.",
                            swapping: new List<int> { j, j + 1 }, passNum: passNum);
                    }
                    else
                    {
                        AddStep("compare", "Сравнение",
                            $"Проход {passNum}: сравниваем соседние числа {FormatNumber(leftVal)} и {FormatNumber(rightVal)}. " +
                            $"Порядок правильный ({FormatNumber(leftVal)} <= {FormatNumber(rightVal)}), оставляем как есть.",
                            comparing: new List<int> { j, j + 1 }, passNum: passNum);
                    }
                }

                int placedIdx = n - 1 - i;
                sortedIndices.Add(placedIdx);
                AddStep("pass_end", "Конец прохода",
                    $"Проход {passNum} закончен. Число {FormatNumber(arr[placedIdx])} встало на свое место в конце массива.",
                    passNum: passNum);

                if (!swappedInPass)
                {
                    for (int k = 0; k < placedIdx; k++)
                    {
                        if (!sortedIndices.Contains(k)) sortedIndices.Add(k);
                    }
                    AddStep("done", "Готово",
                        "За этот проход ни одной перестановки не было, значит массив уже отсортирован.",
                        passNum: passNum);
                    earlyDone = true;
                    break;
                }
            }

            if (!earlyDone)
            {
                if (!sortedIndices.Contains(0)) sortedIndices.Add(0);
                AddStep("done", "Готово",
                    "Сортировка пузырьком завершена. Все числа стоят по возрастанию.",
                    passNum: n - 1);
            }
        }

        stats.StepsCount = steps.Count;
        var initialCopy = new double[input.Count];
        for (int k = 0; k < input.Count; k++) initialCopy[k] = input[k];

        return new InternalSortResult
        {
            Algorithm = InternalSortAlgorithm.Bubble,
            AlgorithmName = "Bubble Sort (Сортировка пузырьком)",
            InitialArray = initialCopy,
            SortedArray = arr,
            Stats = stats,
            Steps = steps
        };
    }

    // 2. Insertion Sort (сортировка простыми вставками)
    public static InternalSortResult InsertionSort(IReadOnlyList<double> input)
    {
        var arr = new double[input.Count];
        for (int k = 0; k < input.Count; k++) arr[k] = input[k];

        var steps = new List<InternalSortStep>();
        var stats = new InternalSortStats();
        int n = arr.Length;

        void AddStep(string type, string label, string explanation, int sortedEnd,
            int? currentIdx = null, double? keyVal = null, List<int>? comparing = null,
            int? shiftedIdx = null, int? insertedIdx = null)
        {
            var sortedList = new List<int>();
            for (int idx = 0; idx < sortedEnd; idx++) sortedList.Add(idx);

            steps.Add(new InternalSortStep
            {
                StepNumber = steps.Count + 1,
                OperationType = type,
                OperationLabel = label,
                Explanation = explanation,
                ArraySnapshot = (double[])arr.Clone(),
                SortedIndices = sortedList,
                CurrentIndex = currentIdx,
                KeyValue = keyVal,
                ComparingIndices = comparing ?? new List<int>(),
                ShiftedIndex = shiftedIdx,
                InsertedIndex = insertedIdx
            });
        }

        if (n == 0)
        {
            AddStep("done", "Готово", "Массив пустой, сортировать нечего.", 0);
        }
        else if (n == 1)
        {
            AddStep("done", "Готово", $"В массиве всего одно число ({FormatNumber(arr[0])}), оно уже отсортировано.", 1);
        }
        else
        {
            AddStep("start", "Старт",
                $"Начинаем сортировку вставками. Первое число {FormatNumber(arr[0])} считаем уже упорядоченным. Остальные будем брать по одному и вставлять на нужное место слева.", 1);

            for (int i = 1; i < n; i++)
            {
                double key = arr[i];
                AddStep("select", "Выбор числа",
                    $"Берем число {FormatNumber(key)} (позиция {i + 1}) и ищем для него место среди уже отсортированных чисел слева.",
                    sortedEnd: i, currentIdx: i, keyVal: key);

                int j = i - 1;
                while (j >= 0)
                {
                    stats.Comparisons++;
                    if (arr[j] > key)
                    {
                        AddStep("compare", "Сравнение",
                            $"Сравниваем {FormatNumber(key)} и {FormatNumber(arr[j])}. Число {FormatNumber(arr[j])} больше, поэтому сдвинем его вправо.",
                            sortedEnd: i, currentIdx: j + 1, keyVal: key, comparing: new List<int> { j });

                        arr[j + 1] = arr[j];
                        stats.Shifts++;

                        AddStep("shift", "Сдвиг вправо",
                            $"Сдвинули {FormatNumber(arr[j])} на одну позицию вправо, освобождая место для {FormatNumber(key)}.",
                            sortedEnd: i, currentIdx: j, keyVal: key, shiftedIdx: j + 1);
                        j--;
                    }
                    else
                    {
                        AddStep("compare", "Сравнение",
                            $"Сравниваем {FormatNumber(key)} и {FormatNumber(arr[j])}. Число {FormatNumber(arr[j])} не больше {FormatNumber(key)}, значит место найдено.",
                            sortedEnd: i, currentIdx: j + 1, keyVal: key, comparing: new List<int> { j });
                        break;
                    }
                }

                arr[j + 1] = key;
                AddStep("insert", "Вставка",
                    $"Вставляем {FormatNumber(key)} на позицию {j + 2}. Теперь в отсортированной части {i + 1} чисел.",
                    sortedEnd: i + 1, keyVal: key, insertedIdx: j + 1);
            }

            AddStep("done", "Готово",
                "Сортировка вставками завершена. Все числа стоят по возрастанию.", n);
        }

        stats.StepsCount = steps.Count;
        var initialCopy = new double[input.Count];
        for (int k = 0; k < input.Count; k++) initialCopy[k] = input[k];

        return new InternalSortResult
        {
            Algorithm = InternalSortAlgorithm.Insertion,
            AlgorithmName = "Insertion Sort (Сортировка вставками)",
            InitialArray = initialCopy,
            SortedArray = arr,
            Stats = stats,
            Steps = steps
        };
    }

    // 3. Merge Sort (сортировка слиянием с деревом разделения и слияния)
    public static InternalSortResult MergeSort(IReadOnlyList<double> input)
    {
        var arr = new double[input.Count];
        for (int k = 0; k < input.Count; k++) arr[k] = input[k];

        var steps = new List<InternalSortStep>();
        var stats = new InternalSortStats();
        var nodes = new List<MergeTreeNode>();
        int n = arr.Length;

        List<MergeTreeNode> SnapshotNodes()
        {
            var copy = new List<MergeTreeNode>(nodes.Count);
            foreach (var nd in nodes) copy.Add(nd.Clone());
            return copy;
        }

        MergeTreeNode CreateNode(int? parentId, string side, int depth, int leftIdx, int rightIdx, List<double> vals, string state = "active")
        {
            var nd = new MergeTreeNode
            {
                Id = nodes.Count,
                ParentId = parentId,
                Side = side,
                Depth = depth,
                LeftIndex = leftIdx,
                RightIndex = rightIdx,
                Values = new List<double>(vals),
                State = state
            };
            nodes.Add(nd);
            return nd;
        }

        void AddStep(string type, string label, string explanation, int? activeNodeId = null, MergeContext? mergeCtx = null)
        {
            steps.Add(new InternalSortStep
            {
                StepNumber = steps.Count + 1,
                OperationType = type,
                OperationLabel = label,
                Explanation = explanation,
                ArraySnapshot = (double[])arr.Clone(),
                ActiveNodeId = activeNodeId,
                TreeNodes = SnapshotNodes(),
                MergeContext = mergeCtx
            });
        }

        static string JoinVals(IEnumerable<double> vals) => string.Join(", ", vals.Select(FormatNumber));

        if (n == 0)
        {
            AddStep("done", "Готово", "Массив пустой, сортировать нечего.");
        }
        else
        {
            var root = CreateNode(null, "root", 0, 0, n - 1, new List<double>(arr), "active");
            if (n == 1)
            {
                root.State = "merged";
                AddStep("done", "Готово",
                    $"В массиве всего одно число ({FormatNumber(arr[0])}), делить его не нужно.",
                    root.Id);
            }
            else
            {
                AddStep("start", "Старт",
                    "Начинаем сортировку слиянием. Делим массив пополам, пока не останутся отдельные числа, а затем собираем их обратно в правильном порядке.", root.Id);

                List<double> RecursiveSort(MergeTreeNode node)
                {
                    if (node.Values.Count <= 1)
                    {
                        node.State = "single";
                        AddStep("single", "Одно число",
                            $"Подмассив [{FormatNumber(node.Values[0])}] состоит из одного числа. Дальше делить нечего, он уже отсортирован.",
                            node.Id);
                        return new List<double>(node.Values);
                    }

                    int mid = node.Values.Count / 2;
                    var leftVals = node.Values.GetRange(0, mid);
                    var rightVals = node.Values.GetRange(mid, node.Values.Count - mid);

                    node.State = "split";
                    stats.Splits++;
                    var leftNode = CreateNode(node.Id, "left", node.Depth + 1, node.LeftIndex, node.LeftIndex + mid - 1, leftVals, "active");
                    var rightNode = CreateNode(node.Id, "right", node.Depth + 1, node.LeftIndex + mid, node.RightIndex, rightVals, "active");

                    AddStep("split", "Разделение",
                        $"Делим подмассив на две части: левая [{JoinVals(leftVals)}] и правая [{JoinVals(rightVals)}].",
                        node.Id);

                    var sortedLeft = RecursiveSort(leftNode);
                    var sortedRight = RecursiveSort(rightNode);

                    node.State = "merging";
                    stats.Merges++;
                    var merged = new List<double>(sortedLeft.Count + sortedRight.Count);
                    int i = 0, j = 0;

                    AddStep("merge_start", "Слияние",
                        $"Соединяем две отсортированные части: [{JoinVals(sortedLeft)}] и [{JoinVals(sortedRight)}]. Сравниваем первые числа и берем меньшее.",
                        node.Id,
                        new MergeContext
                        {
                            NodeId = node.Id,
                            LeftValues = new List<double>(sortedLeft),
                            RightValues = new List<double>(sortedRight),
                            ResultValues = new List<double>(),
                            LeftPtr = 0,
                            RightPtr = 0
                        });

                    while (i < sortedLeft.Count && j < sortedRight.Count)
                    {
                        stats.Comparisons++;
                        double lv = sortedLeft[i];
                        double rv = sortedRight[j];

                        AddStep("merge_compare", "Сравнение",
                            $"Сравниваем {FormatNumber(lv)} (слева) и {FormatNumber(rv)} (справа). " +
                            (lv <= rv
                                ? $"Меньше или равно {FormatNumber(lv)}, берем его."
                                : $"Меньше {FormatNumber(rv)}, берем его."),
                            node.Id,
                            new MergeContext
                            {
                                NodeId = node.Id,
                                LeftValues = new List<double>(sortedLeft),
                                RightValues = new List<double>(sortedRight),
                                ResultValues = new List<double>(merged),
                                LeftPtr = i,
                                RightPtr = j,
                                ComparingLeft = i,
                                ComparingRight = j
                            });

                        double chosen;
                        if (lv <= rv)
                        {
                            chosen = lv;
                            i++;
                        }
                        else
                        {
                            chosen = rv;
                            j++;
                        }

                        merged.Add(chosen);
                        arr[node.LeftIndex + merged.Count - 1] = chosen;

                        var updatedNodeVals = new List<double>(merged);
                        for (int a = i; a < sortedLeft.Count; a++) updatedNodeVals.Add(sortedLeft[a]);
                        for (int b = j; b < sortedRight.Count; b++) updatedNodeVals.Add(sortedRight[b]);
                        node.Values = updatedNodeVals;

                        AddStep("merge_place", "Перенос",
                            $"Переносим число {FormatNumber(chosen)} в общий результат.",
                            node.Id,
                            new MergeContext
                            {
                                NodeId = node.Id,
                                LeftValues = new List<double>(sortedLeft),
                                RightValues = new List<double>(sortedRight),
                                ResultValues = new List<double>(merged),
                                LeftPtr = i,
                                RightPtr = j
                            });
                    }

                    while (i < sortedLeft.Count)
                    {
                        double chosen = sortedLeft[i++];
                        merged.Add(chosen);
                        arr[node.LeftIndex + merged.Count - 1] = chosen;
                        var updatedNodeVals = new List<double>(merged);
                        for (int a = i; a < sortedLeft.Count; a++) updatedNodeVals.Add(sortedLeft[a]);
                        node.Values = updatedNodeVals;

                        AddStep("merge_place", "Остаток",
                            $"Правая часть закончилась, дописываем {FormatNumber(chosen)} из левой части.",
                            node.Id,
                            new MergeContext
                            {
                                NodeId = node.Id,
                                LeftValues = new List<double>(sortedLeft),
                                RightValues = new List<double>(sortedRight),
                                ResultValues = new List<double>(merged),
                                LeftPtr = i,
                                RightPtr = j
                            });
                    }

                    while (j < sortedRight.Count)
                    {
                        double chosen = sortedRight[j++];
                        merged.Add(chosen);
                        arr[node.LeftIndex + merged.Count - 1] = chosen;
                        var updatedNodeVals = new List<double>(merged);
                        for (int b = j; b < sortedRight.Count; b++) updatedNodeVals.Add(sortedRight[b]);
                        node.Values = updatedNodeVals;

                        AddStep("merge_place", "Остаток",
                            $"Левая часть закончилась, дописываем {FormatNumber(chosen)} из правой части.",
                            node.Id,
                            new MergeContext
                            {
                                NodeId = node.Id,
                                LeftValues = new List<double>(sortedLeft),
                                RightValues = new List<double>(sortedRight),
                                ResultValues = new List<double>(merged),
                                LeftPtr = i,
                                RightPtr = j
                            });
                    }

                    node.Values = new List<double>(merged);
                    node.State = "merged";
                    AddStep("merge_end", "Слияние готово",
                        $"Подмассив собран и отсортирован: [{JoinVals(merged)}].",
                        node.Id);
                    return merged;
                }

                RecursiveSort(root);
                AddStep("done", "Готово",
                    "Сортировка слиянием завершена. Все подмассивы объединены в отсортированный массив.",
                    root.Id);
            }
        }

        stats.StepsCount = steps.Count;
        var initialCopy = new double[input.Count];
        for (int k = 0; k < input.Count; k++) initialCopy[k] = input[k];

        return new InternalSortResult
        {
            Algorithm = InternalSortAlgorithm.Merge,
            AlgorithmName = "Merge Sort (Сортировка слиянием)",
            InitialArray = initialCopy,
            SortedArray = arr,
            Stats = stats,
            Steps = steps
        };
    }

    // 4. Quick Sort Hoare (быстрая сортировка Хоара)
    public static InternalSortResult QuickSortHoare(IReadOnlyList<double> input)
    {
        var arr = new double[input.Count];
        for (int k = 0; k < input.Count; k++) arr[k] = input[k];

        var steps = new List<InternalSortStep>();
        var stats = new InternalSortStats();
        var sortedSet = new HashSet<int>();
        int n = arr.Length;

        void AddStep(string type, string label, string explanation,
            int? rangeLeft = null, int? rangeRight = null,
            double? pivotVal = null, int? pivotIdx = null,
            int? ptrI = null, int? ptrJ = null,
            List<int>? comparing = null, List<int>? swapping = null,
            List<int>? leftPart = null, List<int>? rightPart = null)
        {
            steps.Add(new InternalSortStep
            {
                StepNumber = steps.Count + 1,
                OperationType = type,
                OperationLabel = label,
                Explanation = explanation,
                ArraySnapshot = (double[])arr.Clone(),
                RangeLeft = rangeLeft,
                RangeRight = rangeRight,
                PivotValue = pivotVal,
                PivotIndex = pivotIdx,
                PointerI = ptrI,
                PointerJ = ptrJ,
                ComparingIndices = comparing ?? new List<int>(),
                SwappingIndices = swapping ?? new List<int>(),
                LeftPartIndices = leftPart ?? new List<int>(),
                RightPartIndices = rightPart ?? new List<int>(),
                SortedIndices = new List<int>(sortedSet)
            });
        }

        if (n == 0)
        {
            AddStep("done", "Готово", "Массив пустой, сортировать нечего.");
        }
        else if (n == 1)
        {
            sortedSet.Add(0);
            AddStep("done", "Готово", $"В массиве всего одно число ({FormatNumber(arr[0])}), оно уже на своем месте.");
        }
        else
        {
            AddStep("start", "Старт",
                "Начинаем быструю сортировку Хоара. Выбираем опорное число и двумя указателями i и j распределяем меньшие числа влево, а большие вправо.",
                rangeLeft: 0, rangeRight: n - 1);

            void QuickSortRecursive(int left, int right)
            {
                if (left >= right)
                {
                    if (left >= 0 && left < n) sortedSet.Add(left);
                    return;
                }

                int pivotIdx = (left + right) / 2;
                double pivotVal = arr[pivotIdx];
                int i = left;
                int j = right;

                AddStep("pivot", "Опорное число",
                    $"Берем участок с позиции {left + 1} по {right + 1}. Опорное число: {FormatNumber(pivotVal)}. " +
                    $"Ставим указатель i на позицию {i + 1}, а j на позицию {j + 1}.",
                    rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx, ptrI: i, ptrJ: j);

                while (i <= j)
                {
                    while (arr[i] < pivotVal)
                    {
                        stats.Comparisons++;
                        AddStep("compare", "Проверка i",
                            $"Указатель i смотрит на {FormatNumber(arr[i])}: оно меньше опорного {FormatNumber(pivotVal)}, сдвигаем i вправо.",
                            rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx, ptrI: i, ptrJ: j,
                            comparing: new List<int> { i });
                        i++;
                    }

                    stats.Comparisons++;
                    AddStep("pointer", "Стоп i",
                        $"Указатель i остановился на {FormatNumber(arr[i])} (оно >= {FormatNumber(pivotVal)}).",
                        rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx, ptrI: i, ptrJ: j,
                        comparing: new List<int> { i });

                    while (arr[j] > pivotVal)
                    {
                        stats.Comparisons++;
                        AddStep("compare", "Проверка j",
                            $"Указатель j смотрит на {FormatNumber(arr[j])}: оно больше опорного {FormatNumber(pivotVal)}, сдвигаем j влево.",
                            rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx, ptrI: i, ptrJ: j,
                            comparing: new List<int> { j });
                        j--;
                    }

                    stats.Comparisons++;
                    AddStep("pointer", "Стоп j",
                        $"Указатель j остановился на {FormatNumber(arr[j])} (оно <= {FormatNumber(pivotVal)}).",
                        rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx, ptrI: i, ptrJ: j,
                        comparing: new List<int> { j });

                    if (i <= j)
                    {
                        if (i != j)
                        {
                            double vi = arr[i], vj = arr[j];
                            (arr[i], arr[j]) = (arr[j], arr[i]);
                            stats.Swaps++;

                            if (pivotIdx == i) pivotIdx = j;
                            else if (pivotIdx == j) pivotIdx = i;

                            AddStep("swap", "Перестановка",
                                $"Меняем местами {FormatNumber(vi)} и {FormatNumber(vj)}, чтобы меньшее число ушло влево, а большее вправо.",
                                rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx, ptrI: i, ptrJ: j,
                                swapping: new List<int> { i, j });
                        }
                        else
                        {
                            AddStep("pointer", "Встреча i и j",
                                $"Указатели встретились на позиции {i + 1} (число {FormatNumber(arr[i])}). Двигаем их дальше.",
                                rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx, ptrI: i, ptrJ: j);
                        }
                        i++;
                        j--;
                    }
                }

                stats.Splits++;
                var leftPart = new List<int>();
                for (int p = left; p <= j; p++) leftPart.Add(p);
                var rightPart = new List<int>();
                for (int p = i; p <= right; p++) rightPart.Add(p);
                for (int mid = j + 1; mid < i; mid++) sortedSet.Add(mid);

                AddStep("partition_end", "Разделение готово",
                    $"Разделили участок вокруг {FormatNumber(pivotVal)}: слева числа <= {FormatNumber(pivotVal)}, справа >= {FormatNumber(pivotVal)}. " +
                    "Теперь сортируем левую и правую части отдельно.",
                    rangeLeft: left, rangeRight: right, pivotVal: pivotVal, pivotIdx: pivotIdx,
                    ptrI: i < n ? i : null, ptrJ: j >= 0 ? j : null,
                    leftPart: leftPart, rightPart: rightPart);

                if (left < j) QuickSortRecursive(left, j);
                else if (left == j) sortedSet.Add(left);

                if (i < right) QuickSortRecursive(i, right);
                else if (i == right) sortedSet.Add(right);
            }

            QuickSortRecursive(0, n - 1);
            for (int idx = 0; idx < n; idx++) sortedSet.Add(idx);

            AddStep("done", "Готово",
                "Быстрая сортировка Хоара завершена. Все числа стоят по возрастанию.");
        }

        stats.StepsCount = steps.Count;
        var initialCopy = new double[input.Count];
        for (int k = 0; k < input.Count; k++) initialCopy[k] = input[k];

        return new InternalSortResult
        {
            Algorithm = InternalSortAlgorithm.QuickHoare,
            AlgorithmName = "Quick Sort (Быстрая сортировка Хоара)",
            InitialArray = initialCopy,
            SortedArray = arr,
            Stats = stats,
            Steps = steps
        };
    }
}
