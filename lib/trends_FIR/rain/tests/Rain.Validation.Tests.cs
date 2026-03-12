using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class RainValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;
    private const int BarCount = 1000;
    private const int DefaultPeriod = 10;
    private const double Tolerance = 1e-9;
    private bool _disposed;

    public RainValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _gbm = new GBM();
        _bars = _gbm.Fetch(BarCount, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Validates RAIN against a naive reference implementation:
    /// 10 cascaded SMAs with weighted average [5,4,3,2,1,1,1,1,1,1]/20
    /// </summary>
    [Fact]
    public void Rain_MatchesNaiveReference_Batch()
    {
        double[] closes = new double[BarCount];
        for (int i = 0; i < BarCount; i++)
        {
            closes[i] = _bars[i].Close;
        }

        // QuanTAlib RAIN
        double[] rainOutput = new double[BarCount];
        Rain.Batch((ReadOnlySpan<double>)closes, rainOutput, DefaultPeriod);

        // Naive reference: 10 cascaded SMAs
        double[] layer0 = NaiveSma(closes, DefaultPeriod);
        double[] layer1 = NaiveSma(layer0, DefaultPeriod);
        double[] layer2 = NaiveSma(layer1, DefaultPeriod);
        double[] layer3 = NaiveSma(layer2, DefaultPeriod);
        double[] layer4 = NaiveSma(layer3, DefaultPeriod);
        double[] layer5 = NaiveSma(layer4, DefaultPeriod);
        double[] layer6 = NaiveSma(layer5, DefaultPeriod);
        double[] layer7 = NaiveSma(layer6, DefaultPeriod);
        double[] layer8 = NaiveSma(layer7, DefaultPeriod);
        double[] layer9 = NaiveSma(layer8, DefaultPeriod);

        // Weighted average: [5,4,3,2,1,1,1,1,1,1]/20
        double[] expected = new double[BarCount];
        for (int i = 0; i < BarCount; i++)
        {
            expected[i] = ((5.0 * layer0[i]) + (4.0 * layer1[i]) + (3.0 * layer2[i]) + (2.0 * layer3[i])
                + layer4[i] + layer5[i] + layer6[i] + layer7[i] + layer8[i] + layer9[i]) / 20.0;
        }

        // Compare after all layers are fully warmed (10 * period = 100)
        int warmup = DefaultPeriod * 10;
        double maxDiff = 0;
        for (int i = warmup; i < BarCount; i++)
        {
            double diff = Math.Abs(rainOutput[i] - expected[i]);
            if (diff > maxDiff)
            {
                maxDiff = diff;
            }

            Assert.True(diff < Tolerance,
                $"Bar {i}: RAIN={rainOutput[i]:F12}, Expected={expected[i]:F12}, Diff={diff:E3}");
        }

        _output.WriteLine($"RAIN vs Naive Reference: maxDiff={maxDiff:E3} (tolerance={Tolerance:E1})");
    }

    [Fact]
    public void Rain_StreamingMatchesBatch()
    {
        double[] closes = new double[BarCount];
        for (int i = 0; i < BarCount; i++)
        {
            closes[i] = _bars[i].Close;
        }

        // Batch
        double[] batchOutput = new double[BarCount];
        Rain.Batch((ReadOnlySpan<double>)closes, batchOutput, DefaultPeriod);

        // Streaming
        var rain = new Rain(DefaultPeriod);
        double[] streamOutput = new double[BarCount];
        for (int i = 0; i < BarCount; i++)
        {
            var result = rain.Update(new TValue(_bars[i].Time, closes[i]));
            streamOutput[i] = result.Value;
        }

        double maxDiff = 0;
        for (int i = 0; i < BarCount; i++)
        {
            double diff = Math.Abs(batchOutput[i] - streamOutput[i]);
            if (diff > maxDiff)
            {
                maxDiff = diff;
            }

            Assert.True(diff < Tolerance,
                $"Bar {i}: Batch={batchOutput[i]:F12}, Stream={streamOutput[i]:F12}, Diff={diff:E3}");
        }

        _output.WriteLine($"RAIN Batch vs Streaming: maxDiff={maxDiff:E3} (tolerance={Tolerance:E1})");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Rain_DifferentPeriods_AllConsistent(int period)
    {
        double[] closes = new double[BarCount];
        for (int i = 0; i < BarCount; i++)
        {
            closes[i] = _bars[i].Close;
        }

        double[] batchOutput = new double[BarCount];
        Rain.Batch((ReadOnlySpan<double>)closes, batchOutput, period);

        var rain = new Rain(period);
        for (int i = 0; i < BarCount; i++)
        {
            rain.Update(new TValue(_bars[i].Time, closes[i]));
        }

        Assert.Equal(rain.Last.Value, batchOutput[^1], Tolerance);
        _output.WriteLine($"Period {period}: Last={rain.Last.Value:F10}");
    }

    [Fact]
    public void Rain_ConstantInput_ConvergesToConstant()
    {
        const double constant = 42.0;
        const int period = 5;

        var rain = new Rain(period);
        for (int i = 0; i < 200; i++)
        {
            rain.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constant));
        }

        // After convergence, RAIN of a constant should be the constant
        Assert.Equal(constant, rain.Last.Value, 1e-10);
    }

    /// <summary>
    /// Naive SMA (N-point) for validation. Uses expanding window during warmup.
    /// </summary>
    private static double[] NaiveSma(double[] source, int period)
    {
        double[] result = new double[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            int start = Math.Max(0, i - period + 1);
            int count = i - start + 1;
            double sum = 0;
            for (int j = start; j <= i; j++)
            {
                sum += source[j];
            }
            result[i] = sum / count;
        }

        return result;
    }
}
