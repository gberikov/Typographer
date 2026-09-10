using Typographer.Internal;
using Typographer.Rules;

namespace Typographer.Tests.Rules;

/// <summary>
/// Веб-адрес не подлежит типографике ни одним правилом фаз Scan и Bind; автоссылка фазы
/// Layout его оборачивает, но не меняет.
/// </summary>
/// <remarks>
/// Адрес — машинный идентификатор, а не текст: правка меняет, куда он ведёт. Раньше внутри
/// адреса запрещался только пробел после знака препинания, а остальные правила работали,
/// и «?v=1.5» превращалось в «?v=1,5», «/1/2» в «/½», «2020-2026» в «2020—2026». При
/// включённой автоссылке испорченный адрес попадал ещё и в href. Затем то же обнаружилось
/// у фазы Bind: «/tel/89991234567» становилось телефоном, «/м2» — квадратным метром.
/// Пресет здесь полный (<see cref="RuleSet.Lebedev"/> вместо <see cref="RuleSet.Default"/>)
/// и дополнен датой ISO и знаком валюты, живущими вне пресетов: разбиение разрядов,
/// перезапись даты и перенос знака валюты — как раз правила, которым адрес противопоказан.
/// </remarks>
public class UrlProtectionTests
{
    /// <summary>
    /// Неразрывный пробел escape-последовательностью: атрибут теста требует константного
    /// выражения, а невидимый символ в исходнике теряется при правке файла.
    /// </summary>
    private const string Nbsp = "\u00A0";

    private static string Run(string source)
        => new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.Lebedev.With(RuleId.Ru.Date.FromIso, RuleId.Ru.Money.Currency),
        }).Process(source);

    [Theory]
    // Точка между цифрами — не десятичный разделитель.
    [InlineData("https://example.com/?v=1.5")]
    // Косая черта между цифрами — не дробь.
    [InlineData("https://example.com/1/2")]
    // Дефис между годами — не тире.
    [InlineData("https://example.com/2020-2026")]
    // «!=» — не знак неравенства.
    [InlineData("https://example.com/?a=1!=2")]
    // Скобка не открывает знак «(c)» и не требует пробела перед собой.
    [InlineData("https://example.com/page(test)")]
    [InlineData("https://example.com/page(c)")]
    // Разряды внутри адреса — часть пути, а не число.
    [InlineData("https://example.com/id/1000000")]
    // Многоточие в пути остаётся тремя точками, и пробела после него не появляется.
    [InlineData("https://example.com/x...y")]
    // Наращение порядкового числительного к адресу не относится.
    [InlineData("https://example.com/25-ый")]
    // Правила фазы Bind адресу противопоказаны так же: одиннадцать цифр в пути — не
    // телефон, «м2», «г.г.» и «P.S.» — не сокращения, «2018-10-10» — не дата, «$100» — не сумма.
    [InlineData("https://example.com/tel/89991234567")]
    [InlineData("https://example.com/id/+79991234567")]
    [InlineData("https://example.com/м2/г.г./P.S.")]
    [InlineData("https://example.com/2018-10-10")]
    [InlineData("https://example.com/$100")]
    public void AddressIsCopiedByteForByte(string source)
        => Assert.Equal(source, Run(source));

    // Адрес кончается на пробеле или угловой скобке, и текст за ним — снова текст. Сам
    // адрес при этом не токен: слово за ним к его хвосту «1.5» не привязывается.
    [Theory]
    [InlineData(
        "Сайт https://a.ru/?v=1.5 и цена 1.5",
        "Сайт https://a.ru/?v=1.5 и" + Nbsp + "цена 1,5")]
    [InlineData(
        "<b>https://a.ru/1/2</b> и 1/2",
        "<b>https://a.ru/1/2</b> и" + Nbsp + "½")]
    public void RulesResumeAfterAddress(string source, string expected)
        => Assert.Equal(expected, Run(source));

    // Угловая скобка адрес завершает: в тексте «<» неотличима от начала тега, и считать её
    // частью адреса значило бы проглотить разметку. Оттого «?a=1<=2» защищённым не остаётся.
    [Fact]
    public void AngleBracketEndsTheAddress()
        => Assert.Equal($"https://a.ru/?a=1{Chars.LessOrEqual}2", Run("https://a.ru/?a=1<=2"));

    // Адрес, испорченный правилами, попадал в href — там же, где и в тексте ссылки.
    [Fact]
    public void AutolinkGetsTheOriginalAddress()
        => Assert.Equal(
            "<a href=\"https://example.com/?v=1.5\">https://example.com/?v=1.5</a>",
            new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Lebedev.With(RuleId.Common.Html.Url) })
                .Process("https://example.com/?v=1.5"));

    // Тег обрывает адрес: «http://a<b>?x» — это не адрес с вопросом, и правила за тегом
    // снова работают.
    [Fact]
    public void TagEndsTheAddress()
        => Assert.Equal("http://a<b>1,5</b>", Run("http://a<b>1.5</b>"));

    // Закрывающая типографская кавычка завершает адрес и обновляет стек кавычек: иначе
    // следующая пара прямых кавычек ошибочно считалась вложенной.
    [Fact]
    public void ClosingQuoteAfterAddressUpdatesQuoteStack()
        => Assert.Equal(
            $"«https://a.ru» и{Nbsp}«слово»",
            Run("«https://a.ru» и \"слово\""));

    // Английская закрывающая кавычка — тоже граница: запятая за ней снова знак препинания,
    // а не часть адреса.
    [Fact]
    public void EnglishClosingQuoteEndsTheAddress()
        => Assert.Equal("https://a.ru”, далее", Run("https://a.ru”,далее"));
}
