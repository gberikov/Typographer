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
    public void ПробелПослеЗапятойВиденПравилуТире()
        => Assert.Equal(
            $"Он{Chars.Nbsp}сказал,{Chars.Nbsp}{Chars.MDash} пойдём",
            Text("Он сказал,- пойдём"));

    [Fact]
    public void ТочкиСклеенныеВБуфереДаютМноготочиеСразу()
        => Assert.Equal($"текст{Chars.Hellip}", Text("текст. .."));

    [Fact]
    public void ТочкиСклеенныеВБуфереВСерединеСтроки()
        => Assert.Equal($"конец{Chars.Hellip} Начало", Text("конец. .. Начало"));

    [Fact]
    public void КавычкаПослеЗапятойОткрывающая()
        => Assert.Equal(
            $"он{Chars.Nbsp}сказал, {Chars.Laquo}да{Chars.Raquo}",
            Text("он сказал,\"да\""));

    [Fact]
    public void КавычкиВокругСсылкиНеТеряютУровень()
        => Assert.Equal(
            $"{Chars.Laquo}<a href=\"#\">Ссылка</a>{Chars.Raquo}",
            Html("\"<a href=\"#\">Ссылка</a>\""));

    [Fact]
    public void ТегНеВыглядитКакНачалоСтроки()
        => Assert.Equal("<b>А</b>- б", Html("<b>А</b>- б"));

    [Fact]
    public void ТекстовыйИHtmlРежимыСовпадаютНаТире()
        => Assert.Equal("А- б", Text("А- б"));
}
