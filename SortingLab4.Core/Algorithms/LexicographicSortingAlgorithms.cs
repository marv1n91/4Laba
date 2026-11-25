using System.Diagnostics;
using System.Text.RegularExpressions;
using SortingLab4.Core.Models;

namespace SortingLab4.Core.Algorithms;

/// <summary>
/// Лексикографическая сортировка слов, подсчёт частоты встречаемости и
/// эксперименты производительности (Задание №3).
/// Все алгоритмы реализованы самостоятельно без встроенных функций сортировки.
/// </summary>
public static class LexicographicSortingAlgorithms
{
    public const int TraceWordLimit = 12;
    private const int TraceStepLimit = 400;
    // Единый алфавит: апостроф, дефис, цифры, латиница a-z, кириллица а-я (с «ё» после «е»)
    private const string Alphabet = "'-0123456789abcdefghijklmnopqrstuvwxyzабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    private static readonly Dictionary<char, int> CharBucket = BuildCharBucket();
    private static readonly int AlphabetBucketsCount = Alphabet.Length + 2; // 0 = конец слова, 1..len = алфавит, last = прочие

    private static readonly Regex WordRegex = new(
        @"[A-Za-zА-Яа-яЁё0-9]+(?:[-'][A-Za-zА-Яа-яЁё0-9]+)*",
        RegexOptions.Compiled);

    private static readonly string[] EnglishVocabulary =
    {
        "algorithm", "analysis", "apple", "array", "banana", "binary", "bucket",
        "character", "class", "code", "column", "compare", "computer", "count",
        "data", "database", "design", "divide", "element", "engine", "example",
        "experiment", "fast", "file", "filter", "frequency", "function", "graph",
        "group", "hardware", "house", "index", "input", "insert", "interface",
        "iteration", "key", "language", "length", "letter", "library", "linear",
        "list", "logic", "loop", "machine", "memory", "merge", "method", "model",
        "module", "network", "node", "number", "object", "operation", "orange",
        "order", "output", "package", "partition", "pattern", "performance",
        "pivot", "pointer", "practice", "prefix", "process", "program", "project",
        "queue", "quick", "radix", "random", "record", "recursion", "result",
        "run", "sample", "science", "search", "sequence", "server", "shift",
        "simple", "size", "software", "sort", "sorting", "speed", "split",
        "stack", "step", "storage", "stream", "string", "structure", "student",
        "swap", "symbol", "system", "table", "target", "task", "test", "text",
        "time", "token", "tree", "type", "unique", "update", "user", "value",
        "variable", "vector", "version", "visual", "window", "word", "work"
    };

    private static Dictionary<char, int> BuildCharBucket()
    {
        var map = new Dictionary<char, int>(Alphabet.Length * 2);
        for (int i = 0; i < Alphabet.Length; i++)
        {
            char lower = Alphabet[i];
            char upper = char.ToUpperInvariant(lower);
            map[lower] = i + 1;
            map[upper] = i + 1;
        }
        return map;
    }

    public static string GetAlgorithmName(LexicographicAlgorithm algorithm) => algorithm switch
    {
        LexicographicAlgorithm.QuickSortHoare => "Quick Sort (Хоара)",
        LexicographicAlgorithm.MergeSort => "Merge Sort (Слиянием)",
        LexicographicAlgorithm.AbcRadixSort => "ABC / MSD Radix Sort",
        _ => algorithm.ToString()
    };

    /// <summary>
    /// Разбивает текст на слова и убирает знаки препинания.
    /// </summary>
    public static string[] ExtractWords(string? text, bool toLowerCase = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        var matches = WordRegex.Matches(text);
        var result = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            result[i] = toLowerCase ? matches[i].Value.ToLowerInvariant() : matches[i].Value;
        }
        return result;
    }

    /// <summary>
    /// Лексикографическое сравнение двух слов в едином алфавитном порядке.
    /// </summary>
    public static int CompareWords(string a, string b) => CompareWords(a, b, out _);

    private static int CompareWords(string a, string b, out int decisionIndex)
    {
        int limit = Math.Min(a.Length, b.Length);
        decisionIndex = limit;
        if (ReferenceEquals(a, b) || a == b) return 0;

        for (int i = 0; i < limit; i++)
        {
            char ca = a[i];
            char cb = b[i];
            if (ca == cb) continue;
            decisionIndex = i;

            int ra = CharBucket.TryGetValue(ca, out int ba) ? ba : AlphabetBucketsCount + char.ToLowerInvariant(ca);
            int rb = CharBucket.TryGetValue(cb, out int bb) ? bb : AlphabetBucketsCount + char.ToLowerInvariant(cb);
            if (ra != rb) return ra < rb ? -1 : 1;
            return ca < cb ? -1 : 1;
        }

        if (a.Length < b.Length) return -1;
        if (a.Length > b.Length) return 1;
        return 0;
    }

    public static IReadOnlyList<LexicographicTraceStep> TraceWords(LexicographicAlgorithm algorithm, string[] words)
    {
        string[] sample = words.Take(TraceWordLimit).ToArray();
        if (sample.Length == 0) return [];
        var trace = new List<LexicographicTraceStep>
        {
            new("Исходные слова", words.Length > sample.Length
                ? $"Разбираем первые {sample.Length} слов из {words.Length}." : "Слова в порядке появления в тексте.", sample)
        };
        var sorted = SortWords(algorithm, sample, trace);
        trace.Add(new("Готово", trace.Count == TraceStepLimit
            ? $"Достигнут предел показа в {TraceStepLimit} шагов. Ниже итог сортировки выбранных слов."
            : "Слова в лексикографическом порядке.", sorted));
        return trace;
    }

    private static int CompareForTrace(string a, string b, List<LexicographicTraceStep>? trace, string title)
    {
        if (trace is not { Count: < TraceStepLimit }) return CompareWords(a, b);
        int result = CompareWords(a, b, out int index);
        string earlier = result < 0 ? a : b;
        string explanation = result == 0 ? "Слова совпадают."
            : index == Math.Min(a.Length, b.Length) ? $"«{earlier}» заканчивается раньше и идёт первым."
            : $"На позиции {index + 1} различаются «{a[index]}» и «{b[index]}». По порядку символов «{earlier}» идёт первым.";
        trace.Add(new(title, explanation, [$"«{a}»", $"«{b}»"]));
        return result;
    }

    public static string[] SortWords(LexicographicAlgorithm algorithm, string[] words)
        => SortWords(algorithm, words, null);

    private static string[] SortWords(LexicographicAlgorithm algorithm, string[] words, List<LexicographicTraceStep>? trace)
    {
        return algorithm switch
        {
            LexicographicAlgorithm.QuickSortHoare => QuickSortWords(words, trace),
            LexicographicAlgorithm.MergeSort => MergeSortWords(words, trace),
            LexicographicAlgorithm.AbcRadixSort => AbcRadixSortWords(words, trace),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
    }

    // =========================================================================
    // АЛГОРИТМ 1А: QUICK SORT (Быстрая сортировка Хоара для слов)
    // =========================================================================
    public static string[] QuickSortWords(string[] words) => QuickSortWords(words, null);

    private static string[] QuickSortWords(string[] words, List<LexicographicTraceStep>? trace)
    {
        var arr = (string[])words.Clone();
        int n = arr.Length;
        if (n <= 1) return arr;

        var stack = new Stack<(int Left, int Right)>();
        stack.Push((0, n - 1));

        while (stack.Count > 0)
        {
            var (left, right) = stack.Pop();
            if (left >= right) continue;

            string pivot = arr[(left + right) / 2];
            if (trace is { Count: < TraceStepLimit })
                trace.Add(new("Опорное слово", $"Берём «{pivot}» из середины участка {left + 1}–{right + 1}.", arr[left..(right + 1)]));
            int i = left;
            int j = right;

            while (i <= j)
            {
                while (CompareForTrace(arr[i], pivot, trace, "Сравнение слева с опорой") < 0) i++;
                while (CompareForTrace(arr[j], pivot, trace, "Сравнение справа с опорой") > 0) j--;
                if (i <= j)
                {
                    if (i != j)
                    {
                        (arr[i], arr[j]) = (arr[j], arr[i]);
                        if (trace is { Count: < TraceStepLimit })
                            trace.Add(new("Обмен", $"Меняем слова на позициях {i + 1} и {j + 1}.", (string[])arr.Clone()));
                    }
                    i++;
                    j--;
                }
            }

            if (j - left > right - i)
            {
                if (left < j) stack.Push((left, j));
                if (i < right) stack.Push((i, right));
            }
            else
            {
                if (i < right) stack.Push((i, right));
                if (left < j) stack.Push((left, j));
            }
        }

        return arr;
    }

    // =========================================================================
    // АЛГОРИТМ 1Б: MERGE SORT (Сортировка слиянием для слов)
    // =========================================================================
    public static string[] MergeSortWords(string[] words) => MergeSortWords(words, null);

    private static string[] MergeSortWords(string[] words, List<LexicographicTraceStep>? trace)
    {
        int n = words.Length;
        if (n <= 1) return (string[])words.Clone();

        var arr = (string[])words.Clone();
        var buffer = new string[n];

        void SortRange(int left, int right)
        {
            if (left >= right) return;
            int mid = (left + right) / 2;
            if (trace is { Count: < TraceStepLimit })
                trace.Add(new("Деление", "Сортируем левую и правую части, затем сливаем их.",
                    [$"Слева · {string.Join(", ", arr[left..(mid + 1)])}", $"Справа · {string.Join(", ", arr[(mid + 1)..(right + 1)])}"]));
            SortRange(left, mid);
            SortRange(mid + 1, right);

            int i = left, j = mid + 1, k = left;
            while (i <= mid && j <= right)
            {
                int order = CompareForTrace(arr[i], arr[j], trace, "Сравнение первых слов двух частей");
                if (order <= 0)
                    buffer[k++] = arr[i++];
                else
                    buffer[k++] = arr[j++];
                if (trace is { Count: < TraceStepLimit })
                    trace.Add(new("Добавление при слиянии", $"Добавляем «{buffer[k - 1]}» в результат слияния.", buffer[left..k]));
            }
            int afterComparisons = k;
            while (i <= mid) buffer[k++] = arr[i++];
            while (j <= right) buffer[k++] = arr[j++];
            if (k > afterComparisons && trace is { Count: < TraceStepLimit })
                trace.Add(new("Остаток части", "Одна часть закончилась. Дописываем оставшиеся слова по порядку.", buffer[afterComparisons..k]));

            for (int p = left; p <= right; p++) arr[p] = buffer[p];
        }

        SortRange(0, n - 1);
        return arr;
    }

    // =========================================================================
    // АЛГОРИТМ 2: ABC-СОРТИРОВКА / MSD RADIX SORT (Поразрядная сортировка слов)
    // =========================================================================
    public static string[] AbcRadixSortWords(string[] words) => AbcRadixSortWords(words, null);

    private static string[] AbcRadixSortWords(string[] words, List<LexicographicTraceStep>? trace)
    {
        if (words.Length <= 1) return (string[])words.Clone();
        var list = new List<string>(words);
        var sorted = MsdRadixSort(list, 0, trace);
        return sorted.ToArray();
    }

    private static List<string> MsdRadixSort(List<string> bucketWords, int depth, List<LexicographicTraceStep>? trace)
    {
        if (bucketWords.Count <= 1) return bucketWords;

        // Если все слова в корзине одинаковые, дальше делить не нужно
        string first = bucketWords[0];
        bool allSame = true;
        for (int i = 1; i < bucketWords.Count; i++)
        {
            if (bucketWords[i] != first)
            {
                allSame = false;
                break;
            }
        }
        if (allSame) return bucketWords;

        var buckets = new List<string>?[AlphabetBucketsCount];
        int otherIdx = AlphabetBucketsCount - 1;

        for (int i = 0; i < bucketWords.Count; i++)
        {
            string w = bucketWords[i];
            int bIdx;
            if (depth >= w.Length)
            {
                bIdx = 0;
            }
            else if (CharBucket.TryGetValue(w[depth], out int mapped))
            {
                bIdx = mapped;
            }
            else
            {
                bIdx = otherIdx;
            }

            buckets[bIdx] ??= new List<string>();
            buckets[bIdx]!.Add(w);
        }

        if (trace is { Count: < TraceStepLimit })
        {
            var groups = new List<string>();
            for (int b = 0; b < buckets.Length; b++)
            {
                if (buckets[b] is not { } group) continue;
                string heading = b == 0 ? "конец слова" : b == otherIdx ? "прочие символы" : $"«{Alphabet[b - 1]}»";
                groups.Add($"{heading} → {string.Join(", ", group)}");
            }
            string prefix = depth == 0 ? string.Empty : $" для слов с началом «{first[..depth]}»";
            trace.Add(new("Распределение по корзинам", $"Смотрим на символ №{depth + 1}{prefix}. Корзину «конец слова» выводим первой.", groups));
        }

        var result = new List<string>(bucketWords.Count);

        // Корзина 0: слова, закончившиеся на текущей глубине
        if (buckets[0] is not null)
            result.AddRange(buckets[0]!);

        // Корзины алфавита 1 .. otherIdx - 1
        for (int b = 1; b < otherIdx; b++)
        {
            var cur = buckets[b];
            if (cur is null || cur.Count == 0) continue;
            if (cur.Count == 1)
                result.Add(cur[0]);
            else
                result.AddRange(MsdRadixSort(cur, depth + 1, trace));
        }

        // Корзина прочих символов
        if (buckets[otherIdx] is not null)
        {
            var other = buckets[otherIdx]!;
            if (other.Count == 1)
                result.Add(other[0]);
            else
                result.AddRange(QuickSortWords(other.ToArray(), trace));
        }

        return result;
    }

    // =========================================================================
    // ПОДСЧЁТ ЧАСТОТЫ ВСТРЕЧАЕМОСТИ ПО ОТСОРТИРОВАННОМУ МАССИВУ
    // =========================================================================
    public static List<WordFrequencyEntry> CountFrequenciesFromSorted(string[] sortedWords)
    {
        var freqs = new List<WordFrequencyEntry>();
        if (sortedWords.Length == 0) return freqs;

        string currentWord = sortedWords[0];
        int count = 1;

        for (int i = 1; i < sortedWords.Length; i++)
        {
            if (sortedWords[i] == currentWord)
            {
                count++;
            }
            else
            {
                freqs.Add(new WordFrequencyEntry { Word = currentWord, Count = count });
                currentWord = sortedWords[i];
                count = 1;
            }
        }

        freqs.Add(new WordFrequencyEntry { Word = currentWord, Count = count });
        return freqs;
    }

    public static LexicographicSortResult ProcessText(string text, LexicographicAlgorithm algorithm, bool toLowerCase = true)
    {
        string algoName = GetAlgorithmName(algorithm);
        var words = ExtractWords(text, toLowerCase);

        if (words.Length == 0)
        {
            return new LexicographicSortResult
            {
                Algorithm = algorithm,
                AlgorithmName = algoName,
                Explanation = "Текст пустой или в нем нет слов."
            };
        }

        var sw = Stopwatch.StartNew();
        var sorted = SortWords(algorithm, words);
        sw.Stop();

        double elapsedMs = sw.Elapsed.TotalMilliseconds;
        var frequencies = CountFrequenciesFromSorted(sorted);

        string expl = algorithm == LexicographicAlgorithm.AbcRadixSort
            ? $"Из текста извлечено {words.Length} слов. ABC-сортировка (MSD Radix Sort) распределила слова по корзинам " +
              $"сначала по первой букве, затем по второй и далее за {elapsedMs:F3} мс. Одинаковые слова встали рядом, " +
              $"после чего за один проход посчитана частота {frequencies.Count} уникальных слов."
            : $"Из текста извлечено {words.Length} слов. Алгоритм «{algoName}» упорядочил их в алфавитном порядке " +
              $"за {elapsedMs:F3} мс. Поскольку все одинаковые слова стоят подряд, мы за один проход подсчитали повторения " +
              $"для {frequencies.Count} уникальных слов.";

        return new LexicographicSortResult
        {
            Algorithm = algorithm,
            AlgorithmName = algoName,
            TotalWords = words.Length,
            UniqueWords = frequencies.Count,
            SortedWords = sorted,
            Frequencies = frequencies,
            ElapsedMs = elapsedMs,
            Explanation = expl
        };
    }

    // =========================================================================
    // ЭКСПЕРИМЕНТЫ ПРОИЗВОДИТЕЛЬНОСТИ
    // =========================================================================
    public static string[] GenerateBenchmarkWords(int count, int seed = 42)
    {
        var rng = new Random(seed + count);
        var result = new string[count];
        for (int i = 0; i < count; i++)
            result[i] = EnglishVocabulary[rng.Next(EnglishVocabulary.Length)];
        return result;
    }

    public static BenchmarkReport RunBenchmarks(
        int[]? sizes = null,
        LexicographicAlgorithm algo1 = LexicographicAlgorithm.QuickSortHoare,
        LexicographicAlgorithm algo2 = LexicographicAlgorithm.AbcRadixSort,
        int repeats = 5)
    {
        sizes ??= new[] { 100, 500, 1000, 2000, 5000 };
        repeats = Math.Clamp(repeats, 1, 15);

        string name1 = GetAlgorithmName(algo1);
        string name2 = GetAlgorithmName(algo2);
        var rows = new List<BenchmarkRow>(sizes.Length);

        // Прогрев JIT на маленьком наборе, чтобы первый замер был честным
        var warmup = GenerateBenchmarkWords(30, 1);
        SortWords(algo1, warmup);
        SortWords(algo2, warmup);

        foreach (int size in sizes)
        {
            var dataset = GenerateBenchmarkWords(size, 42);

            double sum1 = 0;
            string[] res1 = Array.Empty<string>();
            for (int r = 0; r < repeats; r++)
            {
                var copy = (string[])dataset.Clone();
                var sw = Stopwatch.StartNew();
                res1 = SortWords(algo1, copy);
                sw.Stop();
                sum1 += sw.Elapsed.TotalMilliseconds;
            }

            double sum2 = 0;
            string[] res2 = Array.Empty<string>();
            for (int r = 0; r < repeats; r++)
            {
                var copy = (string[])dataset.Clone();
                var sw = Stopwatch.StartNew();
                res2 = SortWords(algo2, copy);
                sw.Stop();
                sum2 += sw.Elapsed.TotalMilliseconds;
            }

            bool match = res1.Length == res2.Length;
            if (match)
            {
                for (int i = 0; i < res1.Length; i++)
                {
                    if (res1[i] != res2[i]) { match = false; break; }
                }
            }

            rows.Add(new BenchmarkRow
            {
                WordCount = size,
                Algorithm1Name = name1,
                Time1Ms = sum1 / repeats,
                Algorithm2Name = name2,
                Time2Ms = sum2 / repeats,
                ResultsMatch = match
            });
        }

        return new BenchmarkReport
        {
            Algorithm1Name = name1,
            Algorithm2Name = name2,
            Repeats = repeats,
            Rows = rows
        };
    }
}
