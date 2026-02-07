using Xunit;

namespace QuanTAlib.Tests;

public class RocpValidationTests
{
    #region Mathematical Validation

    [Fact]
    public void Rocp_ManualCalculation_MatchesExpected()
    {
        var rocp = new Rocp(3);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 115, 120, 125 };

        for (int i = 0; i < values.Length; i++)
        {
            var result = rocp.Update(new TValue(time.AddSeconds(i), values[i]), true);

            if (i >= 3)
            {
                double expected = 100.0 * (values[i] - values[i - 3]) / values[i - 3];
                Assert.Equal(expected, result.Value, 10);
            }
            else
            {
                Assert.Equal(0.0, result.Value, 10);
            }
        }
    }

    [Fact]
    public void Rocp_FivePercentIncrease_ReturnsFive()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 105.0), true);

        Assert.Equal(5.0, result.Value, 10);
    }

    [Fact]
    public void Rocp_FivePercentDecrease_ReturnsNegativeFive()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 95.0), true);

        Assert.Equal(-5.0, result.Value, 10);
    }

    #endregion

    #region Relationship to ROCR and ROC

    [Fact]
    public void Rocp_RelationshipToRocr_IsCorrect()
    {
        // ROCP = (ROCR - 1) * 100
        var rocp = new Rocp(2);
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 120, 115 };

        for (int i = 0; i < values.Length; i++)
        {
            rocp.Update(new TValue(time.AddSeconds(i), values[i]), true);
            rocr.Update(new TValue(time.AddSeconds(i), values[i]), true);
        }

        // ROCP = (ROCR - 1) * 100
        double expectedFromRocr = (rocr.Last.Value - 1.0) * 100.0;
        Assert.Equal(expectedFromRocr, rocp.Last.Value, 10);
    }

    [Fact]
    public void Rocp_RelationshipToRoc_IsCorrect()
    {
        // ROCP = 100 * ROC / past
        var rocp = new Rocp(2);
        var roc = new Roc(2);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 120, 115 };

        for (int i = 0; i < values.Length; i++)
        {
            rocp.Update(new TValue(time.AddSeconds(i), values[i]), true);
            roc.Update(new TValue(time.AddSeconds(i), values[i]), true);
        }

        // ROCP = 100 * ROC / past
        // For last value: past = values[2] = 110
        double expectedFromRoc = 100.0 * roc.Last.Value / values[2];
        Assert.Equal(expectedFromRoc, rocp.Last.Value, 10);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Rocp_SmallValues_MaintainsPrecision()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 0.0001), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 0.00015), true);

        // 100 * (0.00015 - 0.0001) / 0.0001 = 50%
        Assert.Equal(50.0, result.Value, 5);
    }

    [Fact]
    public void Rocp_LargeValues_MaintainsPrecision()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 1_000_000), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 1_100_000), true);

        // 100 * (1_100_000 - 1_000_000) / 1_000_000 = 10%
        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Rocp_NegativeValues_HandlesCorrectly()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, -100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), -50.0), true);

        // 100 * (-50 - (-100)) / (-100) = 100 * 50 / -100 = -50%
        Assert.Equal(-50.0, result.Value, 10);
    }

    [Fact]
    public void Rocp_MixedSigns_HandlesCorrectly()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, -100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 100.0), true);

        // 100 * (100 - (-100)) / (-100) = 100 * 200 / -100 = -200%
        Assert.Equal(-200.0, result.Value, 10);
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
        var streamingRocp = new Rocp(5);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            var tv = streamingRocp.Update(new TValue(source[i].Time, source[i].Value), true);
            streamingResults.Add(tv.Value);
        }

        // Batch
        var batchResult = Rocp.Calculate(source, 5);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], 10);
        }
    }

    #endregion

    #region TA-Lib Compatibility Notes

    [Fact]
    public void Rocp_TaLibCompatibility_Conversion()
    {
        // TA-Lib ROCP returns decimal (0.05 for 5%)
        // QuanTAlib ROCP returns percentage (5.0 for 5%)
        // Conversion: TaLibRocp = QuanTAlibRocp / 100

        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 105.0), true);

        double quantalibRocp = result.Value; // 5.0
        double talibEquivalent = quantalibRocp / 100.0; // 0.05

        Assert.Equal(5.0, quantalibRocp, 10);
        Assert.Equal(0.05, talibEquivalent, 10);
    }

    #endregion
}
