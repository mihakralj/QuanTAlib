using Skender.Stock.Indicators;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class SuperValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public SuperValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void MatchesSkender()
    {
        var super = new Super(10, 3.0);
        var results = new List<double>();
        var upper = new List<double>();
        var lower = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = super.Update(_data.Bars[i]);
            results.Add(res.Value);
            upper.Add(super.UpperBand.Value);
            lower.Add(super.LowerBand.Value);
        }

        // Skender uses GetSuperTrend
        var skenderResults = _data.SkenderQuotes.GetSuperTrend(10, 3.0).ToList();

        Assert.Equal(_data.Bars.Count, skenderResults.Count);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            // Skender returns null for warmup
            if (skenderResults[i].SuperTrend == null)
            {
                Assert.True(double.IsNaN(results[i]));
                continue;
            }

            Assert.Equal((double)skenderResults[i].SuperTrend!, results[i], ValidationHelper.SkenderTolerance);

            if (skenderResults[i].UpperBand != null)
            {
                Assert.Equal((double)skenderResults[i].UpperBand!, upper[i], ValidationHelper.SkenderTolerance);
            }

            if (skenderResults[i].LowerBand != null)
            {
                Assert.Equal((double)skenderResults[i].LowerBand!, lower[i], ValidationHelper.SkenderTolerance);
            }
        }
    }

    // Note: OoplesFinance implementation of SuperTrend diverges significantly from Skender and QuanTAlib.
    // This is likely due to different initialization logic for ATR or the SuperTrend state itself.
    // Therefore, we do not validate against Ooples for SuperTrend.
}
