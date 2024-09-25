using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace QuanTAlib;

public class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true);
        BenchmarkRunner.Run<EmaBenchmark>(config);
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class EmaBenchmark
{
    private const int Period = 10;
    private const int Length = 100_000;
    private GbmFeed gbm = null!;
    private TSeries inputs = null!;

    [GlobalSetup]
    public void Setup()
    {
        gbm = new GbmFeed();
        inputs = new();

        for (int i = 0; i < Length; i++)
        {
            TBar item = gbm.Generate(DateTime.Now);
            inputs.Add(new TValue(item.Time, item.Close, true, true));
        }
    }

    [Benchmark]
    public void Afirma_bench()
    {
        Afirma ma1 = new(Period);
        for (int i = 0; i < Length; i++)
        {
            TValue item = gbm.Generate(DateTime.Now).Close;
            ma1.Calc(item);
        }

    }

    [Benchmark]
    public void Alma_bench()
    {
        Alma ma1 = new(Period);
        for (int i = 0; i < Length; i++)
        {
            TValue item = gbm.Generate(DateTime.Now).Close;
            ma1.Calc(item);
        }
    }

    [Benchmark]
    public void Dema_bench()
    {
        Dema ma1 = new(Period);
        for (int i = 0; i < Length; i++)
        {
            TValue item = gbm.Generate(DateTime.Now).Close;
            ma1.Calc(item);
        }
    }

    [Benchmark]
    public void Ema_bench()
    {
        Ema ma1 = new(Period);
        for (int i = 0; i < Length; i++)
        {
            TValue item = gbm.Generate(DateTime.Now).Close;
            ma1.Calc(item);
        }
    }
}