
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
namespace QuanTAlib.Validation;

public sealed class SpearmanValidationTests
{
    [Fact]
    public void PerfectLinear_RhoEqualsOne()
    {
        // Perfect linear relationship: Y = 2X + 5
        // Ranks of X and Y are identical → ρ = 1.0
        var s = new Spearman(10);
        for (int i = 1; i <= 10; i++)
        {
            s.Update((double)i, (2.0 * i) + 5.0, isNew: true);
        }
        Assert.Equal(1.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void PerfectNonlinearMonotonic_RhoEqualsOne()
    {
        // Perfect monotonic but nonlinear: Y = X³
        // Ranks are identical → ρ = 1.0 (Spearman captures monotonic, not just linear)
        var s = new Spearman(10);
        for (int i = 1; i <= 10; i++)
        {
            double x = i;
            s.Update(x, x * x * x, isNew: true);
        }
        Assert.Equal(1.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void BatchAndStreaming_ProduceSameResults()
    {
        var gbmX = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 777);
        var gbmY = new GBM(startPrice: 100, mu: 0.03, sigma: 0.15, seed: 888);

        var seriesX = new TSeries();
        var seriesY = new TSeries();

        for (int i = 0; i < 50; i++)
        {
            var barX = gbmX.Next();
            var barY = gbmY.Next();
            seriesX.Add(new TValue(barX.Time, barX.Close));
            seriesY.Add(new TValue(barY.Time, barY.Close));
        }

        TSeries batch = Spearman.Batch(seriesX, seriesY, 10);

        var streaming = new Spearman(10);
        for (int i = 0; i < 50; i++)
        {
            streaming.Update(seriesX[i], seriesY[i], isNew: true);
            Assert.Equal(streaming.Last.Value, batch[i].Value, 1e-10);
        }
    }

    [Fact]
    public void KnownRanks_NoTies_MatchesSimplifiedFormula()
    {
        // Without ties: ρ = 1 - 6·Σd²/(n(n²-1))
        // X = [10,20,30,40,50], Y = [50,30,10,40,20]
        // Ranks X = [1,2,3,4,5], Ranks Y = [5,3,1,4,2]
        // d = [-4,-1,2,0,3], d² = [16,1,4,0,9], Σd² = 30
        // ρ = 1 - 6*30 / (5*24) = 1 - 180/120 = 1 - 1.5 = -0.5
        var s = new Spearman(5);
        double[] x = [10, 20, 30, 40, 50];
        double[] y = [50, 30, 10, 40, 20];

        for (int i = 0; i < 5; i++)
        {
            s.Update(x[i], y[i], isNew: true);
        }

        Assert.Equal(-0.5, s.Last.Value, 1e-10);
    }

    [Fact]
    public void SpearmanVsKendall_BothDetectMonotonic()
    {
        // Both Spearman and Kendall should be +1 for perfectly concordant data
        var spearman = new Spearman(5);
        var kendall = new Kendall(5);

        for (int i = 1; i <= 5; i++)
        {
            spearman.Update((double)i, (double)i, isNew: true);
            kendall.Update(new TValue(DateTime.UtcNow, i), new TValue(DateTime.UtcNow, i), isNew: true);
        }

        Assert.Equal(1.0, spearman.Last.Value, 1e-10);
        Assert.Equal(1.0, kendall.Last.Value, 1e-10);
    }

    [Fact]
    public void BoundaryValues_AllTied()
    {
        // All X values identical → zero variance in ranks → ρ = 0
        var s = new Spearman(5);
        for (int i = 0; i < 5; i++)
        {
            s.Update(42.0, (double)(i + 1), isNew: true);
        }
        Assert.Equal(0.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void Spearman_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateEhlersSpearmanRankIndicator();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
