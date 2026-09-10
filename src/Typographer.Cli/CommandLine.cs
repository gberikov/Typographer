using System.Globalization;
using System.Reflection;
using System.Text;
using Typographer.Rules;

namespace Typographer.Cli;

/// <summary>Разбор ключей и работа с потоками. Всё, что можно проверить тестом.</summary>
/// <remarks>
/// Ключи разбираются вручную: спецификация (раздел 10) обещает утилите единственную
/// зависимость — ядро, а System.CommandLine был бы второй ради двух десятков строк.
/// </remarks>
internal static class CommandLine
{
    private const string Help = """
        Типограф для русского языка.

        Использование: dotnet-typographer [ключи] [файл...]

        Без файлов читает стандартный ввод и пишет в стандартный вывод.

          -t, --text              обычный текст вместо HTML
          -e, --entities <режим>  symbols | named | numeric | mixed (по умолчанию symbols)
          -r, --rules <пресет>    default | lebedev | typograf | gost | all | minimal | none
              --enable <правило>  включить правило по имени, можно повторять
              --disable <правило> выключить правило по имени, можно повторять
              --br                перевод строки заменять на <br />
              --p                 абзацы оборачивать в <p>
              --nobr <число>      объединять до N слов в <nobr>
          -i, --in-place          писать результат обратно в файлы
          -l, --list-rules        напечатать имена всех правил и выйти
          -h, --help              эта справка
          -V, --version           версия

        Имена правил совпадают с именами JS-typograf: ru/dash/main, common/nbsp/afterShortWord.
        """;

    /// <summary>Выполняет команду.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <param name="input">Стандартный ввод.</param>
    /// <param name="output">Стандартный вывод.</param>
    /// <param name="error">Поток ошибок.</param>
    /// <returns>Ноль при успехе, единица при любой ошибке.</returns>
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        bool text = false;
        bool inPlace = false;
        EntityMode entities = EntityMode.Symbols;
        RuleSet rules = RuleSet.Default;
        bool useBr = false;
        bool useP = false;
        int maxNobr = 0;
        var files = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h" or "--help":
                    output.WriteLine(Help);
                    return 0;

                case "-V" or "--version":
                    output.WriteLine(Version());
                    return 0;

                case "-l" or "--list-rules":
                    foreach (RuleId rule in RuleSet.All)
                    {
                        output.WriteLine(rule.Name);
                    }

                    return 0;

                case "-t" or "--text":
                    text = true;
                    break;

                case "-i" or "--in-place":
                    inPlace = true;
                    break;

                case "--br":
                    useBr = true;
                    break;

                case "--p":
                    useP = true;
                    break;

                case "-e" or "--entities":
                    if (!TryTakeValue(args, ref i, out string mode))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!TryParseEntities(mode, out entities))
                    {
                        return Fail(error, $"Неизвестный режим сущностей: {mode}.");
                    }

                    break;

                case "-r" or "--rules":
                    if (!TryTakeValue(args, ref i, out string preset))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!TryParsePreset(preset, out rules))
                    {
                        return Fail(error, $"Неизвестный пресет: {preset}.");
                    }

                    break;

                case "--enable" or "--disable":
                    if (!TryTakeValue(args, ref i, out string name))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!RuleId.TryParse(name, out RuleId named))
                    {
                        return Fail(error, $"Неизвестное правило: {name}.");
                    }

                    rules = arg == "--enable" ? rules.With(named) : rules.Without(named);
                    break;

                case "--nobr":
                    if (!TryTakeValue(args, ref i, out string count))
                    {
                        return Fail(error, $"Ключ {arg} требует значения.");
                    }

                    if (!int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxNobr)
                        || maxNobr < 0)
                    {
                        return Fail(error, $"Ключ --nobr требует неотрицательного числа, получено: {count}.");
                    }

                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        return Fail(error, $"Неизвестный ключ: {arg}. Справка: dotnet-typographer --help");
                    }

                    files.Add(arg);
                    break;
            }
        }

        if (inPlace && files.Count == 0)
        {
            return Fail(error, "Ключ --in-place требует хотя бы одного файла.");
        }

        Func<string, string> process = text
            ? new TextTypographer(new TextOptions { Rules = rules }).Process
            : new HtmlTypographer(new HtmlOptions
            {
                Rules = rules,
                Entities = entities,
                UseBr = useBr,
                UseP = useP,
                MaxNobr = maxNobr,
            }).Process;

        if (files.Count == 0)
        {
            output.Write(process(input.ReadToEnd()));
            return 0;
        }

        foreach (string file in files)
        {
            string source;
            try
            {
                source = File.ReadAllText(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail(error, $"Не удалось прочитать {file}: {e.Message}");
            }

            string result = process(source);

            if (!inPlace)
            {
                output.Write(result);
                continue;
            }

            try
            {
                // Без метки порядка байт: она делает файл нечитаемым для половины утилит,
                // а UTF-8 в ней не нуждается.
                File.WriteAllText(file, result, new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail(error, $"Не удалось записать {file}: {e.Message}");
            }
        }

        return 0;
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryParseEntities(string value, out EntityMode mode)
    {
        switch (value)
        {
            case "symbols": mode = EntityMode.Symbols; return true;
            case "named": mode = EntityMode.Named; return true;
            case "numeric": mode = EntityMode.Numeric; return true;
            case "mixed": mode = EntityMode.Mixed; return true;
            default: mode = EntityMode.Symbols; return false;
        }
    }

    private static bool TryParsePreset(string value, out RuleSet rules)
    {
        switch (value)
        {
            case "default": rules = RuleSet.Default; return true;
            case "lebedev": rules = RuleSet.Lebedev; return true;
            case "typograf": rules = RuleSet.Typograf; return true;
            case "gost": rules = RuleSet.Gost; return true;
            case "all": rules = RuleSet.All; return true;
            case "minimal": rules = RuleSet.Minimal; return true;
            case "none": rules = RuleSet.None; return true;
            default: rules = RuleSet.Default; return false;
        }
    }

    private static string Version()
        => typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
               ?.InformationalVersion
           ?? "0.0.0";

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 1;
    }
}
