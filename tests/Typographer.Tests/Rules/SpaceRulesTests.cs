using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class SpaceRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Space.DelRepeatSpace)
            .With(RuleId.Common.Space.DelBeforePunctuation)
            .With(RuleId.Common.Space.DelBeforeDot)
            .With(RuleId.Common.Space.DelBeforePercent)
            .With(RuleId.Common.Space.DelBetweenExclamationMarks)
            .With(RuleId.Common.Space.AfterComma)
            .With(RuleId.Common.Space.AfterColon)
            .With(RuleId.Common.Space.AfterSemicolon)
            .With(RuleId.Common.Space.AfterExclamationMark)
            .With(RuleId.Common.Space.AfterQuestionMark)
            .With(RuleId.Common.Space.BeforeBracket)
            .With(RuleId.Common.Space.Bracket)
            .With(RuleId.Common.Space.SquareBracket)
            .With(RuleId.Common.Punctuation.Hellip),
    }).Process(source);

    [Theory]
    [InlineData("два  пробела", "два пробела")]
    [InlineData("три   пробела", "три пробела")]
    public void CollapsesRepeatedSpaces(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("слово , запятая", "слово, запятая")]
    [InlineData("слово !", "слово!")]
    [InlineData("слово ?", "слово?")]
    public void RemovesSpaceBeforePunctuation(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void AddsSpaceAfterComma()
        => Assert.Equal("раз, два, три", Run("раз,два,три"));

    [Fact]
    public void LeavesCommaInNumberAlone()
        => Assert.Equal("3,14", Run("3,14"));

    [Fact]
    public void NoSpaceBetweenTwoPunctuationMarks()
        => Assert.Equal("текст,, ещё", Run("текст,,ещё"));

    [Fact]
    public void StableOnSecondPassForAdjacentPunctuation()
    {
        string once = Run("текст,,ещё");

        Assert.Equal(once, Run(once));
    }

    [Fact]
    public void NoSpaceBetweenCommaAndEllipsis()
        => Assert.Equal("раз,…два", Run("раз,...два"));

    [Fact]
    public void StableOnSecondPassForCommaBeforeEllipsis()
    {
        string once = Run("раз,...два");

        Assert.Equal(once, Run(once));
    }

    // Пробел не ставится слева от закрывающей скобки, кавычки и перевода строки: там его
    // не бывает, а перед кавычкой он к тому же делает её открывающей и сбивает счётчик
    // уровней до конца документа.
    [Theory]
    [InlineData("(а,) б", "(а,) б")]
    [InlineData("а,\nб", "а,\nб")]
    [InlineData("а,\tб", "а,\tб")]
    public void NoSpaceBeforeClosingCharsAndLineBreak(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Fact]
    public void ReplacesThreeDotsWithEllipsis()
        => Assert.Equal("вот…", Run("вот..."));

    [Fact]
    public void LeavesFourDotsAlone()
        => Assert.Equal("вот....", Run("вот...."));

    // Гарантия 4 спецификации: текст никогда не становится разметкой. Пробел перед «?» и «!»
    // удаляется правилом delBeforePunctuation, и если слева от пробела стоит «<», результат —
    // «<?» или «<!» — псевдотег, который последующие проходы (Bind, Layout, Emit) примут за
    // настоящий тег. В текстовом режиме разметки нет вовсе, поэтому его вывод — эталон.
    [Fact]
    public void DoesNotTurnLessThanIntoTagStart()
    {
        string textResult = TextTypograf.Default.Process("дом < ? и лес");
        string htmlResult = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default })
            .Process("дом < ? и лес");

        Assert.Equal(textResult, htmlResult);
        Assert.DoesNotContain("<?", htmlResult);
    }

    [Fact]
    public void DoesNotTurnLessThanIntoCommentStart()
    {
        string htmlResult = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default })
            .Process("текст < !-- не комментарий");

        Assert.DoesNotContain("<!--", htmlResult);
    }

    [Theory]
    [InlineData("текст:ещё", "текст: ещё")]
    [InlineData("раз;два", "раз; два")]
    [InlineData("Ура!Победа", "Ура! Победа")]
    [InlineData("Что?Как", "Что? Как")]
    public void AddsSpaceAfterPunctuation(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    // Двоеточие между цифрами — время или счёт, а не конец предложения.
    [InlineData("Время 10:30", "Время 10:30")]
    // Двоеточие в адресе: за ним косая черта, а не текст.
    [InlineData("http://example.com", "http://example.com")]
    public void KeepsColonInsideTimeAndUrl(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    // Восклицательный знак здесь часть оператора: пробелы вокруг него значащие.
    [InlineData("8 != 9", "8 != 9")]
    // Четыре точки — не многоточие, правило их не собирает, и приклеивать к слову не за что.
    [InlineData("текст ....", "текст ....")]
    public void KeepsSpaceWhenPunctuationIsNotPunctuation(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("слово .", "слово.")]
    [InlineData("50 %", "50%")]
    [InlineData("Ура ! ! !", "Ура!!!")]
    public void RemovesSpaceBeforeDotPercentAndExclamationMarks(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    // Точка с запятой закрывает сущность разметки: пробел разорвал бы её пополам.
    [InlineData("текст &lt;тут&gt;", "текст &lt;тут&gt;")]
    [InlineData("число &#160;тут", "число &#160;тут")]
    // Обычная точка с запятой пробел получает — амперсанда слева нет.
    [InlineData("раз ;два", "раз; два")]
    public void KeepsHtmlEntityIntact(string source, string expected)
        => Assert.Equal(expected, Run(source));

    [Theory]
    [InlineData("слово(текст)", "слово (текст)")]
    [InlineData("( текст )", "(текст)")]
    [InlineData("[ текст ]", "[текст]")]
    // Запятая перед закрывающей скобкой пробела не получает: слева от скобки его не бывает.
    [InlineData("(раз,)", "(раз,)")]
    // Пробел перед скобкой уже есть — второго не появляется.
    [InlineData("слово (текст)", "слово (текст)")]
    public void NormalizesSpacesAroundBrackets(string source, string expected)
        => Assert.Equal(expected, Run(source));

    // Знаки препинания внутри веб-адреса принадлежат адресу, а не предложению. Признак
    // адреса — «://»; он же и граница, за которой правила снова работают как обычно.
    [Theory]
    [InlineData("http://example.com/a-b?x=1&y=2")]
    [InlineData("https://example.com/a,b;c")]
    [InlineData("Сайт http://a.ru/x?y=1 и точка.")]
    public void KeepsUrlIntact(string source) => Assert.Equal(source, Run(source));

    // Адрес кончается на первом пробеле: за ним предложение снова обычное.
    [Fact]
    public void UrlEndsAtSpace()
        => Assert.Equal("http://a.ru/x?y=1 текст: ещё", Run("http://a.ru/x?y=1 текст:ещё"));

    // Рожица — не двоеточие перед словом: ни пробел слева не удаляется, ни справа
    // не дописывается. Спецификация, раздел 9: «:-)» не должен стать тире.
    [Theory]
    [InlineData("смайл :-) в конце")]
    [InlineData("смайл :-( в конце")]
    [InlineData("смайл ;) в конце")]
    [InlineData("смайл :-D в конце")]
    public void KeepsSmileyIntact(string source) => Assert.Equal(source, Run(source));

    // Двоеточие перед скобкой с текстом рожицей не считается: между ними пробел.
    [Fact]
    public void ColonBeforeSpacedBracketIsNotSmiley()
        => Assert.Equal("Пример: (текст)", Run("Пример : (текст)"));
}
