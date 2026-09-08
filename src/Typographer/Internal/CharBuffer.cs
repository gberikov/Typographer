using System.Buffers;

namespace Typographer.Internal;

/// <summary>
/// Растущий буфер символов поверх <see cref="ArrayPool{T}"/>.
/// Разрешает патчить уже записанные позиции — на этом построена однопроходность конвейера.
/// Изменяемая структура: передавать только по ссылке.
/// </summary>
internal struct CharBuffer
{
    private char[] _array;
    private int _length;
    private readonly int _maxLength;

    public CharBuffer(int initialCapacity, int maxLength = 0)
    {
        _array = ArrayPool<char>.Shared.Rent(Math.Max(initialCapacity, 16));
        _length = 0;
        _maxLength = maxLength;
    }

    public readonly int Length => _length;

    public readonly char CharAt(int index) => _array[index];

    public readonly ReadOnlySpan<char> AsSpan() => _array.AsSpan(0, _length);

    public void Write(char value)
    {
        EnsureCapacity(1);
        _array[_length++] = value;
    }

    public void Write(ReadOnlySpan<char> value)
    {
        EnsureCapacity(value.Length);
        value.CopyTo(_array.AsSpan(_length));
        _length += value.Length;
    }

    public readonly void PatchAt(int index, char value) => _array[index] = value;

    public void Dispose()
    {
        char[]? array = _array;
        _array = null!;
        _length = 0;
        if (array is not null)
        {
            ArrayPool<char>.Shared.Return(array);
        }
    }

    private void EnsureCapacity(int additional)
    {
        int required = _length + additional;
        if (_maxLength > 0 && required > _maxLength)
        {
            throw new OutputTooLargeException(_maxLength);
        }

        if (required <= _array.Length)
        {
            return;
        }

        int capacity = _array.Length * 2;
        while (capacity < required)
        {
            capacity *= 2;
        }

        char[] grown = ArrayPool<char>.Shared.Rent(capacity);
        _array.AsSpan(0, _length).CopyTo(grown);
        ArrayPool<char>.Shared.Return(_array);
        _array = grown;
    }
}
