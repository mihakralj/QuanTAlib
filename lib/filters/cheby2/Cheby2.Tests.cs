using Xunit;
using QuanTAlib;

namespace Filters;

public class Cheby2Tests
{
    private readonly GBM _gbm;

    public Cheby2Tests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 1234);
    }

    [Fact]
    public void Constructor_ValidatesParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby2(period: 1, attenuation: 5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby2(period: 10, attenuation: 0.0));
        var filter = new Cheby2(period: 10, attenuation: 5.0);
        Assert.NotNull(filter);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenReady()
    {
        var filter = new Cheby2(20, 5.0);
        Assert.False(filter.IsHot);

        // Feed some data
        for (int i = 0; i < 3; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        Assert.True(filter.IsHot);
    }

    [Fact]
    public void Update_HandlesNaNSafely()
    {
        var filter = new Cheby2(10, 5.0);

        // Initial good value
        filter.Update(new TValue(DateTime.UtcNow, 100.0));

        // Bad value
        var result = filter.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Should use last valid value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var filter = new Cheby2(10, 5.0);
        var time = DateTime.UtcNow;

        // Add some history
        for (int i = 0; i < 5; i++)
        {
            filter.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        double valBefore = filter.Last.Value;

        // 1. New update
        filter.Update(new TValue(time.AddSeconds(5), 200.0), isNew: true);
        double valAfterNew = filter.Last.Value;

        // 2. Correction (isNew=false) with different value
        filter.Update(new TValue(time.AddSeconds(5), 150.0), isNew: false);
        double valAfterCorrection = filter.Last.Value;

        Assert.NotEqual(valBefore, valAfterNew);
        Assert.NotEqual(valAfterNew, valAfterCorrection);

        // 3. Correction back to original value (isNew=false)
        filter.Update(new TValue(time.AddSeconds(5), 200.0), isNew: false);
        double valRestored = filter.Last.Value;

        Assert.Equal(valAfterNew, valRestored, 1e-10);
    }

    [Fact]
    public void SpanBatch_MatchesIterative()
    {
        const int period = 10;
        int count = 100;
        var data = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = data.Close.Values.ToArray();

        // Iterative
        var filter = new Cheby2(period, 5.0);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            iterativeResults[i] = filter.Update(new TValue(DateTime.UtcNow, values[i])).Value;
        }

        // Span
        var spanResults = new double[count];
        Cheby2.Calculate(values, spanResults, period, 5.0);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], spanResults[i], 1e-10);
        }
    }
}
