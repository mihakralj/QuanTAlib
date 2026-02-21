using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public class Butter2ValidationTests
{
    private readonly GBM _gbm;

    public Butter2ValidationTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void ValidateAgainstReferenceImplementation()
    {
        // Generate test data
        var bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        const int period = 14;

        // 1. QuanTAlib Implementation
        var butter = new Butter2(period);
        var quantalibResult = new List<double>();
        foreach (var item in series)
        {
            quantalibResult.Add(butter.Update(item).Value);
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

    [Fact]
    public void ValidateAgainstOoples()
    {
        // Generate test data
        var bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        int period = 14;

        // 1. QuanTAlib Implementation
        var butter = new Butter2(period);
        var quantalibResult = new List<double>();
        foreach (var item in series)
        {
            quantalibResult.Add(butter.Update(item).Value);
        }

        // 2. Ooples Implementation
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = b.AsDateTime,
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var ooplesResult = stockData.CalculateEhlers2PoleButterworthFilterV2(length: period);
        var ooplesValues = ooplesResult.OutputValues.Values.First();

        // Compare
        Assert.Equal(quantalibResult.Count, ooplesValues.Count);

        // Check last 100 bars
        for (int i = quantalibResult.Count - 100; i < quantalibResult.Count; i++)
        {
            // Ooples implementation (Ehlers) deviates slightly from standard Butterworth (PineScript reference)
            // Tolerance increased to 0.2 to account for this difference.
            Assert.Equal(ooplesValues[i], quantalibResult[i], 2e-1);
        }
    }

    private static List<double> CalculateReference(TSeries source, int period)
    {
        var result = new List<double>();

        int safe_length = Math.Max(period, 2);
        double omega = 2.0 * Math.PI / safe_length;
        double sin_omega = Math.Sin(omega);
        double cos_omega = Math.Cos(omega);
        double alpha = sin_omega / Math.Sqrt(2.0);
        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cos_omega;
        double a2 = 1.0 - alpha;
        double b0 = (1.0 - cos_omega) / 2.0;
        double b1 = 1.0 - cos_omega;
        double b2 = (1.0 - cos_omega) / 2.0;

        double filt = 0;
        double filt1 = 0;
        double filt2 = 0;

        double src1 = 0;
        double src2 = 0;

        for (int i = 0; i < source.Count; i++)
        {
            double src = source[i].Value;

            if (i < 2)
            {
                filt = src;
            }
            else
            {
                double ssrc = src;
                filt = (b0 * ssrc + b1 * src1 + b2 * src2 - a1 * filt1 - a2 * filt2) / a0;
            }

            result.Add(filt);

            // Update history
            src2 = src1;
            src1 = src;

            filt2 = filt1;
            filt1 = filt;
        }

        return result;
    }
}
