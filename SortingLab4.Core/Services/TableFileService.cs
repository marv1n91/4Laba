using System.Text;
using SortingLab4.Core.Algorithms;
using SortingLab4.Core.Models;

namespace SortingLab4.Core.Services;

/// <summary>
/// Сервис загрузки, валидации, фильтрации и сохранения табличных файлов
/// для Задания №2 (Внешняя сортировка).
/// </summary>
public static class TableFileService
{
    public static TableDocument LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Файл не найден: {filePath}", filePath);

        string text = File.ReadAllText(filePath, Encoding.UTF8);
        return ParseTableText(text, Path.GetFileName(filePath));
    }

    public static TableDocument ParseTableText(string rawText, string fileName = "data.csv")
    {
        if (string.IsNullOrWhiteSpace(rawText))
            throw new InvalidDataException("Файл пустой. Выберите файл с данными или готовый сценарий.");

        string cleaned = rawText.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = cleaned.Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            throw new InvalidDataException("В файле не найдено ни одной непустой строки.");

        char? delimiter = DetectDelimiter(lines[0]);

        // Одноколоночный текстовый файл (например, список русских слов)
        if (delimiter is null)
        {
            var rows = new List<string[]>();
            char[] trimChars = { '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']' };
            foreach (var line in lines)
            {
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    string word = part.Trim(trimChars);
                    if (!string.IsNullOrEmpty(word))
                        rows.Add(new[] { word });
                }
            }

            if (rows.Count == 0)
                throw new InvalidDataException("Не удалось извлечь записи из текстового файла.");

            return new TableDocument
            {
                FileName = fileName,
                IsSingleColumn = true,
                Headers = new List<string> { "Слово" },
                Rows = rows,
                ColumnTypes = new List<KeyComparisonType> { KeyComparisonType.Text }
            };
        }

        // Табличный CSV / TSV файл с произвольным количеством столбцов
        var parsedRows = new List<string[]>();
        foreach (var line in lines)
        {
            var cells = ParseDelimitedLine(line, delimiter.Value);
            if (cells.Any(c => !string.IsNullOrWhiteSpace(c)))
                parsedRows.Add(cells);
        }

        if (parsedRows.Count < 2)
            throw new InvalidDataException("В табличном файле должна быть строка заголовков и хотя бы одна строка данных.");

        var headers = new List<string>(parsedRows[0].Length);
        for (int i = 0; i < parsedRows[0].Length; i++)
        {
            string h = parsedRows[0][i].Trim();
            headers.Add(string.IsNullOrEmpty(h) ? $"Столбец {i + 1}" : h);
        }

        int expectedCols = headers.Count;
        var dataRows = new List<string[]>(parsedRows.Count - 1);
        for (int r = 1; r < parsedRows.Count; r++)
        {
            if (parsedRows[r].Length != expectedCols)
            {
                throw new InvalidDataException(
                    $"Ошибка структуры таблицы в строке {r + 1}: ожидалось {expectedCols} столбц(а/ов), а найдено {parsedRows[r].Length}.");
            }
            var trimmed = new string[expectedCols];
            for (int c = 0; c < expectedCols; c++) trimmed[c] = parsedRows[r][c].Trim();
            dataRows.Add(trimmed);
        }

        var colTypes = new List<KeyComparisonType>(expectedCols);
        for (int c = 0; c < expectedCols; c++)
            colTypes.Add(ExternalSortingAlgorithms.DetectColumnType(dataRows, c));

        // Уникальные категории для удобной фильтрации (например, континент или класс вещества)
        var filterOptions = new Dictionary<int, List<string>>();
        for (int c = 0; c < expectedCols; c++)
        {
            if (colTypes[c] != KeyComparisonType.Text) continue;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unique = new List<string>();
            foreach (var row in dataRows)
            {
                string val = row[c];
                if (!string.IsNullOrEmpty(val) && seen.Add(val))
                    unique.Add(val);
            }
            if (unique.Count > 1 && unique.Count <= 25 && unique.Count < dataRows.Count)
                filterOptions[c] = unique;
        }

        return new TableDocument
        {
            FileName = fileName,
            IsSingleColumn = false,
            Headers = headers,
            Rows = dataRows,
            ColumnTypes = colTypes,
            FilterOptions = filterOptions
        };
    }

    public static List<string[]> FilterRows(List<string[]> rows, int? filterCol, string? filterValue)
    {
        if (filterCol is null || string.IsNullOrWhiteSpace(filterValue))
            return new List<string[]>(rows);

        string target = filterValue.Trim();
        var filtered = new List<string[]>();
        foreach (var row in rows)
        {
            if (filterCol.Value < row.Length &&
                string.Equals(row[filterCol.Value].Trim(), target, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(row);
            }
        }
        return filtered;
    }

    public static string SaveSortedTable(
        string outputDir,
        List<string> headers,
        List<string[]> sortedRows,
        ExternalSortMethod method,
        bool isSingleColumn)
    {
        Directory.CreateDirectory(outputDir);
        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string ext = (isSingleColumn || headers.Count == 1) ? "txt" : "csv";
        string path = Path.Combine(outputDir, $"sorted_{method}_{ts}.{ext}");

        if (ext == "txt")
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            foreach (var row in sortedRows)
                writer.WriteLine(row.Length > 0 ? row[0] : string.Empty);
        }
        else
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            writer.WriteLine(string.Join(",", headers.Select(EscapeCsvCell)));
            foreach (var row in sortedRows)
                writer.WriteLine(string.Join(",", row.Select(EscapeCsvCell)));
        }

        return path;
    }

    private static char? DetectDelimiter(string firstLine)
    {
        char[] candidates = { ',', ';', '\t' };
        char? best = null;
        int bestCount = 0;
        foreach (char d in candidates)
        {
            int cnt = 0;
            foreach (char ch in firstLine)
                if (ch == d) cnt++;
            if (cnt > bestCount)
            {
                bestCount = cnt;
                best = d;
            }
        }
        return bestCount >= 1 ? best : null;
    }

    private static string[] ParseDelimitedLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        cells.Add(current.ToString());
        return cells.ToArray();
    }

    private static string EscapeCsvCell(string cell)
    {
        if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n'))
            return $"\"{cell.Replace("\"", "\"\"")}\"";
        return cell;
    }
}
