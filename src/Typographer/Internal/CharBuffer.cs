using System.Buffers;

namespace Typographer.Internal;

/// <summary>
/// Растущий буфер символов поверх <see cref="ArrayPool{T}"/>.
/// Разрешает патчить уже записанные позиции — на этом построена однопроходность конвейера.
/// Изменяемая структура: передавать только по ссылке.
/// </summary>
internal struct CharBuffer
{
    /// <summary>Предел длины буфера: больше этого массив в .NET не бывает.</summary>
    private const int MaxCapacity = 0X7FFFFFC7;

    private char[] _array;
    private int _length;

    public CharBuffer(int initialCapacity)
    {
        _array = ArrayPool<char>.Shared.Rent(Math.Max(initialCapacity, 16));
        _length = 0;
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

    /// <summary>Отбрасывает хвост буфера до указанной длины. Ёмкость не меняется.</summary>
    public void Truncate(int length) => _length = length;

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
        // Требуемая длина тоже считается в long: у буфера, доросшего до предела массива,
        // сумма в int уходит в минус и проходит проверку как «места хватает».
        long required = (long)_length + additional;
        if (required <= _array.Length)
        {
            return;
        }

        // Рост считается в long и упирается в предел массива. В int удвоение переполняется
        // на входе около миллиарда символов: цикл уходит в бесконечный либо Rent получает
        // отрицательную длину — а гарантия 2 разрешает единственное исключение,
        // OutputTooLargeException, и никакое другое.
        long capacity = (long)_array.Length * 2;
        while (capacity < required)
        {
            capacity *= 2;
        }

        if (capacity > MaxCapacity)
        {
            capacity = MaxCapacity;
        }

        if (required > MaxCapacity)
        {
            throw new OutputTooLargeException(MaxCapacity);
        }

        char[] grown = ArrayPool<char>.Shared.Rent((int)capacity);
        _array.AsSpan(0, _length).CopyTo(grown);
        ArrayPool<char>.Shared.Return(_array);
        _array = grown;
    }
}
