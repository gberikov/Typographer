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
    public void НеБросаетНаБитомВводе(string source)
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });

        Assert.NotNull(typograf.Process(source));
    }

    [Fact]
    public void СлучайныеДанныеНеЛомаютТипограф()
    {
        var random = new Random(20260908);
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        var chars = new char[256];

        for (int iteration = 0; iteration < 500; iteration++)
        {
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)random.Next(0x20, 0x4FF);
            }

            Assert.NotNull(typograf.Process(new string(chars)));
        }
    }

    [Fact]
    public void ПревышениеПределаДаётПонятноеИсключение()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default, MaxOutputLength = 10 });

        Assert.Throws<OutputTooLargeException>(() => typograf.Process(new string('а', 100)));
    }

    [Fact]
    public void БезПравокВозвращаетТотЖеЭкземпляр()
    {
        var typograf = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.Default });
        // Ни одного слова короче четырёх букв, ни кавычек, ни дефисов, ни многоточий,
        // ни повторяющихся пробелов, ни пробелов перед знаками препинания — иначе набор
        // правил по умолчанию (например, nbsp после короткого слова) внесёт правку,
        // и Assert.Same провалится по не относящейся к делу причине.
        string source = "простой текст, который типограф оставит неизменным";

        Assert.Same(source, typograf.Process(source));
    }
}
