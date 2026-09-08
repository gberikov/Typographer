using Typographer.Rules;

namespace Typographer.Internal;

/// <summary>Фаза Scan: посимвольное применение правил к текстовому узлу.</summary>
internal static class TextScanner
{
    public static void Run(ReadOnlySpan<char> source, RuleSet rules, ref CharBuffer buffer)
        => buffer.Write(source);
}
