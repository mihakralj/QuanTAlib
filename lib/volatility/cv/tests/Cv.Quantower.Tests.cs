using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class CvIndicatorTests
{
    [Fact]
    public void CvIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CvIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.2, indicator.Alpha);
        Assert.Equal(0.7, indicator.Beta);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CV - Conditional Volatility (GARCH(1,1))", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CvIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CvIndicator { Period = 14, Alpha = 0.15, Beta = 0.75 };
        Assert.Contains("CV", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.75", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CvIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CvIndicator();

        Assert.Equal(0, CvIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CvIndicator_Initialize_CreatesInternalCv()
    {
        var indicator = new CvIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CvIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CvIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data with volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i * 2 + (i % 2 == 0 ? 5 : -5); // Add some volatility
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0); // CV should be non-negative
    }

    [Fact]
    public void CvIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CvIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 120, 128, 115, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CvIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new CvIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double basePrice = 100 + i + (i % 3 == 0 ? 10 : -5); // Add volatility
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val >= 0, $"Period {period} should produce non-negative CV");
        }
    }

    [Fact]
    public void CvIndicator_DifferentAlphaValues_Work()
    {
        double[] alphas = { 0.05, 0.1, 0.2, 0.3 };

        foreach (var alpha in alphas)
        {
            var indicator = new CvIndicator { Alpha = alpha, Beta = 0.6 }; // Keep alpha + beta < 1
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Alpha {alpha} should produce finite value");
            Assert.True(val >= 0, $"Alpha {alpha} should produce non-negative CV");
        }
    }

    [Fact]
    public void CvIndicator_DifferentBetaValues_Work()
    {
        double[] betas = { 0.5, 0.6, 0.7, 0.8 };

        foreach (var beta in betas)
        {
            var indicator = new CvIndicator { Alpha = 0.1, Beta = beta }; // Keep alpha + beta < 1
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 50; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Beta {beta} should produce finite value");
            Assert.True(val >= 0, $"Beta {beta} should produce non-negative CV");
        }
    }

    [Fact]
    public void CvIndicator_StationarityConstraint_AdjustsBeta()
    {
        // Test that when alpha + beta >= 1, OnInit adjusts beta
        var indicator = new CvIndicator { Alpha = 0.5, Beta = 0.6 }; // Sum = 1.1, violates constraint
        indicator.Initialize();

        // Beta should be adjusted to maintain stationarity (0.99 - alpha)
        Assert.True(indicator.Alpha + indicator.Beta < 1.0,
            "After initialization, alpha + beta should be less than 1");
    }

    [Fact]
    public void CvIndicator_DifferentSourceTypes_Work()
    {
        SourceType[] sources = { SourceType.Close, SourceType.High, SourceType.Low, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new CvIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 40; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void CvIndicator_Period_CanBeChanged()
    {
        var indicator = new CvIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }

    [Fact]
    public void CvIndicator_Alpha_CanBeChanged()
    {
        var indicator = new CvIndicator();
        Assert.Equal(0.2, indicator.Alpha);

        indicator.Alpha = 0.15;
        Assert.Equal(0.15, indicator.Alpha);

        indicator.Alpha = 0.25;
        Assert.Equal(0.25, indicator.Alpha);
    }

    [Fact]
    public void CvIndicator_Beta_CanBeChanged()
    {
        var indicator = new CvIndicator();
        Assert.Equal(0.7, indicator.Beta);

        indicator.Beta = 0.6;
        Assert.Equal(0.6, indicator.Beta);

        indicator.Beta = 0.8;
        Assert.Equal(0.8, indicator.Beta);
    }

    [Fact]
    public void CvIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new CvIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void CvIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CvIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Cv.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CvIndicator_VolatilityClustering_ProducesVaryingOutput()
    {
        var indicator = new CvIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        // Add data with varying volatility
        for (int i = 0; i < 50; i++)
        {
            // First 20 bars: low volatility, next 20 bars: high volatility, last 10: low again
            double volatilityFactor;
            if (i < 20)
            {
                volatilityFactor = 1.0;
            }
            else if (i < 40)
            {
                volatilityFactor = 5.0;
            }
            else
            {
                volatilityFactor = 1.0;
            }
            double basePrice = 100 + (i % 2 == 0 ? volatilityFactor : -volatilityFactor);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + volatilityFactor, basePrice - volatilityFactor, basePrice, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (i >= 10) // After warmup
            {
                values.Add(indicator.LinesSeries[0].GetValue(0));
            }
        }

        // Verify we got varying volatility values (GARCH captures clustering)
        double min = values.Min();
        double max = values.Max();
        Assert.True(max > min, "CV should vary with changing volatility patterns");
    }
}
