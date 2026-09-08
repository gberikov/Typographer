using System.Globalization;

namespace Typographer.Internal;

/// <summary>Фаза Emit: кодирование типографских символов по выбранному режиму.</summary>
internal static class Emitter
{
    public static void Encode(ReadOnlySpan<char> source, EntityMode mode, ref CharBuffer destination)
    {
        if (mode == EntityMode.Symbols)
        {
            destination.Write(source);
            return;
        }

        foreach (char c in source)
        {
            string? name = EntityTable.NameOf(c);
            bool encode = name is not null
                && (mode != EntityMode.Mixed || EntityTable.IsInvisible(c));

            if (!encode)
            {
                destination.Write(c);
                continue;
            }

            destination.Write('&');
            if (mode == EntityMode.Numeric)
            {
                destination.Write('#');
                destination.Write(((int)c).ToString(CultureInfo.InvariantCulture).AsSpan());
            }
            else
            {
                destination.Write(name.AsSpan());
            }

            destination.Write(';');
        }
    }
}
