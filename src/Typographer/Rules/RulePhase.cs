namespace Typographer.Rules;

/// <summary>Фаза конвейера, на которой применяется правило.</summary>
public enum RulePhase
{
    /// <summary>Подготовка: BOM, переводы строк, декодирование сущностей.</summary>
    Prepare,

    /// <summary>Защита: разметка и неприкосновенные зоны.</summary>
    Protect,

    /// <summary>Посимвольный проход: кавычки, тире, пунктуация, символы.</summary>
    Scan,

    /// <summary>Словарный проход: неразрывные пробелы, сокращения, единицы.</summary>
    Bind,

    /// <summary>Компоновка: nobr, висячая пунктуация, абзацы и переносы.</summary>
    Layout,

    /// <summary>Вывод: кодирование сущностей.</summary>
    Emit,
}
