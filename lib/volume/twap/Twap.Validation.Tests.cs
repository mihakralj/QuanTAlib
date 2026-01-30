namespace QuanTAlib.Tests;

public class TwapValidationTests
{
    private readonly ValidationTestData _data;

    public TwapValidationTests()
    {
        _data = new ValidationTestData();
    }

    // Note: TWAP (Time Weighted Average Price) is not available in TA-Lib, Skender, Tulip, or Ooples.
    // Validation tests focus on internal consistency between streaming, batch, and span modes.

    [Fact]
    public void Twap_Streaming_Matches_Batch()
    {
        const int period = 20;

        // Streaming
        var twap = new Twap(period);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(twap.Update(bar).Value);
        }

        // Batch
        var batchResult = Twap.Calculate(_data.Bars, period);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Twap_Span_Matches_Streaming()
    {
        const int period = 20;

        // Extract typical prices from bars
        var typicalPrices = new double[_data.Bars.Count];
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var bar = _data.Bars[i];
            typicalPrices[i] = (bar.High + bar.Low + bar.Close) / 3.0;
        }

        // Streaming (using TValue with typical price)
        var twap = new Twap(period);
        var streamingValues = new List<double>();
        for (int i = 0; i < typicalPrices.Length; i++)
        {
            streamingValues.Add(twap.Update(new TValue(DateTime.UtcNow.AddMinutes(i), typicalPrices[i])).Value);
        }

        // Span
        var spanOutput = new double[typicalPrices.Length];
        Twap.Calculate(typicalPrices, spanOutput, period);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanOutput, 0, 100, 1e-9);
    }

    [Fact]
    public void Twap_Different_Periods_Produce_Different_Results()
    {
        const int period1 = 10;
        const int period2 = 50;

        var twap1 = new Twap(period1);
        var twap2 = new Twap(period2);

        var values1 = new List<double>();
        var values2 = new List<double>();

        foreach (var bar in _data.Bars)
        {
            values1.Add(twap1.Update(bar).Value);
            values2.Add(twap2.Update(bar).Value);
        }

        // With different periods, we expect different results at reset boundaries
        bool foundDifference = false;
        for (int i = 50; i < values1.Count; i++)
        {
            if (Math.Abs(values1[i] - values2[i]) > 1e-9)
            {
                foundDifference = true;
                break;
            }
        }

        Assert.True(foundDifference, "Different periods should produce different results");
    }

    [Fact]
    public void Twap_ZeroPeriod_Matches_RunningAverage()
    {
        // With period = 0, TWAP should be a simple running average of all values
        var twap = new Twap(period: 0);

        double sum = 0;
        int count = 0;

        foreach (var bar in _data.Bars)
        {
            double typicalPrice = (bar.High + bar.Low + bar.Close) / 3.0;
            sum += typicalPrice;
            count++;

            var result = twap.Update(bar);
            double expectedAverage = sum / count;

            Assert.Equal(expectedAverage, result.Value, 9);
        }
    }

    [Fact]
    public void Twap_AllModes_Match_With_Different_Periods()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Extract typical prices
            var typicalPrices = new double[_data.Bars.Count];
            for (int i = 0; i < _data.Bars.Count; i++)
            {
                var bar = _data.Bars[i];
                typicalPrices[i] = (bar.High + bar.Low + bar.Close) / 3.0;
            }

            // Streaming
            var twap = new Twap(period);
            var streamingValues = new List<double>();
            for (int i = 0; i < typicalPrices.Length; i++)
            {
                streamingValues.Add(twap.Update(new TValue(DateTime.UtcNow.AddMinutes(i), typicalPrices[i])).Value);
            }

            // Batch
            var batchResult = Twap.Calculate(_data.Bars, period);
            var batchValues = batchResult.Values.ToArray();

            // Span
            var spanOutput = new double[typicalPrices.Length];
            Twap.Calculate(typicalPrices, spanOutput, period);

            // Verify all modes match
            ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
            ValidationHelper.VerifyData(streamingValues.ToArray(), spanOutput, 0, 100, 1e-9);
        }
    }

    [Fact]
    public void Twap_Values_Are_Bounded()
    {
        const int period = 20;

        var twap = new Twap(period);
        var values = new List<double>();

        foreach (var bar in _data.Bars)
        {
            values.Add(twap.Update(bar).Value);
        }

        // All values should be finite
        Assert.True(values.All(v => double.IsFinite(v)), "All TWAP values should be finite");

        // TWAP should be within the price range
        double minPrice = _data.Bars.Min(b => b.Low);
        double maxPrice = _data.Bars.Max(b => b.High);

        // After warmup, TWAP should be bounded by price range
        foreach (var v in values.Skip(period))
        {
            Assert.True(v >= minPrice * 0.9 && v <= maxPrice * 1.1,
                $"TWAP {v} should be within reasonable bounds of price range [{minPrice}, {maxPrice}]");
        }
    }
}