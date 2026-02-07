using Xunit;

namespace QuanTAlib.Tests;

public class RocrValidationTests
{
    private const double Epsilon = 1e-10;

    #region Mathematical Validation

    [Fact]
    public void Rocr_ManualCalculation_MatchesExpected()
    {
        // Manual test: ROCR = current / past
        var rocr = new Rocr(3);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 115, 120, 125 };

        for (int i = 0; i < values.Length; i++)
        {
            var result = rocr.Update(new TValue(time.AddSeconds(i), values[i]), true);

            if (i >= 3)
            {
                // After warmup, should return ratio
                double expected = values[i] / values[i - 3];
                Assert.Equal(expected, result.Value, 10);
            }
            else
            {
                // During warmup, should return 1.0
                Assert.Equal(1.0, result.Value, 10);
            }
        }
    }

    [Fact]
    public void Rocr_TenPercentIncrease_Returns1Point1()
    {
        var rocr = new Rocr(1); // 1-period lookback
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), 110.0), true);

        // 110 / 100 = 1.10
        Assert.Equal(1.10, result.Value, 10);
    }

    [Fact]
    public void Rocr_TenPercentDecrease_Returns0Point9()
    {
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), 90.0), true);

        // 90 / 100 = 0.90
        Assert.Equal(0.90, result.Value, 10);
    }

    [Fact]
    public void Rocr_ConversionToRocp_IsCorrect()
    {
        // ROCP = (ROCR - 1) * 100
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), 115.0), true);

        double rocp = (result.Value - 1.0) * 100.0;
        // 115/100 = 1.15, ROCP = (1.15 - 1) * 100 = 15%
        Assert.Equal(15.0, rocp, 10);
    }

    [Fact]
    public void Rocr_ConversionFromChange_IsCorrect()
    {
        // CHANGE = (current - past) / past = ROCR - 1
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), 125.0), true);

        double change = result.Value - 1.0;
        // 125/100 = 1.25, CHANGE = 0.25 = 25% increase
        Assert.Equal(0.25, change, 10);
    }

    #endregion

    #region Relationship to ROC

    [Fact]
    public void Rocr_RelationshipToRoc_IsCorrect()
    {
        // ROC = current - past
        // ROCR = current / past
        // If we know ROC and past, we can verify: ROCR = (ROC + past) / past = 1 + ROC/past

        var rocr = new Rocr(2);
        var roc = new Roc(2);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 120, 115 };

        for (int i = 0; i < values.Length; i++)
        {
            rocr.Update(new TValue(time.AddSeconds(i), values[i]), true);
            roc.Update(new TValue(time.AddSeconds(i), values[i]), true);
        }

        // For last value: ROCR = current/past, ROC = current - past
        // past = values[3] = 110, current = values[4] = 115
        // ROCR = 115/110, ROC = 115 - 110 = 5
        // Relationship: ROCR = (past + ROC) / past = 1 + ROC/past
        double expectedRelationship = 1.0 + roc.Last.Value / values[2];
        Assert.Equal(expectedRelationship, rocr.Last.Value, 10);
    }

    #endregion

    #region Compounding Property

    [Fact]
    public void Rocr_Compounding_MultiplyForTotalChange()
    {
        // ROCR values can be multiplied to get total change
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 110, 121, 133.1 }; // ~10% increase each period
        double compound = 1.0;

        for (int i = 0; i < values.Length; i++)
        {
            var result = rocr.Update(new TValue(time.AddSeconds(i), values[i]), true);
            if (i > 0)
            {
                compound *= result.Value;
            }
        }

        // Total change from 100 to 133.1 = 1.331
        double expectedTotal = values[^1] / values[0];
        Assert.Equal(expectedTotal, compound, 5);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Rocr_SmallValues_MaintainsPrecision()
    {
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 0.0001), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), 0.00015), true);

        // 0.00015 / 0.0001 = 1.5
        Assert.Equal(1.5, result.Value, 5);
    }

    [Fact]
    public void Rocr_LargeValues_MaintainsPrecision()
    {
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 1_000_000), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), 1_100_000), true);

        // 1_100_000 / 1_000_000 = 1.1
        Assert.Equal(1.1, result.Value, 10);
    }

    [Fact]
    public void Rocr_NegativeValues_HandlesCorrectly()
    {
        // Negative values can occur in spreads, basis, etc.
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, -100.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), -50.0), true);

        // -50 / -100 = 0.5
        Assert.Equal(0.5, result.Value, 10);
    }

    [Fact]
    public void Rocr_MixedSigns_HandlesCorrectly()
    {
        var rocr = new Rocr(1);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, -100.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(1), 100.0), true);

        // 100 / -100 = -1.0
        Assert.Equal(-1.0, result.Value, 10);
    }

    #endregion

    #region Batch vs Streaming Consistency

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streamingRocr = new Rocr(5);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            var tv = streamingRocr.Update(new TValue(source[i].Time, source[i].Value), true);
            streamingResults.Add(tv.Value);
        }

        // Batch
        var batchResult = Rocr.Calculate(source, 5);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], 10);
        }
    }

    #endregion
}
