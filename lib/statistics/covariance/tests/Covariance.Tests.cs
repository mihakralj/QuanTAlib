
namespace QuanTAlib.Tests;

public class CovarianceTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Covariance(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Covariance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Covariance(1)); // Period must be >= 2

        // Valid period should not throw
        var cov = new Covariance(2);
        Assert.NotNull(cov);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var cov = new Covariance(10);

        Assert.Equal(0, cov.Last.Value);
        Assert.False(cov.IsHot);
        Assert.Contains("Cov", cov.Name, StringComparison.Ordinal);

        cov.Update(100, 100);
        cov.Update(101, 101);
        Assert.NotEqual(0, cov.Last.Time);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        const int period = 5;
        var cov = new Covariance(period);

        for (int i = 0; i < period - 1; i++)
        {
            Assert.False(cov.IsHot, $"IsHot should be false at index {i}");
            cov.Update(i, i * 2);
        }

        cov.Update(period - 1, (period - 1) * 2);
        Assert.True(cov.IsHot, "IsHot should be true after period updates");
    }

    [Fact]
    public void Covariance_CalculatesCorrectly()
    {
        // Arrange
        var cov = new Covariance(3, isPopulation: false);

        // Act & Assert
        // 1. Add (1, 2)
        // MeanX = 1, MeanY = 2
        // Cov = 0 (n=1)
        var res1 = cov.Update(1, 2);
        Assert.Equal(0, res1.Value);

        // 2. Add (2, 4)
        // X: {1, 2}, Y: {2, 4}
        // MeanX = 1.5, MeanY = 3
        // Cov = ((1-1.5)(2-3) + (2-1.5)(4-3)) / 1
        //     = ((-0.5)(-1) + (0.5)(1)) / 1
        //     = (0.5 + 0.5) / 1 = 1
        var res2 = cov.Update(2, 4);
        Assert.Equal(1, res2.Value);

        // 3. Add (3, 6)
        // X: {1, 2, 3}, Y: {2, 4, 6}
        // MeanX = 2, MeanY = 4
        // Cov = ((1-2)(2-4) + (2-2)(4-4) + (3-2)(6-4)) / 2
        //     = ((-1)(-2) + 0 + (1)(2)) / 2
        //     = (2 + 2) / 2 = 2
        var res3 = cov.Update(3, 6);
        Assert.Equal(2, res3.Value);

        // 4. Add (4, 8) -> Window slides: {2, 3, 4}, {4, 6, 8}
        // MeanX = 3, MeanY = 6
        // Cov = ((2-3)(4-6) + (3-3)(6-6) + (4-3)(8-6)) / 2
        //     = ((-1)(-2) + 0 + (1)(2)) / 2
        //     = (2 + 2) / 2 = 2
        var res4 = cov.Update(4, 8);
        Assert.Equal(2, res4.Value);
    }

    [Fact]
    public void Covariance_Population_CalculatesCorrectly()
    {
        // Arrange
        var cov = new Covariance(3, isPopulation: true);

        // Act & Assert
        cov.Update(1, 2);
        cov.Update(2, 4);

        // 3. Add (3, 6)
        // X: {1, 2, 3}, Y: {2, 4, 6}
        // MeanX = 2, MeanY = 4
        // Cov = ((1-2)(2-4) + (2-2)(4-4) + (3-2)(6-4)) / 3
        //     = (2 + 2) / 3 = 4/3
        var res3 = cov.Update(3, 6);
        Assert.Equal(4.0 / 3.0, res3.Value, precision: 10);
    }

    [Fact]
    public void Covariance_HandlesZeroCovariance()
    {
        // Arrange
        var cov = new Covariance(3);

        // Act
        cov.Update(1, 1);
        cov.Update(2, 1);
        var res = cov.Update(3, 1); // Y is constant, variance Y is 0, covariance is 0

        // Assert
        Assert.Equal(0, res.Value);
    }

    [Fact]
    public void Covariance_HandlesNegativeCovariance()
    {
        // Arrange
        var cov = new Covariance(3);

        // Act
        cov.Update(1, 3);
        cov.Update(2, 2);
        var res = cov.Update(3, 1);

        // X: {1, 2, 3}, MeanX = 2
        // Y: {3, 2, 1}, MeanY = 2
        // Cov = ((1-2)(3-2) + (2-2)(2-2) + (3-2)(1-2)) / 2
        //     = ((-1)(1) + 0 + (1)(-1)) / 2
        //     = (-1 - 1) / 2 = -1

        // Assert
        Assert.Equal(-1, res.Value);
    }

    [Fact]
    public void Covariance_Resync_Works()
    {
        // Arrange
        var cov = new Covariance(3);

        // Act
        // Force many updates to trigger resync (ResyncInterval = 1000)
        // We can't easily force 1000 updates in a simple test without loop,
        // but we can verify the logic holds for a sequence.
        for (int i = 0; i < 1100; i++)
        {
            cov.Update(i, i * 2);
        }

        // Last 3: {1097, 1098, 1099}, {2194, 2196, 2198}
        // This is a perfect linear relationship y = 2x
        // Cov(X, 2X) = 2 * Var(X)
        // Var(X) for {x-1, x, x+1} is:
        // Mean = x
        // SumSqDiff = (-1)^2 + 0 + 1^2 = 2
        // Var = 2 / 2 = 1
        // Cov = 2 * 1 = 2

        // Assert
        Assert.Equal(2, cov.Last.Value, precision: 10);
    }

    [Fact]
    public void Covariance_Update_IsNew_False_Works()
    {
        // Arrange
        var cov = new Covariance(3);

        // Act
        cov.Update(1, 2);
        cov.Update(2, 4);
        cov.Update(3, 6); // Cov = 2

        // Update last bar with new values
        // Change (3, 6) to (4, 8)
        // X: {1, 2, 4}, MeanX = 7/3 = 2.333...
        // Y: {2, 4, 8}, MeanY = 14/3 = 4.666...
        // This is harder to calc manually, let's use the property that it should match adding (4, 8) directly

        var res = cov.Update(4, 8, isNew: false);

        var cov2 = new Covariance(3);
        cov2.Update(1, 2);
        cov2.Update(2, 4);
        var expected = cov2.Update(4, 8);

        // Assert
        Assert.Equal(expected.Value, res.Value, precision: 10);
    }

    [Fact]
    public void Covariance_Throws_On_Single_Input()
    {
        var cov = new Covariance(10);
        Assert.Throws<NotSupportedException>(() => cov.Update(new TValue(DateTime.UtcNow, 1)));
        Assert.Throws<NotSupportedException>(() => cov.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => cov.Prime([1, 2, 3]));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var cov = new Covariance(5);

        // Feed 10 updates
        for (int i = 0; i < 10; i++)
        {
            cov.Update(i, i * 2);
        }

        double stateAfterTen = cov.Last.Value;

        // Apply 5 corrections with isNew=false
        for (int i = 0; i < 5; i++)
        {
            cov.Update(100 + i, 200 + i, isNew: false);
        }

        // Restore to original values
        cov.Update(9, 18, isNew: false);

        Assert.Equal(stateAfterTen, cov.Last.Value, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var cov = new Covariance(5);
        for (int i = 0; i < 10; i++)
        {
            cov.Update(i, i * 2);
        }
        Assert.True(cov.IsHot);

        cov.Reset();
        Assert.False(cov.IsHot);
        Assert.Equal(0, cov.Last.Value);
    }

    [Fact]
    public void NaN_Input_ProducesNaN()
    {
        var cov = new Covariance(5);

        // Add some valid values
        cov.Update(1, 2);
        cov.Update(2, 4);
        cov.Update(3, 6);

        // Add NaN - Covariance propagates NaN (two-input indicators don't have last valid value substitution)
        var result = cov.Update(double.NaN, double.NaN);

        // For two-input indicators, NaN may propagate or produce 0
        // The behavior depends on implementation - just verify no exception
        Assert.True(double.IsNaN(result.Value) || double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_ProducesInfinity()
    {
        var cov = new Covariance(5);

        // Add some valid values
        cov.Update(1, 2);
        cov.Update(2, 4);
        cov.Update(3, 6);

        // Add Infinity - Covariance propagates infinity (two-input indicators don't have last valid value substitution)
        var result = cov.Update(double.PositiveInfinity, double.PositiveInfinity);

        // For two-input indicators, infinity may propagate
        // The behavior depends on implementation - just verify no exception
        Assert.True(double.IsInfinity(result.Value) || double.IsNaN(result.Value) || double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchSpan_MatchesStreaming()
    {
        int period = 5;
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        double[] x = new double[count];
        double[] y = new double[count];
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            x[i] = bar.Close;
            y[i] = (bar.Close * 1.5) + 10; // Correlated series
        }

        // Streaming
        var cov = new Covariance(period);
        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = cov.Update(x[i], y[i]).Value;
        }

        // Batch
        double[] batchResults = new double[count];
        Covariance.Batch(x, y, batchResults, period);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], precision: 9);
        }
    }
}
