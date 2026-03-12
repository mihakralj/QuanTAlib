using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Kendall Tau-a Rank Correlation Coefficient.
/// Validates against known mathematical results and properties since
/// no standard TA library implements Kendall Tau directly.
/// </summary>
public sealed class KendallValidationTests : IDisposable
{
    private const double Tolerance = 1e-10;
    private readonly ITestOutputHelper _output;

    public KendallValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    #region Mathematical Property Validation

    [Fact]
    public void Validate_PerfectConcordance_TauEqualsOne()
    {
        // When both series are monotonically increasing with no ties,
        // all n(n-1)/2 pairs are concordant → τ = 1.0
        const int period = 10;
        var indicator = new Kendall(period);

        for (int i = 0; i < period; i++)
        {
            indicator.Update((double)i, (double)i, true);
        }

        Assert.Equal(1.0, indicator.Last.Value, Tolerance);
        _output.WriteLine($"Perfect concordance: τ = {indicator.Last.Value:G17} (expected 1.0)");
    }

    [Fact]
    public void Validate_PerfectDiscordance_TauEqualsMinusOne()
    {
        // When one series is ascending and the other descending,
        // all pairs are discordant → τ = -1.0
        const int period = 10;
        var indicator = new Kendall(period);

        for (int i = 0; i < period; i++)
        {
            indicator.Update((double)i, (double)(period - 1 - i), true);
        }

        Assert.Equal(-1.0, indicator.Last.Value, Tolerance);
        _output.WriteLine($"Perfect discordance: τ = {indicator.Last.Value:G17} (expected -1.0)");
    }

    [Fact]
    public void Validate_KnownSequence_TauA()
    {
        // x = [1, 2, 3, 4, 5], y = [1, 3, 2, 5, 4]
        // Pairs: (1,2)(1,3)(1,4)(1,5)(2,3)(2,4)(2,5)(3,4)(3,5)(4,5) = 10 total
        // Concordant: (1,2)✓(1,3)✓(1,4)✓(1,5)✓(2,4)✓(2,5)✓(3,4)✓(3,5)✓ = 8
        // Discordant: (2,3)✗(4,5)✗ = 2
        // τ = (8-2)/10 = 0.6
        var indicator = new Kendall(5);
        indicator.Update(1.0, 1.0, true);
        indicator.Update(2.0, 3.0, true);
        indicator.Update(3.0, 2.0, true);
        indicator.Update(4.0, 5.0, true);
        indicator.Update(5.0, 4.0, true);

        Assert.Equal(0.6, indicator.Last.Value, Tolerance);
        _output.WriteLine($"Known sequence τ = {indicator.Last.Value:G17} (expected 0.6)");
    }

    [Fact]
    public void Validate_ReverseKnownSequence_NegativeTau()
    {
        // x = [5, 4, 3, 2, 1], y = [1, 3, 2, 5, 4]
        // This reverses x → should yield τ = -0.6 (same magnitude, opposite sign)
        var indicator = new Kendall(5);
        indicator.Update(5.0, 1.0, true);
        indicator.Update(4.0, 3.0, true);
        indicator.Update(3.0, 2.0, true);
        indicator.Update(2.0, 5.0, true);
        indicator.Update(1.0, 4.0, true);

        Assert.Equal(-0.6, indicator.Last.Value, Tolerance);
        _output.WriteLine($"Reverse sequence τ = {indicator.Last.Value:G17} (expected -0.6)");
    }

    [Fact]
    public void Validate_AllTied_TauEqualsZero()
    {
        // When all x values are identical, every pair has diffX=0 → product=0
        // No concordant or discordant pairs → τ = 0
        var indicator = new Kendall(5);
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(42.0, (double)i, true);
        }

        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
        _output.WriteLine($"All-tied x: τ = {indicator.Last.Value:G17} (expected 0.0)");
    }

    [Fact]
    public void Validate_SymmetryProperty()
    {
        // τ(X,Y) should equal τ(Y,X)
        const int n = 20;
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 42);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.4, seed: 84);

        double[] xData = new double[n];
        double[] yData = new double[n];

        for (int i = 0; i < n; i++)
        {
            xData[i] = gbmX.Next().Close;
            yData[i] = gbmY.Next().Close;
        }

        // τ(X,Y)
        var ind1 = new Kendall(10);
        for (int i = 0; i < n; i++)
        {
            ind1.Update(xData[i], yData[i], true);
        }

        // τ(Y,X)
        var ind2 = new Kendall(10);
        for (int i = 0; i < n; i++)
        {
            ind2.Update(yData[i], xData[i], true);
        }

        Assert.Equal(ind1.Last.Value, ind2.Last.Value, Tolerance);
        _output.WriteLine($"Symmetry: τ(X,Y) = {ind1.Last.Value:G17}, τ(Y,X) = {ind2.Last.Value:G17}");
    }

    [Fact]
    public void Validate_AntisymmetryProperty()
    {
        // τ(X, -Y) should equal -τ(X, Y)
        const int n = 30;
        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 55);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.4, seed: 77);

        double[] xData = new double[n];
        double[] yData = new double[n];

        for (int i = 0; i < n; i++)
        {
            xData[i] = gbmX.Next().Close;
            yData[i] = gbmY.Next().Close;
        }

        // τ(X,Y)
        var ind1 = new Kendall(10);
        for (int i = 0; i < n; i++)
        {
            ind1.Update(xData[i], yData[i], true);
        }

        // τ(X,-Y)
        var ind2 = new Kendall(10);
        for (int i = 0; i < n; i++)
        {
            ind2.Update(xData[i], -yData[i], true);
        }

        Assert.Equal(-ind1.Last.Value, ind2.Last.Value, Tolerance);
        _output.WriteLine($"Antisymmetry: τ(X,Y) = {ind1.Last.Value:G17}, τ(X,-Y) = {ind2.Last.Value:G17}");
    }

    #endregion

    #region Batch vs Streaming Consistency

    [Fact]
    public void Validate_BatchTSeries_MatchesStreaming()
    {
        const int period = 10;
        const int length = 200;

        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 42);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.4, seed: 123);

        var seriesX = new TSeries(length);
        var seriesY = new TSeries(length);

        for (int i = 0; i < length; i++)
        {
            var now = DateTime.UtcNow.AddMinutes(i);
            seriesX.Add(new TValue(now, gbmX.Next().Close));
            seriesY.Add(new TValue(now, gbmY.Next().Close));
        }

        // Streaming
        var indicator = new Kendall(period);
        double[] streamResults = new double[length];
        for (int i = 0; i < length; i++)
        {
            streamResults[i] = indicator.Update(
                seriesX.Values[i], seriesY.Values[i], true).Value;
        }

        // Batch TSeries
        var batchResults = Kendall.Batch(seriesX, seriesY, period);

        int matched = 0;
        for (int i = period; i < length; i++)
        {
            if (double.IsFinite(streamResults[i]) && double.IsFinite(batchResults.Values[i]))
            {
                Assert.Equal(streamResults[i], batchResults.Values[i], Tolerance);
                matched++;
            }
        }

        Assert.True(matched > 100, $"Only matched {matched} values (expected > 100)");
        _output.WriteLine($"Batch TSeries vs Streaming: {matched} values matched");
    }

    [Fact]
    public void Validate_BatchSpan_MatchesStreaming()
    {
        const int period = 10;
        const int length = 200;

        var gbmX = new GBM(startPrice: 100, mu: 0.02, sigma: 0.3, seed: 42);
        var gbmY = new GBM(startPrice: 200, mu: 0.01, sigma: 0.4, seed: 123);

        double[] xData = new double[length];
        double[] yData = new double[length];

        for (int i = 0; i < length; i++)
        {
            xData[i] = gbmX.Next().Close;
            yData[i] = gbmY.Next().Close;
        }

        // Streaming
        var indicator = new Kendall(period);
        double[] streamResults = new double[length];
        for (int i = 0; i < length; i++)
        {
            streamResults[i] = indicator.Update(xData[i], yData[i], true).Value;
        }

        // Span batch
        double[] spanResults = new double[length];
        Kendall.Batch(xData, yData, spanResults, period);

        int matched = 0;
        for (int i = period; i < length; i++)
        {
            if (double.IsFinite(streamResults[i]) && double.IsFinite(spanResults[i]))
            {
                Assert.Equal(streamResults[i], spanResults[i], Tolerance);
                matched++;
            }
        }

        Assert.True(matched > 100, $"Only matched {matched} values (expected > 100)");
        _output.WriteLine($"Batch Span vs Streaming: {matched} values matched");
    }

    #endregion

    #region Known Analytical Values

    [Fact]
    public void Validate_ThreeElements_KnownTau()
    {
        // x = [1, 2, 3], y = [3, 1, 2]
        // Pairs: (1,2): x↑y↓ disc, (1,3): x↑y↓ disc, (2,3): x↑y↑ conc
        // τ = (1-2)/3 = -1/3
        var indicator = new Kendall(3);
        indicator.Update(1.0, 3.0, true);
        indicator.Update(2.0, 1.0, true);
        indicator.Update(3.0, 2.0, true);

        Assert.Equal(-1.0 / 3.0, indicator.Last.Value, Tolerance);
        _output.WriteLine($"Three elements: τ = {indicator.Last.Value:G17} (expected {-1.0 / 3.0:G17})");
    }

    [Fact]
    public void Validate_FourElements_AllConcordant()
    {
        // x = [1,2,3,4], y = [10,20,30,40]
        // All 6 pairs concordant → τ = 6/6 = 1.0
        var indicator = new Kendall(4);
        indicator.Update(1.0, 10.0, true);
        indicator.Update(2.0, 20.0, true);
        indicator.Update(3.0, 30.0, true);
        indicator.Update(4.0, 40.0, true);

        Assert.Equal(1.0, indicator.Last.Value, Tolerance);
        _output.WriteLine($"Four elements all concordant: τ = {indicator.Last.Value:G17}");
    }

    [Fact]
    public void Validate_FourElements_MixedPairs()
    {
        // x = [1,2,3,4], y = [2,4,1,3]
        // Pairs analysis:
        // (1,2): x↑ y↑ C  (2,3): x↑ y↓ D  (3,4): x↑ y↑ C
        // (1,3): x↑ y↓ D  (2,4): x↑ y↓ D
        // (1,4): x↑ y↑ C
        // C=3, D=3 → τ = 0/6 = 0.0
        var indicator = new Kendall(4);
        indicator.Update(1.0, 2.0, true);
        indicator.Update(2.0, 4.0, true);
        indicator.Update(3.0, 1.0, true);
        indicator.Update(4.0, 3.0, true);

        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
        _output.WriteLine($"Four elements mixed: τ = {indicator.Last.Value:G17} (expected 0.0)");
    }

    #endregion
}
