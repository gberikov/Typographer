namespace Typographer.Tests;

public class RussianTypographerTests
{
    [Fact]
    public void Format_LeavesPlainTextUnchanged() =>
        Assert.Equal("Привет, мир", RussianTypographer.Format("Привет, мир"));
}
