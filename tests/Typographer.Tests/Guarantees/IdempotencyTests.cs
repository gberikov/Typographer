using Typographer.Internal;
using Typographer.Rules;
using Typographer.Tests.Corpus;

namespace Typographer.Tests.Guarantees;

public class IdempotencyTests
{
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void SecondPassChangesNothing_Html(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void SecondPassChangesNothing_PlainText(string source)
    {
        var typograf = new TextTypograf(new TextOptions { Rules = RuleSet.Default });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    // Набор Default проверяет то, чем пользуются; набор All — то, что вообще написано.
    // Правила вне Default (деньги, ударение, повтор слова) меняют текст сильнее прочих, и
    // разрыв идемпотентности вероятнее всего именно там.
    // Исключение — common/nbsp/replaceNbsp: оно снимает неразрывные пробелы ПЕРЕД
    // типографированием, чтобы правила расставили свои. Пробел, поставленный однократным
    // превращением («- » в тире, «руб.» в знак рубля), на втором прогоне восстановить уже
    // нечем: исходной формы в тексте нет. Это свойство самого правила, а не дефект; см.
    // docs/spec.md, гарантия 5.
    [Theory]
    [MemberData(nameof(HardCases.All), MemberType = typeof(HardCases))]
    public void SecondPassChangesNothing_AllRules(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions
        {
            Rules = RuleSet.All.Without(RuleId.Common.Nbsp.ReplaceNbsp),
        });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    // Входы подобраны так, чтобы вывод содержал сущность РЯДОМ со знаком, по которому
    // принимает решение следующее правило: «&nbsp;» сразу за запятой и «&hellip;» сразу за
    // запятой. Пока фазы Prepare не было, второй прогон видел на этом месте амперсанд, не
    // узнавал в нём пробел или знак препинания и дописывал лишний пробел.
    [Theory]
    [InlineData(EntityMode.Named, "Он сказал: \"да\" - и ушёл")]
    [InlineData(EntityMode.Named, "Он сказал,- пойдём")]
    [InlineData(EntityMode.Named, "раз,...два")]
    [InlineData(EntityMode.Numeric, "Он сказал: \"да\" - и ушёл")]
    [InlineData(EntityMode.Numeric, "Он сказал,- пойдём")]
    [InlineData(EntityMode.Numeric, "раз,...два")]
    [InlineData(EntityMode.Mixed, "Он сказал: \"да\" - и ушёл")]
    [InlineData(EntityMode.Mixed, "Он сказал,- пойдём")]
    [InlineData(EntityMode.Mixed, "раз,...два")]
    public void IdempotentWithEntityEncoding(EntityMode mode, string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default, Entities = mode });
        string once = typograf.Process(source);

        Assert.Equal(once, typograf.Process(once));
    }

    // Гарантия 5 намеренно НЕ распространяется на UseBr/MaxNobr (см. docs/spec.md, §8, и
    // XML-комментарии на этих опциях в HtmlOptions): они оборачивают текст в разметку один
    // раз, и прогон по собственному выводу типографа вкладывает разметку в саму себя. Эти
    // тесты закрепляют РЕАЛЬНОЕ поведение на повторном прогоне — оно должно быть известным и
    // проверяемым, а не сюрпризом при следующей правке.

    [Fact]
    public void UseBr_SecondPassNestsBrTag()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, UseBr = true });
        string once = typograf.Process("первая\nвторая");
        string twice = typograf.Process(once);

        Assert.Equal("первая<br />\nвторая", once);
        Assert.Equal("первая<br /><br />\nвторая", twice);
        Assert.NotEqual(once, twice);
    }

    // UseP, в отличие от UseBr и MaxNobr, идемпотентен: абзацы расставляются по документу
    // целиком, а документ, в котором блочная разметка уже есть, второй раз не размечается.
    [Fact]
    public void UseP_SecondPassDoesNotNestParagraphTag()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, UseP = true });
        string once = typograf.Process("первый\n\nвторой");

        Assert.Equal("<p>первый</p>\n<p>второй</p>", once);
        Assert.Equal(once, typograf.Process(once));
    }

    [Fact]
    public void UseP_DoesNotWrapExistingBlockMarkup()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, UseP = true });
        string source = "<ul>\n<li>раз</li>\n</ul>";

        Assert.Equal(source, typograf.Process(source));
    }

    [Fact]
    public void UseP_DoesNotBreakInlineMarkup()
        => Assert.Equal(
            "<p>раз <b>два</b></p>\n<p>три</p>",
            new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, UseP = true })
                .Process("раз <b>два</b>\n\nтри"));

    [Fact]
    public void MaxNobr_SecondPassNestsNobrTag()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None, MaxNobr = 2 });
        string once = typograf.Process($"в{Chars.Nbsp}доме на горе");
        string twice = typograf.Process(once);

        Assert.Equal($"<nobr>в{Chars.Nbsp}доме</nobr> на горе", once);
        Assert.Equal($"<nobr><nobr>в{Chars.Nbsp}доме</nobr></nobr> на горе", twice);
        Assert.NotEqual(once, twice);
    }
}
