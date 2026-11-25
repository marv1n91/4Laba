using System.Globalization;
using System.Text;
using System.Text.Json;
using SortingLab4.Core.Models;

namespace SortingLab4.Core.Algorithms;

/// <summary>
/// Внешняя сортировка записей таблиц с реальным использованием временных файлов на диске
/// (без подмены встроенной сортировкой в памяти).
/// Реализованы три метода:
/// 1. Прямое слияние (Direct Merge)
/// 2. Естественное слияние (Natural Merge)
/// 3. Многопутевое слияние (Multiway Merge, k вспомогательных файлов)
/// </summary>
public static class ExternalSortingAlgorithms
{
    // Порядок букв русского алфавита (буква «ё» идёт сразу после «е»)
    private const string RuAlphabet = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
    private static readonly Dictionary<char, int> RuOrder = BuildRuOrder();

    private static Dictionary<char, int> BuildRuOrder()
    {
        var map = new Dictionary<char, int>(RuAlphabet.Length);
        for (int i = 0; i < RuAlphabet.Length; i++) map[RuAlphabet[i]] = i;
        return map;
    }

    public static KeyComparisonType DetectColumnType(IReadOnlyList<string[]> rows, int colIndex)
    {
        bool hasNonEmpty = false;
        foreach (var row in rows)
        {
            if (colIndex >= row.Length) continue;
            string raw = row[colIndex].Trim();
            if (string.IsNullOrEmpty(raw)) continue;
            hasNonEmpty = true;
            if (!TryParseNumber(raw, out _)) return KeyComparisonType.Text;
        }
        return hasNonEmpty ? KeyComparisonType.Number : KeyComparisonType.Text;
    }

    public static bool TryParseNumber(string raw, out double value)
    {
        string cleaned = raw.Trim().Replace(" ", "").Replace(',', '.');
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Сравнивает две записи по ключевому столбцу (числовому или текстовому).
    /// </summary>
    public static int CompareRecords(string[] a, string[] b, int keyCol, KeyComparisonType keyType)
    {
        string sa = keyCol < a.Length ? a[keyCol].Trim() : string.Empty;
        string sb = keyCol < b.Length ? b[keyCol].Trim() : string.Empty;

        if (keyType == KeyComparisonType.Number)
        {
            bool okA = TryParseNumber(sa, out double na);
            bool okB = TryParseNumber(sb, out double nb);
            if (okA && okB)
            {
                if (na < nb) return -1;
                if (na > nb) return 1;
                return 0;
            }
            if (okA) return -1;
            if (okB) return 1;
        }

        return CompareText(sa, sb);
    }

    /// <summary>
    /// Алфавитное сравнение строк с корректным порядком русских букв (включая «ё»).
    /// </summary>
    public static int CompareText(string a, string b)
    {
        string la = a.ToLowerInvariant();
        string lb = b.ToLowerInvariant();
        int len = Math.Min(la.Length, lb.Length);
        for (int i = 0; i < len; i++)
        {
            char ca = la[i];
            char cb = lb[i];
            if (ca == cb) continue;

            var (ga, ra) = GetCharGroupAndRank(ca);
            var (gb, rb) = GetCharGroupAndRank(cb);
            if (ga != gb) return ga < gb ? -1 : 1;
            if (ra != rb) return ra < rb ? -1 : 1;
        }
        if (la.Length < lb.Length) return -1;
        if (la.Length > lb.Length) return 1;
        return 0;
    }

    private static (int Group, int Rank) GetCharGroupAndRank(char ch)
    {
        if (ch >= '0' && ch <= '9') return (0, ch);
        if (ch >= 'a' && ch <= 'z') return (1, ch);
        if (RuOrder.TryGetValue(ch, out int ruIdx)) return (2, ruIdx);
        return (3, ch);
    }

    private static void WriteRecord(StreamWriter writer, string[] record)
    {
        writer.WriteLine(JsonSerializer.Serialize(record));
    }

    private static string[]? ReadRecord(StreamReader reader)
    {
        string? line = reader.ReadLine();
        if (line is null) return null;
        return JsonSerializer.Deserialize<string[]>(line);
    }

    private static List<FileRecordPreview> ReadFilePreview(string path, int keyCol, int maxItems = 35)
    {
        var list = new List<FileRecordPreview>();
        if (!File.Exists(path)) return list;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        int count = 0;
        while (count < maxItems)
        {
            string? line = reader.ReadLine();
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var rec = JsonSerializer.Deserialize<string[]>(line);
            if (rec is null) continue;
            list.Add(PreviewRecord(rec, keyCol));
            count++;
        }
        return list;
    }

    private static List<string[]> ReadAllRecords(string path)
    {
        var result = new List<string[]>();
        if (!File.Exists(path)) return result;
        using var reader = new StreamReader(path, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var rec = JsonSerializer.Deserialize<string[]>(line);
            if (rec is not null) result.Add(rec);
        }
        return result;
    }

    private static FileRecordPreview PreviewRecord(string[] record, int keyCol) => new()
    {
        Key = keyCol < record.Length ? record[keyCol] : string.Empty,
        Label = record.Length > 0 ? record[0] : string.Empty
    };

    private static void RecordStep(List<ExternalSortStep> steps, int maxSteps, int keyCol,
        string type, string label, int passNumber, string explanation,
        string mainPath, string[] auxPaths, string[] auxNames, ExternalMergeDecision? decision = null)
    {
        if (steps.Count >= maxSteps) return;

        var auxSnapshots = new List<AuxFileSnapshot>(auxPaths.Length);
        for (int i = 0; i < auxPaths.Length; i++)
        {
            auxSnapshots.Add(new AuxFileSnapshot
            {
                Name = auxNames[i],
                Records = ReadFilePreview(auxPaths[i], keyCol),
                IsActiveSource = decision?.Chosen.FileName == auxNames[i]
            });
        }

        steps.Add(new ExternalSortStep
        {
            StepNumber = steps.Count + 1,
            OperationType = type,
            OperationLabel = label,
            PassNumber = passNumber,
            Explanation = explanation,
            MainFileRecords = ReadFilePreview(mainPath, keyCol),
            AuxFiles = auxSnapshots,
            MergeDecision = decision
        });
    }

    private static void RecordMergeWrite(List<ExternalSortStep> steps, int maxSteps, int keyCol, int passNumber,
        string mainPath, string[] auxPaths, string[] auxNames, int outputPosition, int selectedFile, bool equalMinimum,
        IReadOnlyList<(int File, string[] Record)> heads)
    {
        if (steps.Count >= maxSteps) return;

        var candidates = heads.Select(head => new ExternalMergeCandidate(auxNames[head.File], PreviewRecord(head.Record, keyCol))).ToArray();
        var chosen = candidates.First(candidate => candidate.FileName == auxNames[selectedFile]);
        var decision = new ExternalMergeDecision(candidates, chosen, outputPosition, equalMinimum);
        bool hasComparison = candidates.Length > 1;
        string reason = candidates.Length == 1 ? "Осталась одна активная серия, поэтому сравнение не нужно."
            : equalMinimum ? "Минимальные ключи равны, берём запись из более раннего файла."
            : "Берём запись с наименьшим ключом.";
        RecordStep(steps, maxSteps, keyCol, hasComparison ? "merge_compare" : "merge_append",
            hasComparison ? "Сравнение и слияние" : "Остаток серии", passNumber,
            $"{reason} Записываем «{chosen.Record.DisplayText}» в основной файл на позицию {outputPosition}.",
            mainPath, auxPaths, auxNames, decision);
    }

    public static ExternalSortResult Sort(
        ExternalSortMethod method,
        List<string> headers,
        List<string[]> rows,
        int keyCol,
        KeyComparisonType keyType = KeyComparisonType.Auto,
        int ways = 3,
        int maxSteps = 300)
    {
        var resolvedType = keyType == KeyComparisonType.Auto
            ? DetectColumnType(rows, keyCol)
            : keyType;

        return method switch
        {
            ExternalSortMethod.DirectMerge => DirectMerge(headers, rows, keyCol, resolvedType, maxSteps),
            ExternalSortMethod.NaturalMerge => NaturalMerge(headers, rows, keyCol, resolvedType, maxSteps),
            ExternalSortMethod.MultiwayMerge => MultiwayMerge(headers, rows, keyCol, resolvedType, ways, maxSteps),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
    }

    // =========================================================================
    // 1. ПРЯМОЕ СЛИЯНИЕ (Direct Merge)
    // =========================================================================
    private static ExternalSortResult DirectMerge(
        List<string> headers, List<string[]> rows, int keyCol, KeyComparisonType keyType, int maxSteps)
    {
        var steps = new List<ExternalSortStep>();
        var stats = new ExternalSortStats { TotalRecords = rows.Count, Ways = 2 };

        void RecordStep(string type, string label, int passNum, string explanation,
            string mainPath, string[] auxPaths, string[] auxNames) =>
            ExternalSortingAlgorithms.RecordStep(steps, maxSteps, keyCol, type, label, passNum, explanation,
                mainPath, auxPaths, auxNames);

        if (rows.Count <= 1)
        {
            return BuildTrivialResult(ExternalSortMethod.DirectMerge, "Прямое слияние", headers, rows, keyCol, keyType, 2);
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "SortingLab4_Direct_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string mainPath = Path.Combine(tempDir, "main.jsonl");
            string auxAPath = Path.Combine(tempDir, "aux_A.jsonl");
            string auxBPath = Path.Combine(tempDir, "aux_B.jsonl");
            string[] auxPaths = { auxAPath, auxBPath };
            string[] auxNames = { "Файл A", "Файл B" };

            using (var fm = new StreamWriter(mainPath, false, Encoding.UTF8))
            {
                foreach (var r in rows) WriteRecord(fm, r);
            }

            RecordStep("start", "Старт", 0,
                $"Начинаем прямое внешнее слияние ({rows.Count} записей). На каждом проходе будем раскладывать данные по Файлу A и Файлу B сериями длиной 1, 2, 4, 8... и сливать их обратно.",
                mainPath, auxPaths, auxNames);

            int runLen = 1;
            int n = rows.Count;

            while (runLen < n)
            {
                stats.Passes++;
                // Фаза 1: Разделение на серии длины runLen
                using (var fm = new StreamReader(mainPath, Encoding.UTF8))
                using (var fa = new StreamWriter(auxAPath, false, Encoding.UTF8))
                using (var fb = new StreamWriter(auxBPath, false, Encoding.UTF8))
                {
                    bool toA = true;
                    while (true)
                    {
                        int written = 0;
                        var target = toA ? fa : fb;
                        for (int k = 0; k < runLen; k++)
                        {
                            var rec = ReadRecord(fm);
                            if (rec is null) break;
                            WriteRecord(target, rec);
                            stats.Moves++;
                            written++;
                        }
                        if (written < runLen) break;
                        toA = !toA;
                    }
                }

                RecordStep("split", "Разделение на серии", stats.Passes,
                    $"Проход {stats.Passes} (длина серии = {runLen}): разложили записи из основного файла по очереди в Файл A и Файл B группами по {runLen}.",
                    mainPath, auxPaths, auxNames);

                // Фаза 2: Слияние пар серий из A и B в основной файл
                using (var fa = new StreamReader(auxAPath, Encoding.UTF8))
                using (var fb = new StreamReader(auxBPath, Encoding.UTF8))
                using (var fm = new StreamWriter(mainPath, false, Encoding.UTF8))
                {
                    var recA = ReadRecord(fa);
                    var recB = ReadRecord(fb);
                    int outputPosition = 0;

                    while (recA is not null || recB is not null)
                    {
                        int countA = 0, countB = 0;
                        while (countA < runLen && countB < runLen && recA is not null && recB is not null)
                        {
                            stats.Comparisons++;
                            int comparison = CompareRecords(recA, recB, keyCol, keyType);
                            int selectedFile = comparison <= 0 ? 0 : 1;
                            WriteRecord(fm, selectedFile == 0 ? recA : recB);
                            fm.Flush();
                            stats.Moves++;
                            RecordMergeWrite(steps, maxSteps, keyCol, stats.Passes, mainPath, auxPaths, auxNames,
                                ++outputPosition, selectedFile, comparison == 0, [(0, recA), (1, recB)]);
                            if (selectedFile == 0)
                            {
                                recA = ReadRecord(fa);
                                countA++;
                            }
                            else
                            {
                                recB = ReadRecord(fb);
                                countB++;
                            }
                        }

                        while (countA < runLen && recA is not null)
                        {
                            WriteRecord(fm, recA);
                            fm.Flush();
                            stats.Moves++;
                            RecordMergeWrite(steps, maxSteps, keyCol, stats.Passes, mainPath, auxPaths, auxNames,
                                ++outputPosition, 0, false, [(0, recA)]);
                            recA = ReadRecord(fa);
                            countA++;
                        }
                        while (countB < runLen && recB is not null)
                        {
                            WriteRecord(fm, recB);
                            fm.Flush();
                            stats.Moves++;
                            RecordMergeWrite(steps, maxSteps, keyCol, stats.Passes, mainPath, auxPaths, auxNames,
                                ++outputPosition, 1, false, [(1, recB)]);
                            recB = ReadRecord(fb);
                            countB++;
                        }
                    }
                }

                RecordStep("merge_end", "Конец прохода", stats.Passes,
                    $"Проход {stats.Passes} завершён! В основном файле сформированы упорядоченные серии длиной {runLen * 2}.",
                    mainPath, auxPaths, auxNames);

                runLen *= 2;
            }

            var sortedRows = ReadAllRecords(mainPath);
            File.WriteAllText(auxAPath, string.Empty);
            File.WriteAllText(auxBPath, string.Empty);

            steps.Add(new ExternalSortStep
            {
                StepNumber = steps.Count + 1,
                OperationType = "done",
                OperationLabel = "Завершено",
                PassNumber = stats.Passes,
                Explanation = $"Прямое внешнее слияние завершено за {stats.Passes} проход(а/ов)! Все {n} записей отсортированы.",
                MainFileRecords = ReadFilePreview(mainPath, keyCol),
                AuxFiles = new List<AuxFileSnapshot>
                {
                    new() { Name = "Файл A" },
                    new() { Name = "Файл B" }
                }
            });

            return new ExternalSortResult
            {
                Method = ExternalSortMethod.DirectMerge,
                MethodName = "Прямое слияние",
                Headers = headers,
                KeyColumn = keyCol,
                ResolvedKeyType = keyType,
                SortedRows = sortedRows,
                Stats = stats,
                Steps = steps
            };
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    // =========================================================================
    // 2. ЕСТЕСТВЕННОЕ СЛИЯНИЕ (Natural Merge)
    // =========================================================================
    private static ExternalSortResult NaturalMerge(
        List<string> headers, List<string[]> rows, int keyCol, KeyComparisonType keyType, int maxSteps)
    {
        var steps = new List<ExternalSortStep>();
        var stats = new ExternalSortStats { TotalRecords = rows.Count, Ways = 2 };

        void RecordStep(string type, string label, int passNum, string explanation,
            string mainPath, string[] auxPaths, string[] auxNames) =>
            ExternalSortingAlgorithms.RecordStep(steps, maxSteps, keyCol, type, label, passNum, explanation,
                mainPath, auxPaths, auxNames);

        if (rows.Count <= 1)
        {
            return BuildTrivialResult(ExternalSortMethod.NaturalMerge, "Естественное слияние", headers, rows, keyCol, keyType, 2);
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "SortingLab4_Natural_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string mainPath = Path.Combine(tempDir, "main.jsonl");
            string auxAPath = Path.Combine(tempDir, "aux_A.jsonl");
            string auxBPath = Path.Combine(tempDir, "aux_B.jsonl");
            string[] auxPaths = { auxAPath, auxBPath };
            string[] auxNames = { "Файл A", "Файл B" };

            using (var fm = new StreamWriter(mainPath, false, Encoding.UTF8))
            {
                foreach (var r in rows) WriteRecord(fm, r);
            }

            RecordStep("start", "Старт", 0,
                $"Начинаем естественное слияние ({rows.Count} записей). Будем находить в файле уже упорядоченные цепочки (естественные серии), раскладывать их по Файлу A и Файлу B, а затем сливать.",
                mainPath, auxPaths, auxNames);

            while (true)
            {
                stats.Passes++;
                int runsCount = 0;

                using (var fm = new StreamReader(mainPath, Encoding.UTF8))
                using (var fa = new StreamWriter(auxAPath, false, Encoding.UTF8))
                using (var fb = new StreamWriter(auxBPath, false, Encoding.UTF8))
                {
                    string[]? prev = null;
                    bool toA = true;
                    while (true)
                    {
                        var rec = ReadRecord(fm);
                        if (rec is null) break;
                        if (prev is null)
                        {
                            runsCount = 1;
                        }
                        else
                        {
                            stats.Comparisons++;
                            if (CompareRecords(rec, prev, keyCol, keyType) < 0)
                            {
                                toA = !toA;
                                runsCount++;
                            }
                        }
                        WriteRecord(toA ? fa : fb, rec);
                        stats.Moves++;
                        prev = rec;
                    }
                }

                if (runsCount <= 1)
                {
                    RecordStep("detect", "Проверка серий", stats.Passes,
                        $"Проход {stats.Passes}: весь файл уже состоит из одной упорядоченной серии. Сортировка завершена.",
                        mainPath, auxPaths, auxNames);
                    break;
                }

                RecordStep("split", "Естественные серии", stats.Passes,
                    $"Проход {stats.Passes}: найдено {runsCount} упорядоченных серий, раскладываем их по очереди в Файл A и Файл B.",
                    mainPath, auxPaths, auxNames);

                using (var fa = new StreamReader(auxAPath, Encoding.UTF8))
                using (var fb = new StreamReader(auxBPath, Encoding.UTF8))
                using (var fm = new StreamWriter(mainPath, false, Encoding.UTF8))
                {
                    var recA = ReadRecord(fa);
                    var recB = ReadRecord(fb);
                    int outputPosition = 0;

                    while (recA is not null || recB is not null)
                    {
                        bool endA = recA is null;
                        bool endB = recB is null;

                        while (!endA && !endB)
                        {
                            stats.Comparisons++;
                            int comparison = CompareRecords(recA!, recB!, keyCol, keyType);
                            int selectedFile = comparison <= 0 ? 0 : 1;
                            var chosen = (selectedFile == 0 ? recA : recB)!;
                            WriteRecord(fm, chosen);
                            fm.Flush();
                            stats.Moves++;
                            RecordMergeWrite(steps, maxSteps, keyCol, stats.Passes, mainPath, auxPaths, auxNames,
                                ++outputPosition, selectedFile, comparison == 0, [(0, recA!), (1, recB!)]);
                            if (selectedFile == 0)
                            {
                                var nextA = ReadRecord(fa);
                                if (nextA is null || CompareRecords(nextA, chosen, keyCol, keyType) < 0) endA = true;
                                recA = nextA;
                            }
                            else
                            {
                                var nextB = ReadRecord(fb);
                                if (nextB is null || CompareRecords(nextB, chosen, keyCol, keyType) < 0) endB = true;
                                recB = nextB;
                            }
                        }

                        while (!endA && recA is not null)
                        {
                            var chosen = recA;
                            WriteRecord(fm, chosen);
                            fm.Flush();
                            stats.Moves++;
                            RecordMergeWrite(steps, maxSteps, keyCol, stats.Passes, mainPath, auxPaths, auxNames,
                                ++outputPosition, 0, false, [(0, chosen)]);
                            var nextA = ReadRecord(fa);
                            if (nextA is null || CompareRecords(nextA, chosen, keyCol, keyType) < 0) endA = true;
                            recA = nextA;
                        }

                        while (!endB && recB is not null)
                        {
                            var chosen = recB;
                            WriteRecord(fm, chosen);
                            fm.Flush();
                            stats.Moves++;
                            RecordMergeWrite(steps, maxSteps, keyCol, stats.Passes, mainPath, auxPaths, auxNames,
                                ++outputPosition, 1, false, [(1, chosen)]);
                            var nextB = ReadRecord(fb);
                            if (nextB is null || CompareRecords(nextB, chosen, keyCol, keyType) < 0) endB = true;
                            recB = nextB;
                        }
                    }
                }

                RecordStep("merge_end", "Конец прохода", stats.Passes,
                    $"Проход {stats.Passes} завершён! Естественные серии слились в более длинные упорядоченные цепочки.",
                    mainPath, auxPaths, auxNames);
            }

            var sortedRows = ReadAllRecords(mainPath);
            File.WriteAllText(auxAPath, string.Empty);
            File.WriteAllText(auxBPath, string.Empty);

            steps.Add(new ExternalSortStep
            {
                StepNumber = steps.Count + 1,
                OperationType = "done",
                OperationLabel = "Завершено",
                PassNumber = stats.Passes,
                Explanation = $"Естественное слияние завершено! Все {rows.Count} записей выстроены по возрастанию.",
                MainFileRecords = ReadFilePreview(mainPath, keyCol),
                AuxFiles = new List<AuxFileSnapshot>
                {
                    new() { Name = "Файл A" },
                    new() { Name = "Файл B" }
                }
            });

            return new ExternalSortResult
            {
                Method = ExternalSortMethod.NaturalMerge,
                MethodName = "Естественное слияние",
                Headers = headers,
                KeyColumn = keyCol,
                ResolvedKeyType = keyType,
                SortedRows = sortedRows,
                Stats = stats,
                Steps = steps
            };
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    // =========================================================================
    // 3. МНОГОПУТЕВОЕ СЛИЯНИЕ (Multiway Merge, k-путевое)
    // =========================================================================
    private static ExternalSortResult MultiwayMerge(
        List<string> headers, List<string[]> rows, int keyCol, KeyComparisonType keyType, int ways, int maxSteps)
    {
        ways = Math.Clamp(ways, 2, 6);
        var steps = new List<ExternalSortStep>();
        var stats = new ExternalSortStats { TotalRecords = rows.Count, Ways = ways };

        void RecordStep(string type, string label, int passNum, string explanation,
            string mainPath, string[] auxPaths, string[] auxNames) =>
            ExternalSortingAlgorithms.RecordStep(steps, maxSteps, keyCol, type, label, passNum, explanation,
                mainPath, auxPaths, auxNames);

        if (rows.Count <= 1)
        {
            return BuildTrivialResult(ExternalSortMethod.MultiwayMerge, $"Многопутевое слияние ({ways}-путевое)", headers, rows, keyCol, keyType, ways);
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "SortingLab4_Multi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string mainPath = Path.Combine(tempDir, "main.jsonl");
            var auxPaths = new string[ways];
            var auxNames = new string[ways];
            for (int i = 0; i < ways; i++)
            {
                auxPaths[i] = Path.Combine(tempDir, $"aux_{i + 1}.jsonl");
                auxNames[i] = $"Файл {i + 1}";
            }

            using (var fm = new StreamWriter(mainPath, false, Encoding.UTF8))
            {
                foreach (var r in rows) WriteRecord(fm, r);
            }

            RecordStep("start", "Старт", 0,
                $"Начинаем {ways}-путевое внешнее слияние ({rows.Count} записей). Серии будут распределяться сразу по {ways} вспомогательным файлам и сливаться одновременно.",
                mainPath, auxPaths, auxNames);

            while (true)
            {
                stats.Passes++;
                int runsCount = 0;

                var writers = new StreamWriter[ways];
                try
                {
                    for (int i = 0; i < ways; i++) writers[i] = new StreamWriter(auxPaths[i], false, Encoding.UTF8);
                    using var fm = new StreamReader(mainPath, Encoding.UTF8);
                    string[]? prev = null;
                    int currentTape = 0;

                    while (true)
                    {
                        var rec = ReadRecord(fm);
                        if (rec is null) break;
                        if (prev is null)
                        {
                            runsCount = 1;
                        }
                        else
                        {
                            stats.Comparisons++;
                            if (CompareRecords(rec, prev, keyCol, keyType) < 0)
                            {
                                currentTape = (currentTape + 1) % ways;
                                runsCount++;
                            }
                        }
                        WriteRecord(writers[currentTape], rec);
                        stats.Moves++;
                        prev = rec;
                    }
                }
                finally
                {
                    foreach (var w in writers) w?.Dispose();
                }

                if (runsCount <= 1)
                {
                    RecordStep("detect", "Проверка серий", stats.Passes,
                        $"Проход {stats.Passes}: осталась одна упорядоченная серия. Сортировка завершена.",
                        mainPath, auxPaths, auxNames);
                    break;
                }

                RecordStep("split", $"Распределение по {ways} файлам", stats.Passes,
                    $"Проход {stats.Passes}: распределили {runsCount} серий по кругу между {ways} вспомогательными файлами.",
                    mainPath, auxPaths, auxNames);

                var readers = new StreamReader[ways];
                try
                {
                    for (int i = 0; i < ways; i++) readers[i] = new StreamReader(auxPaths[i], Encoding.UTF8);
                    using var fm = new StreamWriter(mainPath, false, Encoding.UTF8);

                    var heads = new string[]?[ways];
                    for (int i = 0; i < ways; i++) heads[i] = ReadRecord(readers[i]);
                    int outputPosition = 0;

                    while (AnyNotNull(heads))
                    {
                        var runActive = new bool[ways];
                        for (int i = 0; i < ways; i++) runActive[i] = heads[i] is not null;

                        while (AnyTrue(runActive))
                        {
                            // Ручной поиск минимума среди текущих первых записей всех активных файлов
                            int bestIdx = -1;
                            bool equalMinimum = false;
                            var candidates = steps.Count < maxSteps ? new List<(int File, string[] Record)>(ways) : null;
                            for (int i = 0; i < ways; i++)
                            {
                                if (!runActive[i] || heads[i] is null) continue;
                                candidates?.Add((i, heads[i]!));
                                if (bestIdx == -1)
                                {
                                    bestIdx = i;
                                }
                                else
                                {
                                    stats.Comparisons++;
                                    int comparison = CompareRecords(heads[i]!, heads[bestIdx]!, keyCol, keyType);
                                    if (comparison < 0)
                                    {
                                        bestIdx = i;
                                        equalMinimum = false;
                                    }
                                    else if (comparison == 0) equalMinimum = true;
                                }
                            }

                            var chosen = heads[bestIdx]!;
                            WriteRecord(fm, chosen);
                            fm.Flush();
                            stats.Moves++;
                            outputPosition++;

                            if (candidates is not null)
                                RecordMergeWrite(steps, maxSteps, keyCol, stats.Passes, mainPath, auxPaths, auxNames,
                                    outputPosition, bestIdx, equalMinimum, candidates);

                            var nextRec = ReadRecord(readers[bestIdx]);
                            if (nextRec is null || CompareRecords(nextRec, chosen, keyCol, keyType) < 0)
                            {
                                runActive[bestIdx] = false;
                            }
                            heads[bestIdx] = nextRec;
                        }
                    }
                }
                finally
                {
                    foreach (var r in readers) r?.Dispose();
                }

                RecordStep("merge_end", "Конец прохода", stats.Passes,
                    $"Проход {stats.Passes} завершён! Серии из {ways} файлов объединены в более крупные отсортированные серии.",
                    mainPath, auxPaths, auxNames);
            }

            var sortedRows = ReadAllRecords(mainPath);
            var emptyAux = new List<AuxFileSnapshot>();
            for (int i = 0; i < ways; i++)
            {
                File.WriteAllText(auxPaths[i], string.Empty);
                emptyAux.Add(new AuxFileSnapshot { Name = auxNames[i] });
            }

            steps.Add(new ExternalSortStep
            {
                StepNumber = steps.Count + 1,
                OperationType = "done",
                OperationLabel = "Завершено",
                PassNumber = stats.Passes,
                Explanation = $"Многопутевое ({ways}-путевое) внешнее слияние завершено! Все {rows.Count} записей отсортированы.",
                MainFileRecords = ReadFilePreview(mainPath, keyCol),
                AuxFiles = emptyAux
            });

            return new ExternalSortResult
            {
                Method = ExternalSortMethod.MultiwayMerge,
                MethodName = $"Многопутевое слияние ({ways}-путевое)",
                Headers = headers,
                KeyColumn = keyCol,
                ResolvedKeyType = keyType,
                SortedRows = sortedRows,
                Stats = stats,
                Steps = steps
            };
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    private static bool AnyNotNull(string[]?[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] is not null) return true;
        return false;
    }

    private static bool AnyTrue(bool[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]) return true;
        return false;
    }

    private static ExternalSortResult BuildTrivialResult(
        ExternalSortMethod method, string name, List<string> headers, List<string[]> rows,
        int keyCol, KeyComparisonType keyType, int ways)
    {
        var aux = new List<AuxFileSnapshot>();
        for (int i = 0; i < ways; i++)
            aux.Add(new AuxFileSnapshot { Name = ways == 2 ? (i == 0 ? "Файл A" : "Файл B") : $"Файл {i + 1}" });

        var mainPreview = rows.Select(r => new FileRecordPreview
        {
            Key = keyCol < r.Length ? r[keyCol] : "",
            Label = r.Length > 0 ? r[0] : ""
        }).ToList();

        return new ExternalSortResult
        {
            Method = method,
            MethodName = name,
            Headers = headers,
            KeyColumn = keyCol,
            ResolvedKeyType = keyType,
            SortedRows = new List<string[]>(rows),
            Stats = new ExternalSortStats { TotalRecords = rows.Count, Ways = ways },
            Steps = new List<ExternalSortStep>
            {
                new()
                {
                    StepNumber = 1,
                    OperationType = "done",
                    OperationLabel = "Готово",
                    Explanation = "В таблице меньше двух записей, сортировка не требуется.",
                    MainFileRecords = mainPreview,
                    AuxFiles = aux
                }
            }
        };
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
            // Игнорируем ошибку очистки временного каталога
        }
    }
}
