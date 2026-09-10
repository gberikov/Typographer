using System.Reflection;

namespace Typographer.Tests.Corpus;

/// <summary>Golden-корпус: пары файлов «вход — эталонный выход» в tests/Typographer.Corpus.</summary>
/// <remarks>
/// Каталог найден через атрибут сборки, а не прыжками вверх от каталога сборки: число
/// прыжков зависит от конфигурации и целевой платформы, а путь нужен ИСХОДНЫЙ — режим
/// перегенерации перезаписывает эталоны в репозитории, а не в bin.
/// Перевод строки при чтении приводится к «\n», а последний отбрасывается: файл в git
/// заканчивается переводом строки по соглашению, и этот перевод — свойство файла, а не
/// входных данных.
/// </remarks>
public static class CorpusFiles
{
    /// <summary>Каталог корпуса. Абсолютный путь, полученный из пути к csproj во время сборки.</summary>
    public static string Directory { get; } = Path.GetFullPath(
        typeof(CorpusFiles).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "CorpusDirectory")
            .Value!);

    /// <summary>Имена случаев корпуса без расширений, в алфавитном порядке.</summary>
    public static TheoryData<string> Names
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (string name in EnumerateNames())
            {
                data.Add(name);
            }

            return data;
        }
    }

    /// <summary>Имена случаев корпуса — то же самое, но пригодное для обхода в цикле.</summary>
    public static IEnumerable<string> EnumerateNames()
        => System.IO.Directory.EnumerateFiles(Directory, "*.in.txt")
            .Select(path => Path.GetFileName(path)[..^".in.txt".Length])
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>Читает вход случая.</summary>
    public static string ReadInput(string name) => Read($"{name}.in.txt");

    /// <summary>Читает эталонный выход случая. Пустая строка, если эталона ещё нет.</summary>
    public static string ReadExpected(string name)
    {
        string path = Path.Combine(Directory, $"{name}.out.txt");
        return File.Exists(path) ? Read($"{name}.out.txt") : string.Empty;
    }

    /// <summary>Записывает эталонный выход случая. Только режим перегенерации.</summary>
    public static void WriteExpected(string name, string value)
        => File.WriteAllText(Path.Combine(Directory, $"{name}.out.txt"), value + "\n");

    private static string Read(string fileName)
    {
        string text = File.ReadAllText(Path.Combine(Directory, fileName)).Replace("\r\n", "\n");
        return text.EndsWith('\n') ? text[..^1] : text;
    }
}
