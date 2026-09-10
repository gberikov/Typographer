using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class BindAcrossMarkupTests
{
    private static string Run(string html) => new HtmlTypographer(new HtmlOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Nbsp.AfterShortWord)
            .With(RuleId.Ru.Nbsp.Abbr)
            .With(RuleId.Ru.Nbsp.Initials),
    }).Process(html);

    [Fact]
    public void WordSplitByInlineTagIsOneWord()
    {
        // «сло|во» — слово из пяти букв: короткому слову неразрывный пробел положен,
        // длинному нет. Пока состояние не жило сквозь сегменты, «во» выглядело
        // коротким словом и получало неразрывный пробел, которого в тексте нет.
        Assert.Equal("<b>сло</b>во дом", Run("<b>сло</b>во дом"));
    }

    [Fact]
    public void ShortWordSplitByInlineTagStillBinds()
    {
        // «н|а» — короткое слово, разорванное тегом: связь остаётся.
        Assert.Equal($"<b>н</b>а{Chars.Nbsp}дом", Run("<b>н</b>а дом"));
    }

    [Fact]
    public void BlockTagEndsTheWord()
    {
        // <p> — граница строки: слово за неё не продолжается. «во» закрывается
        // блочным тегом, а не пробелом, поэтому связывать его не с чем.
        Assert.Equal("<p>во</p> дом", Run("<p>во</p> дом"));
    }

    [Fact]
    public void ProtectedZoneEndsTheWord()
    {
        // Защищённая зона слово завершает: «во» после </code> — самостоятельное
        // короткое слово, и неразрывный пробел после него ставится.
        Assert.Equal(
            $"<code>сло</code>во{Chars.Nbsp}дом",
            Run("<code>сло</code>во дом"));
    }

    [Fact]
    public void WritingPassCopiesEverythingItDoesNotChange()
    {
        // Фаза стала пишущей: документ обязан выйти байт в байт, если ни одно правило не
        // сработало. Проверяется на входе, где есть все виды сегментов сразу.
        const string source = "<p title=\"a - b\">раз<!-- к --><code>x  y</code>два</p>";
        Assert.Equal(source, new HtmlTypographer(new HtmlOptions
        {
            Rules = RuleSet.None.With(RuleId.Ru.Nbsp.Initials),
        }).Process(source));
    }

    [Fact]
    public void RecycledBufferDoesNotLeakPreviousPhase()
        // Ради экономии буферов фаза пишет в буфер фазы Prepare. Если его забыли обнулить,
        // вывод начнётся с копии подготовленного документа — тест ловит именно это.
        => Assert.Equal(
            $"в{Chars.Nbsp}доме",
            new TextTypographer(new TextOptions
            {
                Rules = RuleSet.None.With(RuleId.Common.Nbsp.AfterShortWord),
            }).Process("в доме"));

    [Fact]
    public void InitialSplitByTagBindsToSurname()
    {
        // Инициал в конце документа связывается с фамилией назад — через тег.
        Assert.Equal($"Пушкин{Chars.Nbsp}<b>А.</b>", Run("Пушкин <b>А.</b>"));
    }

    /// <summary>
    /// Знак единицы измерения отбивается от числа и тогда, когда между ними стоит строчный
    /// тег. Соседство пробела со знаком считалось сравнением с концом буфера, а байты тега
    /// туда уже записаны — и «25 &lt;b&gt;°C&lt;/b&gt;» оставалось с обычным пробелом.
    /// </summary>
    [Theory]
    [InlineData("25 <b>°C</b>", "25\u00A0<b>°C</b>")]
    [InlineData("<b>25 </b>°C", "<b>25\u00A0</b>°C")]
    [InlineData("<b>25</b> °C", "<b>25</b>\u00A0°C")]
    [InlineData("50 <i>%</i>", "50\u00A0<i>%</i>")]
    // Токен между пробелом и знаком соседство разрывает и через теги тоже.
    [InlineData("30 <b>15</b>°", "30 <b>15</b>°")]
    public void UnitSignBindsToNumberThroughInlineTag(string source, string expected)
        => Assert.Equal(expected, new HtmlTypographer(new HtmlOptions
        {
            Rules = RuleSet.None.With(RuleId.Common.Nbsp.AfterNumber),
        }).Process(source));

    /// <summary>
    /// Внутри nobr неразрывный пробел не нужен: перенос запрещён самим тегом.
    /// </summary>
    [Fact]
    public void UnitSignInsideNobrStaysBreakable()
        => Assert.Equal("<nobr>25 °C</nobr>", new HtmlTypographer(new HtmlOptions
        {
            Rules = RuleSet.None.With(RuleId.Common.Nbsp.AfterNumber, RuleId.Common.Nbsp.Nowrap),
        }).Process("<nobr>25 °C</nobr>"));
}
