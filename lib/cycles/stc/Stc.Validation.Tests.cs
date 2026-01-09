using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class StcValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public StcValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        _testData.Dispose();
    }

    [Fact]
    public void Validate_Skender_Stc()
    {
        // Skender defaults: cycle=10, fast=23, slow=50
        int cycle = 10;
        int fast = 23;
        int slow = 50;

        var sResult = _testData.SkenderQuotes.GetStc(cycle, fast, slow).ToList();
        var sMacd = _testData.SkenderQuotes.GetMacd(fast, slow, 9).ToList();

        // Standard STC uses factor 0.5, which corresponds to dPeriod=3 (2/(3+1)=0.5)
        // Skender recommends S+C+250 warmup. 50+10+250 = 310.
        int skip = 310; 

        // Debug mode: Check d=3 specifically and print values
        int debugD = 3;
        var qStc = new Stc(kPeriod: cycle, dPeriod: debugD, fastLength: fast, slowLength: slow, smoothing: StcSmoothing.Ema);
        var qResult = qStc.Update(_testData.Data);

        double sumSq = 0;
        int count = 0;
        _output.WriteLine("");
        _output.WriteLine($"Debugging comparison for d={debugD} (Standard)");
        _output.WriteLine("Index | Skender | QuanTAlib | Diff");
        _output.WriteLine("-----------------------------------");
        
        bool foundMismatch = false;
        double maxDiff = 0;
        int maxDiffIndex = -1;

        // Print MACD comparison for critical range
        _output.WriteLine("");
        _output.WriteLine("MACD Comparison (Indices 328-335)");
        _output.WriteLine("Idx | SkenderMACD");
        for (int i = 328; i <= 335; i++)
        {
            var sm = sMacd[i].Macd ?? double.NaN;
            _output.WriteLine($"{i,3} | {sm,11:F4}");
        }
        _output.WriteLine("");

        for (int i = skip; i < qResult.Count; i++)
        {
           double sVal = sResult[i].Stc ?? double.NaN;
           double qVal = qResult[i].Value;
           double diff = Math.Abs(sVal - qVal);

           if (diff > maxDiff)
           {
               maxDiff = diff;
               maxDiffIndex = i;
           }

           if (!foundMismatch && diff > 2.0)
           {
               _output.WriteLine("First Mismatch > 2.0 found:");
               for (int j = Math.Max(skip, i - 5); j < Math.Min(qResult.Count, i + 5); j++)
               {
                   var sv = sResult[j].Stc ?? double.NaN;
                   var qv = qResult[j].Value;
                   _output.WriteLine($"{j,5} | {sv,7:F2} | {qv,9:F2} | {sv-qv,6:F2}");
               }
               foundMismatch = true;
           }

           if (!double.IsNaN(sVal) && !double.IsNaN(qVal))
           {
               sumSq += (sVal - qVal) * (sVal - qVal);
               count++;
           }
        }
        double rmse = Math.Sqrt(sumSq / count);
        _output.WriteLine($"RMSE: {rmse:F4}");
        _output.WriteLine($"Max Diff: {maxDiff:F4} at index {maxDiffIndex}");

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Stc, skip: skip, tolerance: 2.0);
    }
}
