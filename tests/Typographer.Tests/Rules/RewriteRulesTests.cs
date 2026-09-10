using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Правила-перезаписи фазы Bind: они усекают буфер до начала токена (или до начала
/// предыдущего токена, когда два сливаются в один) и пишут замену. Переписывать левее
/// <c>BindState.SafeFrom</c> нельзя — там лежит уже скопированная разметка.
/// </summary>
public class RewriteRulesTests
{
    private static string Run(string source, params RuleId[] rules)
        => new TextTypograf(new TextOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    private static string Html(string source, params RuleId[] rules)
        => new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None.With(rules) }).Process(source);

    [Fact]
    public void YearsCollapseToDoubleLetter()
    {
        Assert.Equal($"1990{Chars.Nbsp}гг.", Run("1990 г.г.", RuleId.Ru.Nbsp.Years));
        Assert.Equal($"1990{Chars.Nbsp}гг.", Run("1990 г. г.", RuleId.Ru.Nbsp.Years));
        Assert.Equal("гг.", Run("г.г.", RuleId.Ru.Nbsp.Years));
        // Переписывать нечего: неразрывный пробел здесь — забота ru/nbsp/year.
        Assert.Equal("1990 гг.", Run("1990 гг.", RuleId.Ru.Nbsp.Years));
    }

    [Fact]
    public void YearsRuleIsIdempotent()
    {
        string once = Run("1990 г. г.", RuleId.Ru.Nbsp.Years);
        Assert.Equal(once, Run(once, RuleId.Ru.Nbsp.Years));
    }

    // Токен, разорванный тегом, правилу-перезаписи недоступен: усечение съело бы байты тега
    // вопреки гарантии 3.
    [Fact]
    public void RewriteDoesNotReachBehindMarkup()
        => Assert.Equal(
            "1990 г.<b>г.</b>",
            Html("1990 г.<b>г.</b>", RuleId.Ru.Nbsp.Years));

    [Fact]
    public void CenturiesCollapseToDoubleLetter()
    {
        Assert.Equal($"XIX{Chars.Nbsp}вв.", Run("XIX в. в.", RuleId.Ru.Nbsp.Centuries));
        Assert.Equal($"XIX{Chars.Nbsp}вв.", Run("XIX в.в.", RuleId.Ru.Nbsp.Centuries));
        Assert.Equal("XIX вв.", Run("XIX вв.", RuleId.Ru.Nbsp.Centuries));
    }

    [Fact]
    public void PostScriptumGetsInnerNbsp()
    {
        Assert.Equal($"P.{Chars.Nbsp}S. текст", Run("P.S. текст", RuleId.Ru.Nbsp.Ps));
        Assert.Equal($"P.{Chars.Nbsp}S. текст", Run("P. S. текст", RuleId.Ru.Nbsp.Ps));
        Assert.Equal($"P.{Chars.Nbsp}P.{Chars.Nbsp}S. текст", Run("P.P.S. текст", RuleId.Ru.Nbsp.Ps));
    }

    [Theory]
    // Кириллические инициалы — не постскриптум: «С.» и «S.» разные символы.
    [InlineData("А. С. Пушкин")]
    [InlineData("простое слово")]
    public void PostScriptumLeavesOtherTokens(string source)
        => Assert.Equal(source, Run(source, RuleId.Ru.Nbsp.Ps));

    [Fact]
    public void SquareAndCubicMeters()
    {
        Assert.Equal($"100{Chars.Nbsp}м²", Run("100 м2", RuleId.Ru.Nbsp.M));
        Assert.Equal($"5{Chars.Nbsp}м³", Run("5 м3", RuleId.Ru.Nbsp.M));
        // Не единица, а часть слова.
        Assert.Equal("м2м", Run("м2м", RuleId.Ru.Nbsp.M));
    }
}
