using System.Text;
using Typographer.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        // UTF-8 задаётся потокам явно, а не через Console.InputEncoding: перенаправленный
        // ввод-вывод в Windows иначе кодируется кодовой страницей консоли, и кавычки-ёлочки
        // в файле превращаются в угловые скобки. Кодовая страница самой консоли меняется
        // отдельно — иначе те же байты стали бы кашей уже на экране.
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.OutputEncoding = utf8;
            }
            catch (IOException)
            {
                // Консоли нет вовсе — работаем как есть.
            }
        }

        using var input = new StreamReader(Console.OpenStandardInput(), utf8);
        using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
        using var error = new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true };

        return CommandLine.Run(args, input, output, error);
    }
}
