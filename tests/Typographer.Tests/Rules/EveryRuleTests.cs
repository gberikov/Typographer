using System.Text;
using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Rules;

/// <summary>Свойства, которые обязано соблюдать КАЖДОЕ правило реестра, включённое в одиночку.</summary>
/// <remarks>
/// Юнит-тесты проверяют правило среди соседей; здесь у правила соседей нет. Разница не
/// теоретическая: правило, полагающееся на пробел, дописанный соседом, в одиночку ведёт себя
/// иначе, и узнать об этом лучше от теста, чем от пользователя с нестандартным набором.
/// Порядок правил внутри фазы задан диспетчером и снаружи не меняется — переставлять нечего.
/// Прогон каждого правила в одиночку проверяет то, ради чего перестановку и затевали бы:
/// независимость правила от соседей по фазе.
/// </remarks>
public class EveryRuleTests
{
    /// <summary>Имена всех правил реестра. Имя, а не RuleId: xunit печатает его в отчёте.</summary>
    public static TheoryData<string> RuleNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (RuleId rule in RuleId.Registry.All)
            {
                data.Add(rule.Name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(RuleNames))]
    public void RuleAloneDoesNotThrow(string name)
    {
        var html = new HtmlTypographer(new HtmlOptions { Rules = Single(name) });
        var text = new TextTypographer(new TextOptions { Rules = Single(name) });

        foreach (string source in Inputs())
        {
            Assert.NotNull(html.Process(source));
            Assert.NotNull(text.Process(source));
        }
    }

    // Идемпотентность каждого правила в одиночку. Исключения — те же три, что у гарантии 5:
    // replaceNbsp снимает авторский неразрывный пробел и восстановить его нечем, nbr
    // вкладывает теги переноса друг в друга, а у escape неподвижной точки нет по
    // определению — амперсанд, ставший «&amp;», станет «&amp;amp;».
    [Theory]
    [MemberData(nameof(RuleNames))]
    public void RuleAloneIsIdempotent(string name)
    {
        if (name is "common/nbsp/replaceNbsp" or "common/html/nbr" or "common/html/escape")
        {
            return;
        }

        var typographer = new HtmlTypographer(new HtmlOptions { Rules = Single(name) });

        foreach (string source in Inputs())
        {
            string once = typographer.Process(source);
            Assert.Equal(once, typographer.Process(once));
        }
    }

    // Гарантия 3: разметка выходит байт-в-байт. Правила, которые разметку создают или
    // преобразуют намеренно, перечислены реестром и из проверки исключены.
    [Theory]
    [MemberData(nameof(RuleNames))]
    public void RuleAloneKeepsMarkupIntact(string name)
    {
        RuleId rule = Parse(name);
        if (Array.IndexOf(RuleId.Registry.MarkupChanging, rule) >= 0)
        {
            return;
        }

        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.None.With(rule) });

        foreach (string source in Inputs())
        {
            Assert.Equal(Tags(source), Tags(typographer.Process(source)));
        }
    }

    // Гарантия 6: правило, которому в тексте не за что зацепиться, возвращает исходный
    // экземпляр строки. Текст подобран так, что в нём нет ни одного знака, интересного хотя
    // бы одному правилу: только буквы и одиночные пробелы, все слова длиннее трёх букв.
    // Исключений два, и оба — правила без символа-триггера: insertFinalNewline дописывает
    // перевод строки в конец ЛЮБОГО текста, у которого его нет, а html/p оборачивает в
    // абзац ЛЮБОЙ текст без блочной разметки. «Не за что зацепиться» для них не бывает.
    [Theory]
    [MemberData(nameof(RuleNames))]
    public void RuleAloneLeavesNeutralTextAlone(string name)
    {
        if (name is "common/space/insertFinalNewline" or "common/html/p")
        {
            return;
        }

        const string Neutral = "простое предложение написанное словами длиннее четырёх букв";

        Assert.Same(Neutral, new HtmlTypographer(new HtmlOptions { Rules = Single(name) }).Process(Neutral));
    }

    // Реестр — таблица с двумя ключами: имя используется в RuleId.TryParse, индекс — как
    // позиция бита в RuleSet. Совпадение любого из них делает два правила одним.
    [Fact]
    public void RegistryKeysAreUnique()
    {
        Assert.Equal(
            RuleId.Registry.All.Length,
            RuleId.Registry.All.Select(rule => rule.Name).Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(
            RuleId.Registry.All.Length,
            RuleId.Registry.All.Select(rule => rule.Index).Distinct().Count());
    }

    // Ноль зарезервирован за default(RuleId): значение по умолчанию не должно совпадать
    // ни с одним настоящим правилом.
    [Fact]
    public void NoRuleTakesTheReservedIndex()
        => Assert.DoesNotContain(RuleId.Registry.All, rule => rule.Index == 0);

    private static RuleId Parse(string name)
    {
        Assert.True(RuleId.TryParse(name, out RuleId rule), $"Правила «{name}» нет в реестре.");
        return rule;
    }

    private static RuleSet Single(string name) => RuleSet.None.With(Parse(name));

    /// <summary>Входы: файловый корпус плюс строчки-ловушки из HardCases.</summary>
    private static IEnumerable<string> Inputs()
    {
        foreach (string name in CorpusFiles.EnumerateNames())
        {
            yield return CorpusFiles.ReadInput(name);
        }

        foreach (TheoryDataRow<string> row in HardCases.All)
        {
            yield return row.Data;
        }
    }

    /// <summary>Все сегменты разметки подряд — то, что гарантия 3 обещает не менять.</summary>
    /// <remarks>
    /// Разбор нарочно наивный и независимый от <c>MarkupScanner</c>: тест обязан ловить
    /// расхождение с ожиданием, а не повторять логику проверяемого кода.
    /// </remarks>
    private static string Tags(string html)
    {
        var tags = new StringBuilder();
        for (int i = 0; i < html.Length - 1; i++)
        {
            if (html[i] != '<' || !(char.IsLetter(html[i + 1]) || html[i + 1] == '/'))
            {
                continue;
            }

            int end = html.IndexOf('>', i);
            if (end < 0)
            {
                break;
            }

            tags.Append(html, i, end - i + 1).Append('\n');
            i = end;
        }

        return tags.ToString();
    }
}
