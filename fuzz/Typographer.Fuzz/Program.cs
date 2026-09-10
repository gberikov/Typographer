using System.Runtime.InteropServices;
using System.Text;
using SharpFuzz;
using Typographer;
using Typographer.Rules;

// Цель фаззинга: конвейер не имеет права выбросить исключение ни на каком входе, кроме
// объявленного OutputTooLargeException (гарантия 2, docs/spec.md, раздел 8). Всё остальное —
// баг ядра, и libFuzzer сохранит вход, на котором оно случилось.
//
// Наборы взяты крайние: None ловит дефекты каркаса — разбор разметки, сущности, буфер, —
// а All ловит дефекты самих правил. Идемпотентность здесь не проверяется: три правила
// нарушают её по своей природе (replaceNbsp, nbr, escape), и фаззеру про эти исключения
// знать негде.
var html = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.All });
var htmlBare = new HtmlTypograf(new HtmlOptions { Rules = RuleSet.None });
var text = new TextTypograf(new TextOptions { Rules = RuleSet.All });

Fuzzer.LibFuzzer.Run(bytes =>
{
    // Два прочтения одних и тех же байт, потому что интересны два разных класса входов.
    // Как UTF-8 — осмысленный текст: затравка из корпуса остаётся читаемой, и фаззер
    // мутирует настоящие предложения, а не шум.
    string decoded = Encoding.UTF8.GetString(bytes);
    html.Process(decoded);
    text.Process(decoded);

    // Как сырой UTF-16 — то, чего честное декодирование не даёт никогда: одиночные
    // суррогаты, неназначенные кодовые точки, оборванные пары. Гарантия 7 обещает, что
    // такой вход проходит насквозь без исключения и без потери символов.
    htmlBare.Process(MemoryMarshal.Cast<byte, char>(bytes).ToString());
});
