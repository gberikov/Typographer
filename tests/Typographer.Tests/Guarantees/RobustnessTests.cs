using System.Buffers;
using Typographer.Rules;

namespace Typographer.Tests.Guarantees;

public class RobustnessTests
{
    [Theory]
    [InlineData("\ud800")]
    [InlineData("текст\udc00текст")]
    [InlineData("<<<<<<")]
    [InlineData("<a href=\"")]
    [InlineData("&&&&&")]
    [InlineData("\"\"\"\"\"\"\"\"\"\"")]
    public void DoesNotThrowOnMalformedInput(string source)
    {
        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.Default });

        Assert.NotNull(typographer.Process(source));
    }

    [Fact]
    public void RandomDataDoesNotBreakTypographer()
    {
        var random = new Random(20260908);
        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.Default });
        var chars = new char[256];

        for (int iteration = 0; iteration < 500; iteration++)
        {
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)random.Next(0x20, 0x4FF);
            }

            Assert.NotNull(typographer.Process(new string(chars)));
        }
    }

    [Fact]
    public void ExceedingLimitThrowsClearException()
    {
        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.Default, MaxOutputLength = 10 });

        Assert.Throws<OutputTooLargeException>(() => typographer.Process(new string('а', 100)));
    }

    // Отрицательный предел раньше молча означал «без ограничения»: проверка смотрела
    // только на положительные. Тот, кто вычислил предел арифметикой и получил минус,
    // должен узнать об этом сразу, а не получить типограф без предела.
    [Fact]
    public void NegativeLimitIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HtmlTypographer(new HtmlOptions { MaxOutputLength = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TextTypographer(new TextOptions { MaxOutputLength = -1 }));
    }

    // Предел — это предел РЕЗУЛЬТАТА, а не промежуточного состояния конвейера. Сканер
    // пишет две точки, прежде чем свернуть их в многоточие вместе с третьей, и предел,
    // применённый к этой записи, срабатывал на входе, который в него укладывается.
    [Fact]
    public void ShrinkingInputWithinLimitDoesNotThrow()
        => Assert.Equal("…", new TextTypographer(new TextOptions { MaxOutputLength = 1 }).Process("..."));

    [Fact]
    public void ShrinkingInputWithinLimitDoesNotThrowInHtml()
        => Assert.Equal("…", new HtmlTypographer(new HtmlOptions { MaxOutputLength = 1 }).Process("..."));

    [Fact]
    public void ExceedingLimitInTextTypographer()
    {
        var typographer = new TextTypographer(new TextOptions { MaxOutputLength = 3 });

        Assert.Throws<OutputTooLargeException>(() => typographer.Process("абвгд"));
    }

    // Предел проверяется до записи в приёмник: приёмник не должен получить половину
    // результата, за которой следует исключение.
    [Fact]
    public void SinkGetsNoPartialResultOnOverflow()
    {
        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.None, MaxOutputLength = 3 });
        var writer = new ArrayBufferWriter<char>();

        Assert.Throws<OutputTooLargeException>(() => typographer.Process("абвгд".AsSpan(), writer));
        Assert.Equal(0, writer.WrittenCount);
    }

    // Предел — на РЕЗУЛЬТАТ документа. Каждый сегмент по отдельности в предел
    // укладывается, документ целиком — нет.
    [Fact]
    public void LimitCountsWholeDocumentNotSegment()
    {
        var typographer = new HtmlTypographer(new HtmlOptions
        {
            Rules = RuleSet.None,
            MaxOutputLength = 20,
        });

        Assert.Throws<OutputTooLargeException>(
            () => typographer.Process("<b>раз</b><b>два</b><b>три</b><b>четыре</b>"));
    }

    // Гарантия 5 на документе из многих сегментов: документные проходы держат
    // состояние сквозь разметку, и второй прогон обязан ничего не изменить.
    [Fact]
    public void MultiSegmentDocumentStaysIdempotent()
    {
        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.Default });

        string once = typographer.Process("Он <i>сказал</i>: \"это <b>важно</b>, и т. д.\" - и ушёл");
        Assert.Equal(once, typographer.Process(once));
    }

    [Fact]
    public void NoEditsReturnsSameInstance()
    {
        var typographer = new HtmlTypographer(new HtmlOptions { Rules = RuleSet.Default });
        // Ни одного слова короче четырёх букв, ни кавычек, ни дефисов, ни многоточий,
        // ни повторяющихся пробелов, ни пробелов перед знаками препинания — иначе набор
        // правил по умолчанию (например, nbsp после короткого слова) внесёт правку,
        // и Assert.Same провалится по не относящейся к делу причине.
        string source = "простой текст, который типограф оставит неизменным";

        Assert.Same(source, typographer.Process(source));
    }
}
