using Typographer.Cli;

namespace Typographer.Integrations.Tests;

public class CliTests
{
    private const string Nbsp = "\u00A0";

    [Fact]
    public void Run_WithoutFilesReadsStandardInput()
    {
        (int code, string output, _) = Run([], "Он - человек");

        Assert.Equal(0, code);
        Assert.Equal($"Он{Nbsp}— человек", output);
    }

    [Fact]
    public void Run_DefaultsToHtmlMode()
    {
        (_, string output, _) = Run([], "<code>a - b</code>");

        Assert.Contains("a - b", output);
    }

    [Fact]
    public void Run_TextOptionDisablesMarkupProtection()
    {
        (_, string output, _) = Run(["--text"], "<code>a - b</code>");

        Assert.Contains("—", output);
    }

    [Fact]
    public void Run_EntitiesOptionChangesEntityForm()
    {
        (int code, string output, _) = Run(["--entities", "named"], "\"цитата\"");

        Assert.Equal(0, code);
        Assert.Contains("&laquo;", output);
    }

    [Fact]
    public void Run_NonePresetChangesNothing()
    {
        (_, string output, _) = Run(["--rules", "none"], "Он - человек");

        Assert.Equal("Он - человек", output);
    }

    [Fact]
    public void Run_DisableOptionTurnsRuleOff()
    {
        (int code, string output, _) = Run(["--disable", "ru/dash/main"], "Он - человек");

        Assert.Equal(0, code);
        Assert.DoesNotContain("—", output);
    }

    [Fact]
    public void Run_UnknownRuleIsError()
    {
        (int code, _, string error) = Run(["--disable", "ru/нет/такого"], "текст");

        Assert.Equal(1, code);
        Assert.Contains("ru/нет/такого", error);
    }

    [Fact]
    public void Run_UnknownOptionIsError()
    {
        (int code, _, string error) = Run(["--чего-нет"], "текст");

        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void Run_OptionWithoutValueIsError()
    {
        (int code, _, string error) = Run(["--entities"], "текст");

        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void Run_HelpGoesToOutputAndReturnsZero()
    {
        (int code, string output, _) = Run(["--help"]);

        Assert.Equal(0, code);
        Assert.Contains("dotnet-typographer", output);
    }

    [Fact]
    public void Run_VersionGoesToOutput()
    {
        (int code, string output, _) = Run(["--version"]);

        Assert.Equal(0, code);
        Assert.NotEqual(string.Empty, output.Trim());
    }

    [Fact]
    public void Run_RuleListContainsJsTypografNames()
    {
        (int code, string output, _) = Run(["--list-rules"]);

        Assert.Equal(0, code);
        Assert.Contains("ru/dash/main", output);
    }

    [Fact]
    public void Run_FileIsReadAndWrittenToOutput()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "Он - человек");
        try
        {
            (int code, string output, _) = Run([path]);

            Assert.Equal(0, code);
            Assert.Contains("—", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_InPlaceOptionOverwritesFile()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "Он - человек");
        try
        {
            (int code, string output, _) = Run(["--in-place", path]);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, output);
            Assert.Contains("—", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_MissingFileIsError()
    {
        (int code, _, string error) = Run([Path.Combine(Path.GetTempPath(), "нет-такого-файла.txt")]);

        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, error);
    }

    private static (int Code, string Output, string Error) Run(string[] args, string input = "")
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = CommandLine.Run(args, new StringReader(input), output, error);
        return (code, output.ToString(), error.ToString());
    }
}
