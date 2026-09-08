using Typographer.Internal;

namespace Typographer.Tests.Internal;

public class CharBufferTests
{
    [Fact]
    public void Write_GrowsBeyondInitialCapacity()
    {
        var buffer = new CharBuffer(initialCapacity: 2);
        try
        {
            // Запись короткой части
            buffer.Write("x");
            // Запись длинной части, гарантирующей рост буфера (>16 символов)
            buffer.Write("очень длинная строка для проверки роста");
            // Проверка: обе части должны быть сохранены правильно
            Assert.Equal("xочень длинная строка для проверки роста", buffer.AsSpan().ToString());
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Fact]
    public void PatchAt_ЗаменяетУжеЗаписанныйСимвол()
    {
        var buffer = new CharBuffer(initialCapacity: 8);
        try
        {
            buffer.Write("в доме");
            buffer.PatchAt(1, ' ');
            Assert.Equal("в доме", buffer.AsSpan().ToString());
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Fact]
    public void Write_БросаетПриПревышенииПредела()
    {
        var buffer = new CharBuffer(initialCapacity: 4, maxLength: 5);
        try
        {
            Assert.Throws<OutputTooLargeException>(() => buffer.Write("шесть!"));
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
