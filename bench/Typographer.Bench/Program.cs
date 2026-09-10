using BenchmarkDotNet.Running;
using Typographer.Bench;

// С аргументом «profile» — грубый профиль по группам правил секундомером: он отвечает на
// вопрос «кто виноват» за секунды. Без аргумента — честный замер BenchmarkDotNet.
if (args.Length > 0 && args[0] == "bind")
{
    BenchmarkRunner.Run<BindPhaseBenchmarks>();
    return;
}

BenchmarkRunner.Run<TypographerBenchmarks>();
