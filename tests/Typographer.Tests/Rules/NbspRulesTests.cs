using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

public class NbspRulesTests
{
    private static string Run(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.None
            .With(RuleId.Common.Nbsp.AfterShortWord)
            .With(RuleId.Ru.Nbsp.Abbr)
            .With(RuleId.Ru.Nbsp.Initials),
    }).Process(source);

    [Fact]
    public void NonBreakingSpaceAfterShortWord()
        => Assert.Equal("в доме на горе", Run("в доме на горе"));

    [Fact]
    public void LongWordUntouched()
        => Assert.Equal("дерево стоит", Run("дерево стоит"));

    [Fact]
    public void AbbreviationTDIsGlued()
        => Assert.Equal("и т. д.", Run("и т. д."));

    [Fact]
    public void InitialsBindToSurname()
        => Assert.Equal("А. С. Пушкин", Run("А. С. Пушкин"));

    // Разбор токена запускался только по идущему за ним обычному пробелу, поэтому инициал
    // в конце ввода или перед знаком препинания с фамилией не связывался вовсе.
    [Fact]
    public void InitialAtEndOfInput()
        => Assert.Equal($"Пушкин{Chars.Nbsp}А.", Run("Пушкин А."));

    [Fact]
    public void InitialBeforeComma()
        => Assert.Equal($"Пушкин{Chars.Nbsp}А., автор", Run("Пушкин А., автор"));

    [Fact]
    public void InitialBeforeLineBreak()
        => Assert.Equal($"Пушкин{Chars.Nbsp}А.\nдалее", Run("Пушкин А.\nдалее"));

    [Fact]
    public void SurnameBeforeInitialsToo()
        => Assert.Equal("Пушкин А. С.", Run("Пушкин А. С."));

    // Точка слово не НАЧИНАЕТ. Словарные решения зависят от длины токена и от того, одна ли
    // в нём буква, поэтому точка, приклеенная слева, меняла вердикт: «.дом» — четыре
    // символа и уже не короткое слово, «.т» — две буквы и уже не часть сокращения, а
    // одиночная точка становилась токеном и подставляла свой пробел под связь с инициалом.
    [Fact]
    public void DotDoesNotStartShortWord()
        => Assert.Equal($"текст .дом{Chars.Nbsp}стоит", Run("текст .дом стоит"));

    [Fact]
    public void DotDoesNotStartAbbreviationPart()
        => Assert.Equal($"а{Chars.Nbsp}.т.{Chars.Nbsp}д.", Run("а .т. д."));

    [Fact]
    public void LoneDotIsNotAToken()
        => Assert.Equal($"дом{Chars.Nbsp}. А.{Chars.Nbsp}Пушкин", Run("дом . А. Пушкин"));

    // Короткое слово в конце входа связывать не с чем: неразрывный пробел, поставленный
    // вперёд, оказывался последним символом текста и оставался невидимым мусором.
    [Fact]
    public void TrailingSpaceStaysBreakable()
    {
        // Пробел перед «в» неразрывным стать может — это решение правила о коротком слове.
        // А вот пробел ПОСЛЕ него закрывает текст, и связывать его не с чем.
        Assert.EndsWith("в ", Run("дом в "), StringComparison.Ordinal);
    }
}
