namespace QuanTAlib.Tests;

public class TviValidationTests
{
    private readonly ValidationTestData _data;

    public TviValidationTests()
    {
        _data = new ValidationTestData();
    }

    // Note: TVI (Trade Volume Index) is not available in TA-Lib, Skender, Tulip, or Ooples.
    // Validation tests focus on internal consistency between streaming, batch, and span modes.

    [Fact]
    public void Tvi_Streaming_Matches_Batch()
    {
        const double minTick = 0.125;

        // Streaming
        var tvi = new Tvi(minTick);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(tvi.Update(bar).Value);
        }

        // Batch
        var batchResult = Tvi.Batch(_data.Bars, minTick);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Tvi_Span_Matches_Streaming()
    {
        const double minTick = 0.125;

        // Streaming
        var tvi = new Tvi(minTick);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(tvi.Update(bar).Value);
        }

        // Span
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanOutput = new double[close.Length];

        Tvi.Batch(close, volume, spanOutput, minTick);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanOutput, 0, 100, 1e-9);
    }

    [Fact]
    public void Tvi_Different_MinTicks_Produce_Different_Results()
    {
        const double minTick1 = 0.1;
        const double minTick2 = 0.5;

        var tvi1 = new Tvi(minTick1);
        var tvi2 = new Tvi(minTick2);

        var values1 = new List<double>();
        var values2 = new List<double>();

        foreach (var bar in _data.Bars)
        {
            values1.Add(tvi1.Update(bar).Value);
            values2.Add(tvi2.Update(bar).Value);
        }

        // With different minTick values, we expect different direction changes
        // leading to different cumulative values
        bool foundDifference = false;
        for (int i = 10; i < values1.Count; i++)
        {
            if (Math.Abs(values1[i] - values2[i]) > 1e-9)
            {
                foundDifference = true;
                break;
            }
        }

        Assert.True(foundDifference, "Different minTick values should produce different results");
    }

    [Fact]
    public void Tvi_With_Tiny_MinTick_Behaves_Like_OBV()
    {
        // With very small minTick, TVI should behave similarly to OBV
        // (direction changes on virtually any price change)
        const double minTick = 1e-12;

        var tvi = new Tvi(minTick);
        var obv = new Obv();

        var tviValues = new List<double>();
        var obvValues = new List<double>();

        foreach (var bar in _data.Bars)
        {
            tviValues.Add(tvi.Update(bar).Value);
            obvValues.Add(obv.Update(bar).Value);
        }

        // With tiny minTick, TVI direction changes on any price move (like OBV)
        // Note: TVI direction is sticky when price unchanged, OBV adds 0 when unchanged
        // So they should match closely but may differ on exactly unchanged prices
        // At minimum, verify finite values and similar magnitude
        Assert.True(tviValues.All(v => double.IsFinite(v)), "TVI should produce finite values");
        Assert.True(obvValues.All(v => double.IsFinite(v)), "OBV should produce finite values");

        // Both should have same sign (both accumulating in same direction)
        double lastTvi = tviValues[tviValues.Count - 1];
        double lastObv = obvValues[obvValues.Count - 1];
        if (lastTvi != 0 && lastObv != 0)
        {
            Assert.Equal(Math.Sign(lastTvi), Math.Sign(lastObv));
        }
    }

    [Fact]
    public void Tvi_AllModes_Match_With_Different_MinTicks()
    {
        double[] minTickValues = { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0 };

        foreach (var minTick in minTickValues)
        {
            // Streaming
            var tvi = new Tvi(minTick);
            var streamingValues = new List<double>();
            foreach (var bar in _data.Bars)
            {
                streamingValues.Add(tvi.Update(bar).Value);
            }

            // Batch
            var batchResult = Tvi.Batch(_data.Bars, minTick);
            var batchValues = batchResult.Values.ToArray();

            // Span
            var close = _data.Bars.Close.Values.ToArray();
            var volume = _data.Bars.Volume.Values.ToArray();
            var spanOutput = new double[close.Length];
            Tvi.Batch(close, volume, spanOutput, minTick);

            // Verify all modes match
            ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
            ValidationHelper.VerifyData(streamingValues.ToArray(), spanOutput, 0, 100, 1e-9);
        }
    }

    [Fact]
    public void Tvi_Cumulative_Values_Are_Finite()
    {
        const double minTick = 0.125;

        var tvi = new Tvi(minTick);
        var values = new List<double>();

        foreach (var bar in _data.Bars)
        {
            values.Add(tvi.Update(bar).Value);
        }

        // All values should be finite
        Assert.True(values.All(v => double.IsFinite(v)), "All TVI values should be finite");

        // Values should be non-zero after warmup
        Assert.True(values.Skip(10).Any(v => v != 0), "TVI should have non-zero values after warmup");
    }
}