// Ha Validation Tests
// No external library (TA-Lib, Tulip) has a direct HA function.
// Skender and Ooples have GetHeikinAshi but validation is self-consistency.

using Xunit;

namespace QuanTAlib.Tests;

public class HaValidationTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;
    private const int DataSize = 5000;

    public HaValidationTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.5, seed: 42);
    }

    private TBarSeries GenerateBars(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void BatchAndStreaming_Match()
    {
        var bars = GenerateBars(DataSize);

        // Streaming
        var streaming = new Ha();
        var streamingBars = new List<TBar>(DataSize);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingBars.Add(streaming.UpdateBar(bars[i], isNew: true));
        }

        // Batch
        var batchResult = Ha.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingBars[i].Open, batchResult[i].Open, Tolerance);
            Assert.Equal(streamingBars[i].High, batchResult[i].High, Tolerance);
            Assert.Equal(streamingBars[i].Low, batchResult[i].Low, Tolerance);
            Assert.Equal(streamingBars[i].Close, batchResult[i].Close, Tolerance);
        }
    }

    [Fact]
    public void SpanAndStreaming_Match()
    {
        var bars = GenerateBars(DataSize);

        // Streaming
        var streaming = new Ha();
        double[] sOpen = new double[bars.Count];
        double[] sHigh = new double[bars.Count];
        double[] sLow = new double[bars.Count];
        double[] sClose = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var ha = streaming.UpdateBar(bars[i], isNew: true);
            sOpen[i] = ha.Open;
            sHigh[i] = ha.High;
            sLow[i] = ha.Low;
            sClose[i] = ha.Close;
        }

        // Span batch
        double[] haO = new double[bars.Count];
        double[] haH = new double[bars.Count];
        double[] haL = new double[bars.Count];
        double[] haC = new double[bars.Count];
        Ha.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues,
            haO, haH, haL, haC);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(sOpen[i], haO[i], Tolerance);
            Assert.Equal(sHigh[i], haH[i], Tolerance);
            Assert.Equal(sLow[i], haL[i], Tolerance);
            Assert.Equal(sClose[i], haC[i], Tolerance);
        }
    }

    [Fact]
    public void ConstantBars_ConvergeToConstant()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;
        double price = 50.0;

        TBar last = default;
        for (int i = 0; i < 100; i++)
        {
            last = indicator.UpdateBar(new TBar(time.AddMinutes(i), price, price, price, price, 1000), isNew: true);
        }

        Assert.Equal(price, last.Open, 1e-6);
        Assert.Equal(price, last.High, 1e-6);
        Assert.Equal(price, last.Low, 1e-6);
        Assert.Equal(price, last.Close, 1e-6);
    }

    [Fact]
    public void HaClose_AlwaysEqualsOHLC4()
    {
        var bars = GenerateBars(DataSize);
        var indicator = new Ha();

        for (int i = 0; i < bars.Count; i++)
        {
            var ha = indicator.UpdateBar(bars[i], isNew: true);
            double expected = bars[i].OHLC4;
            Assert.Equal(expected, ha.Close, Tolerance);
        }
    }

    [Fact]
    public void HaHighLow_AlwaysContainBody()
    {
        var bars = GenerateBars(DataSize);
        var indicator = new Ha();

        for (int i = 0; i < bars.Count; i++)
        {
            var ha = indicator.UpdateBar(bars[i], isNew: true);
            Assert.True(ha.High >= ha.Open, $"Bar {i}: High {ha.High} < Open {ha.Open}");
            Assert.True(ha.High >= ha.Close, $"Bar {i}: High {ha.High} < Close {ha.Close}");
            Assert.True(ha.Low <= ha.Open, $"Bar {i}: Low {ha.Low} > Open {ha.Open}");
            Assert.True(ha.Low <= ha.Close, $"Bar {i}: Low {ha.Low} > Close {ha.Close}");
        }
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        var bars = GenerateBars(100);
        var indicator1 = new Ha();
        var indicator2 = new Ha();

        // Run indicator1 normally
        for (int i = 0; i < bars.Count; i++)
        {
            indicator1.UpdateBar(bars[i], isNew: true);
        }

        // Run indicator2 with corrections
        for (int i = 0; i < bars.Count; i++)
        {
            indicator2.UpdateBar(bars[i], isNew: true);
            // Simulate correction
            if (i > 0 && i % 5 == 0)
            {
                indicator2.UpdateBar(bars[i], isNew: false);
            }
        }

        Assert.Equal(indicator1.LastBar.Open, indicator2.LastBar.Open, Tolerance);
        Assert.Equal(indicator1.LastBar.Close, indicator2.LastBar.Close, Tolerance);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var bars = GenerateBars(50);
        var (results, indicator) = Ha.Calculate(bars);
        Assert.True(indicator.IsHot);
        Assert.Equal(bars.Count, results.Count);
    }
}
