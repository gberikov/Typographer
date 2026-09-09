using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Стыки правил фазы Scan: левый контекст читается из буфера, состояние сканера живёт
/// сквозь все текстовые сегменты документа. Проверка совпадения первого и второго прогона
/// для этих же входов идёт через корпус сложных случаев
/// (<see cref="Corpus.HardCases"/> в <see cref="Guarantees.IdempotencyTests"/>).
/// </summary>
/// <remarks>
/// Невидимые глазом символы задаются константами <see cref="Chars"/>, а не литералами.
/// </remarks>
public class ScannerContextTests
{
    private static string Text(string source) => new TextTypograf(new TextOptions
    {
        Rules = RuleSet.Default,
    }).Process(source);

    private static string Html(string source) => new HtmlTypograf(new HtmlOptions
    {
        Rules = RuleSet.Default,
    }).Process(source);

    [Fact]
    public void SpaceAfterCommaVisibleToDashRule()
        => Assert.Equal(
            $"Он{Chars.Nbsp}сказал,{Chars.Nbsp}{Chars.MDash} пойдём",
            Text("Он сказал,- пойдём"));

    [Fact]
    public void DotsGluedInBufferBecomeEllipsisImmediately()
        => Assert.Equal($"текст{Chars.Hellip}", Text("текст. .."));

    [Fact]
    public void DotsGluedInBufferMidLine()
        => Assert.Equal($"конец{Chars.Hellip} Начало", Text("конец. .. Начало"));

    [Fact]
    public void QuoteAfterCommaIsOpening()
        => Assert.Equal(
            $"он{Chars.Nbsp}сказал, {Chars.Laquo}да{Chars.Raquo}",
            Text("он сказал,\"да\""));

    [Fact]
    public void QuotesAroundLinkKeepLevel()
        => Assert.Equal(
            $"{Chars.Laquo}<a href=\"#\">Ссылка</a>{Chars.Raquo}",
            Html("\"<a href=\"#\">Ссылка</a>\""));

    [Fact]
    public void TagDoesNotLookLikeLineStart()
        => Assert.Equal("<b>А</b>- б", Html("<b>А</b>- б"));

    [Fact]
    public void TextAndHtmlModesAgreeOnDash()
        => Assert.Equal("А- б", Text("А- б"));

    // Кавычка сразу за запятой закрывающая, если уровень уже открыт: дописанный перед ней
    // пробел сделал бы её открывающей, и дальше уровни кавычек не сходятся до конца
    // документа — «Да,» превращалось в «Да, „».
    [Fact]
    public void ClosingQuoteAfterCommaGetsNoSpace()
        => Assert.Equal(
            $"{Chars.Laquo}Да,{Chars.Raquo} сказал он. {Chars.Laquo}Второй{Chars.Raquo}",
            Text("\"Да,\" сказал он. \"Второй\""));

    // Блочный тег — граница строки: за ним начинается новый абзац, даже если перевода
    // строки между тегами нет (свёрнутый HTML).
    [Fact]
    public void BlockTagStartsNewLineForQuote()
        => Assert.Equal(
            $"<p>Первый.</p><p>{Chars.Laquo}Цитата{Chars.Raquo}</p>",
            Html("<p>Первый.</p><p>\"Цитата\"</p>"));

    [Fact]
    public void BlockTagStartsNewLineForDirectSpeechDash()
        => Assert.Equal($"Привет<br />{Chars.MDash} Пока", Html("Привет<br />- Пока"));

    [Fact]
    public void ClosedListItemAlsoStartsLine()
        => Assert.Equal(
            $"<li>раз</li><li>{Chars.Laquo}два{Chars.Raquo}</li>",
            Html("<li>раз</li><li>\"два\"</li>"));

    [Fact]
    public void ScanStateSurvivesManySegments()
    {
        // Стек кавычек и последний символ живут сквозь ВСЕ сегменты: закрывающая
        // кавычка в четвёртом узле обязана закрыть уровень, открытый в первом.
        string result = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.None.With(RuleId.Common.Punctuation.Quote),
        }).Process("<i>\"</i>а<b>б</b>\"");

        Assert.Equal("<i>«</i>а<b>б</b>»", result);
    }
}
