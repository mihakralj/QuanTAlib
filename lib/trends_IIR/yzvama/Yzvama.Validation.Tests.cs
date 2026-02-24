namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for YZVAMA (Yang-Zhang Volatility Adjusted Moving Average).
/// Validates mathematical properties rather than comparing against an external library.
/// </summary>
public class YzvamaValidationTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Yzvama_ZeroVolatility_EqualsMaxLength_SMA()
    {
        const int maxLength = 30;
        var yzvama = new Yzvama(yzvShortPeriod: 3, yzvLongPeriod: 50, percentileLookback: 50, minLength: 5, maxLength: maxLength);
        var sma = new Sma(maxLength);

        // Using close-only input creates synthetic bars with O=H=L=C which yields yzv_short=0,
        // thus percentile ~ 0 and adjusted length ~= maxLength.
        var values = Enumerable.Range(1, 300).Select(i => (double)i).ToArray();
        foreach (var val in values)
        {
            var tv = new TValue(DateTime.UtcNow, val);
            yzvama.Update(tv, isNew: true);
            sma.Update(tv, isNew: true);
        }

        Assert.Equal(sma.Last.Value, yzvama.Last.Value, 1.0);
    }

    [Fact]
    public void Yzvama_ConstantInput_OutputEqualsInput()
    {
        var yzvama = new Yzvama();
        const double constantValue = 42.5;

        for (int i = 0; i < 300; i++)
        {
            yzvama.Update(new TValue(DateTime.UtcNow, constantValue), isNew: true);
        }

        Assert.Equal(constantValue, yzvama.Last.Value, Tolerance);
    }

    [Fact]
    public void Yzvama_OutputWithinInputRange()
    {
        var yzvama = new Yzvama();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double minInput = double.MaxValue;
        double maxInput = double.MinValue;
        var outputs = new List<double>();

        foreach (var bar in bars)
        {
            minInput = Math.Min(minInput, bar.Close);
            maxInput = Math.Max(maxInput, bar.Close);
            outputs.Add(yzvama.Update(bar, isNew: true).Value);
        }

        var hotOutputs = outputs.Skip(150).ToList();
        foreach (var output in hotOutputs)
        {
            Assert.True(output >= minInput - 1 && output <= maxInput + 1,
                $"Output {output} should be within input range [{minInput}, {maxInput}]");
        }
    }

    [Fact]
    public void Yzvama_VolatilitySpike_DrivesTowardMinLength()
    {
        // Prime with a stable low-volatility regime (yzv_short ~ 0 => percentile low => maxLength)
        const int percentileLookback = 20;
        const int minLength = 5;
        const int maxLength = 50;

        var yzvama = new Yzvama(yzvShortPeriod: 3, yzvLongPeriod: 50, percentileLookback: percentileLookback, minLength: minLength, maxLength: maxLength);
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < percentileLookback; i++)
        {
            var bar = new TBar(t + i, 100, 100, 100, 100, 0);
            yzvama.Update(bar, isNew: true);
        }

        // A single large-range bar ranks at the top of the volatility buffer.
        // EMA smoothing on the percentile prevents an instant snap to minLength.
        // Instead the smoothed percentile ramps gradually, placing the output
        // between the full-buffer SMA (around 100) and an instant-jump value (120).
        var spike = new TBar(t + percentileLookback, 100, 200, 50, 200, 0);
        var result = yzvama.Update(spike, isNew: true);

        // Smoothed percentile dampens the response: adjusted length is shorter than maxLength
        // but not yet at minLength. Result should be above the all-100 average and below 120.
        Assert.True(result.Value > 100.0, $"Expected output > 100 after spike, got {result.Value}");
        Assert.True(result.Value <= 120.0, $"Expected output <= 120 after spike, got {result.Value}");
    }
}

