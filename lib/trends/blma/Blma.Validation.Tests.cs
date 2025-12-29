using System;
using System.Collections.Generic;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class BlmaValidationTests
{
    private readonly GBM _gbm;

    public BlmaValidationTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void ValidateAgainstReferenceImplementation()
    {
        // Generate test data
        var bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        int period = 14;

        // 1. QuanTAlib Implementation
        var blma = new Blma(period);
        var quantalibResult = new List<double>();
        foreach (var item in series)
        {
            quantalibResult.Add(blma.Update(item).Value);
        }

        // 2. Reference Implementation (PineScript logic)
        var referenceResult = CalculateReference(series, period);

        // Compare
        Assert.Equal(quantalibResult.Count, referenceResult.Count);
        for (int i = 0; i < quantalibResult.Count; i++)
        {
            // Allow small difference due to float precision
            Assert.Equal(referenceResult[i], quantalibResult[i], 1e-9);
        }
    }

    private static IReadOnlyList<double> CalculateReference(TSeries source, int period)
    {
        var result = new List<double>();
        var buffer = new List<double>();

        for (int i = 0; i < source.Count; i++)
        {
            buffer.Add(source[i].Value);

            // PineScript logic:
            // int p = math.min(bar_index + 1, period)
            int p = Math.Min(buffer.Count, period);

            // Calculate weights
            var weights = new double[p];
            double totalWeight = 0;

            if (p == 1)
            {
                weights[0] = 1.0;
                totalWeight = 1.0;
            }
            else
            {
                double invPMinus1 = 1.0 / (p - 1);
                double pi2 = 2.0 * Math.PI;
                double pi4 = 4.0 * Math.PI;
                double a0 = 0.42;
                double a1 = 0.5;
                double a2 = 0.08;

                for (int j = 0; j < p; j++)
                {
                    double ratio = j * invPMinus1;
                    double w = a0 - (a1 * Math.Cos(pi2 * ratio)) + (a2 * Math.Cos(pi4 * ratio));
                    weights[j] = w;
                    totalWeight += w;
                }
            }

            // Calculate weighted sum
            double sum = 0;
            // PineScript: for i = 0 to p - 1
            // float price = source[i] (where source[0] is newest)
            // float w = array.get(weights, i)
            // So weights[0] * newest, weights[1] * 2nd newest...

            // My C# buffer is chronological (0 is oldest).
            // So buffer[buffer.Count - 1] is newest.
            // buffer[buffer.Count - 1 - j] is j-th lag.

            // Wait, in Blma.cs I implemented:
            // sum += buffer[i] * weights[i] (where buffer[0] is oldest)
            // So weights[0] * oldest.

            // PineScript: weights[0] * newest.
            // Since Blackman window is symmetric, weights[0] == weights[p-1].
            // So weights[0] * newest == weights[p-1] * newest (if symmetric).
            // But weights[0] is 0. weights[p-1] is 0.
            // weights[p/2] is peak.
            // So symmetric window applied forward or backward is the same.
            // Let's verify symmetry.
            // w(j) vs w(p-1-j).
            // ratio(j) = j/(p-1).
            // ratio(p-1-j) = (p-1-j)/(p-1) = 1 - j/(p-1) = 1 - ratio(j).
            // cos(2pi * (1-r)) = cos(2pi - 2pi*r) = cos(-2pi*r) = cos(2pi*r).
            // cos(4pi * (1-r)) = cos(4pi - 4pi*r) = cos(4pi*r).
            // So yes, w(j) == w(p-1-j).
            // So applying weights[0] to newest or oldest doesn't matter for the sum.

            // However, I should match my implementation in Blma.cs.
            // In Blma.cs: sum += buffer[i] * weights[i] (buffer[0] is oldest).
            // So weights[0] * oldest.

            // In this reference implementation, let's do the same.
            // Use the last p elements of buffer.
            int start = buffer.Count - p;
            for (int j = 0; j < p; j++)
            {
                // buffer[start + j] is the value.
                // weights[j] is the weight.
                sum += buffer[start + j] * weights[j];
            }

            result.Add(sum / totalWeight);
        }

        return result;
    }
}
