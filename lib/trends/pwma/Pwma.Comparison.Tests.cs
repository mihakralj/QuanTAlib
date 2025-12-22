using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class PwmaComparisonTests
{
    private readonly ITestOutputHelper _output;

    public PwmaComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Compare_O1_vs_Naive()
    {
        int period = 50;
        int count = 5000;
        var random = new Random(123);
        var data = new List<double>();
        for (int i = 0; i < count; i++) data.Add(random.NextDouble() * 100);

        // 1. QuanTAlib O(1)
        var pwma = new Pwma(period);
        var qResults = new List<double>();
        foreach (var val in data)
        {
            qResults.Add(pwma.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        // 2. Naive O(N)
        var nResults = new List<double>();
        for (int i = 0; i < count; i++)
        {
            if (i < period - 1)
            {
                // Warmup logic matching QuanTAlib
                double sumSq = 0;
                double wSum = 0;
                for (int j = 0; j <= i; j++)
                {
                    double weight = (j + 1) * (j + 1);
                    wSum += data[j] * weight;
                    sumSq += weight;
                }
                nResults.Add(wSum / sumSq);
            }
            else
            {
                double sumSq = 0;
                double wSum = 0;
                for (int j = 0; j < period; j++)
                {
                    double weight = (j + 1) * (j + 1);
                    wSum += data[i - period + 1 + j] * weight;
                    sumSq += weight;
                }
                nResults.Add(wSum / sumSq);
            }
        }

        // Compare
        double maxDiff = 0;
        for (int i = 0; i < count; i++)
        {
            double diff = Math.Abs(qResults[i] - nResults[i]);
            if (diff > maxDiff) maxDiff = diff;
        }

        _output.WriteLine($"Max Difference between O(1) and Naive O(N): {maxDiff:F10}");
        Assert.True(maxDiff < 1e-9, $"O(1) implementation diverged too much: {maxDiff}");
    }
}
