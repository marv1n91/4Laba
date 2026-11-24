# Лабораторная работа №4. Алгоритмы сортировки (C#, .NET 8, WPF)

Настольное приложение на C# (.NET 8 + WPF) для трех заданий лабораторной работы по алгоритмам сортировки. Все алгоритмы написаны вручную, без использования `Array.Sort`, `List.Sort` и `LINQ OrderBy`.

## Структура проекта

- `SortingLab4.sln` - файл решения.
- `SortingLab4.Core` - библиотека с алгоритмами, моделями и работой с файлами:
  - `Algorithms/InternalSortingAlgorithms.cs` - Bubble Sort, Insertion Sort, Merge Sort, Quick Sort (Хоара).
  - `Algorithms/ExternalSortingAlgorithms.cs` - прямое, естественное и многопутевое слияние с временными файлами на диске.
  - `Algorithms/LexicographicSortingAlgorithms.cs` - Quick Sort, Merge Sort, ABC / MSD Radix Sort, подсчет частот и замеры времени.
  - `Services/TableFileService.cs` - чтение CSV/TSV/TXT с любым числом столбцов, фильтрация и сохранение результата.
- `SortingLab4.App` - графический интерфейс WPF:
  - `MainWindow.xaml` - окно с тремя вкладками.
  - `Views/InternalSortView.xaml` - пошаговая визуализация внутренней сортировки, настройки массива и журналы операций.
  - `Views/ExternalSortView.xaml` - загрузка таблиц и сценариев, отображение временных файлов, таблицы и сохранение результата.
  - `Views/LexicographicSortView.xaml` - обработка текста, таблица частот, эксперименты и график.
  - `Helpers/UiAnimations.cs` - плавные анимации элементов интерфейса и визуализации.
- `SortingLab4.Tests` - консольный проект с проверкой всех алгоритмов и граничных случаев.
- `data/` - готовые наборы данных (`countries.csv`, `chemicals.csv`, `russian_words.txt`, `sample_english.txt`).

## Запуск

Нужен установленный .NET 8 SDK (или новее) на Windows. Внешних пакетов нет.

Сборка решения:
```bash
dotnet build SortingLab4.sln
```

Запуск приложения:
```bash
dotnet run --project SortingLab4.App
```

Запуск тестов:
```bash
dotnet run --project SortingLab4.Tests
```

Решение также можно открыть в Visual Studio 2022 или Rider через файл `SortingLab4.sln`.
