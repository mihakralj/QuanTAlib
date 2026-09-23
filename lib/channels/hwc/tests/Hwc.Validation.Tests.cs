namespace QuanTAlib.Tests;

/// <summary>
/// Formula validation for HWC (Holt-Winter Channel). No external library implements this
/// adaptive-volatility Holt-Winters channel, so this test independently re-derives the
/// documented triple-exponential HWMA forecast plus EMA-smoothed squared-error band width,
/// and checks it bar-by-bar against the streaming implementation.
/// </summary>
public sealed class HwcValidationTests : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private bool _disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    [Theory]
    [InlineData(20, 1.0)]
    [InlineData(10, 2.0)]
    public void Hwc_MatchesIndependentFormulaReimplementation(int period, double multiplier)
    {
        double[] data = _testData.RawData.ToArray();
        var hwc = new Hwc(period, multiplier);

        double alpha = 2.0 / (period + 1.0);
        double beta = 1.0 / period;
        double gamma = 1.0 / period;

        double f = 0, v = 0, a = 0, filt = 0;
        bool initialized = false;

        int count = data.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = 0; i < count; i++)
        {
            hwc.Update(new TValue(DateTime.UtcNow.AddSeconds(i), data[i]));

            double expectedMiddle, expectedUpper, expectedLower;
            if (!initialized)
            {
                f = data[i]; v = 0; a = 0; filt = 0;
                initialized = true;
                expectedMiddle = data[i];
                expectedUpper = data[i];
                expectedLower = data[i];
            }
            else
            {
                double prevF = f, prevV = v, prevA = a;
                double forecast = prevF + prevV + (0.5 * prevA);

                double newF = (forecast * (1 - alpha)) + (alpha * data[i]);
                double newV = ((prevV + prevA) * (1 - beta)) + (beta * (newF - prevF));
                double newA = (prevA * (1 - gamma)) + (gamma * (newV - prevV));

                double result = newF + newV + (0.5 * newA);

                double err = data[i] - forecast;
                double newFilt = (err * err * alpha) + (filt * (1 - alpha));

                double band = multiplier * Math.Sqrt(newFilt);
                expectedMiddle = result;
                expectedUpper = result + band;
                expectedLower = result - band;

                f = newF; v = newV; a = newA; filt = newFilt;
            }

            if (i >= start)
            {
                Assert.Equal(expectedMiddle, hwc.Middle.Value, precision: 8);
                Assert.Equal(expectedUpper, hwc.Upper.Value, precision: 8);
                Assert.Equal(expectedLower, hwc.Lower.Value, precision: 8);
            }
        }
    }

    [Fact]
    public void Hwc_UpperAlwaysAboveLower()
    {
        double[] data = _testData.RawData.ToArray();
        var hwc = new Hwc(20, 1.5);

        foreach (double v in data)
        {
            hwc.Update(new TValue(DateTime.UtcNow, v));
            Assert.True(hwc.Upper.Value >= hwc.Lower.Value);
            Assert.True(hwc.Upper.Value >= hwc.Middle.Value);
            Assert.True(hwc.Lower.Value <= hwc.Middle.Value);
        }
    }
}
