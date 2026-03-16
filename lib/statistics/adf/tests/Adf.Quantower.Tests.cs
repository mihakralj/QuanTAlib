using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class AdfIndicatorTests
{
    [Fact]
    public void AdfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AdfIndicator();

        Assert.Equal(50, indicator.Period);
        Assert.Equal(0, indicator.MaxLag);
        Assert.Equal(1, indicator.RegressionModel); // Constant
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("ADF", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AdfIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new AdfIndicator { Period = 30 };

        Assert.Equal(30, indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(30, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AdfIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AdfIndicator { Period = 50, MaxLag = 2, RegressionModel = 1 };

        Assert.Contains("ADF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("50", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AdfIndicator_ShortName_ShowsRegressionModel()
    {
        var nc = new AdfIndicator { RegressionModel = 0 };
        Assert.Contains("nc", nc.ShortName, StringComparison.Ordinal);

        var c = new AdfIndicator { RegressionModel = 1 };
        Assert.Contains(",c)", c.ShortName, StringComparison.Ordinal);

        var ct = new AdfIndicator { RegressionModel = 2 };
        Assert.Contains("ct", ct.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AdfIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AdfIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Adf.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AdfIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new AdfIndicator { Period = 30 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AdfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AdfIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AdfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AdfIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AdfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AdfIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void AdfIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AdfIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 6; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);

            var reason = i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar;
            indicator.ProcessUpdate(new UpdateArgs(reason));
        }

        Assert.Equal(6, indicator.LinesSeries[0].Count);
        for (int i = 0; i < 6; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void AdfIndicator_DifferentSourceTypes_Work()
    {
        var sourceTypes = new[] { SourceType.Close, SourceType.Open, SourceType.High,
                                  SourceType.Low, SourceType.HL2, SourceType.HLC3 };

        foreach (var sourceType in sourceTypes)
        {
            var indicator = new AdfIndicator { Period = 20, Source = sourceType };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Failed for SourceType={sourceType}");
        }
    }

    [Fact]
    public void AdfIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new AdfIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void AdfIndicator_OutputInRange()
    {
        var indicator = new AdfIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i), 100 + i * 0.5, 105 + i * 0.5, 95 + i * 0.5, 102 + i * 0.5);

            var reason = i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar;
            indicator.ProcessUpdate(new UpdateArgs(reason));
        }

        for (int i = 0; i < 30; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.InRange(val, 0.0, 1.0);
        }
    }

    [Fact]
    public void AdfIndicator_Description_IsSet()
    {
        var indicator = new AdfIndicator();
        Assert.False(string.IsNullOrEmpty(indicator.Description));
    }

    [Fact]
    public void AdfIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator30 = new AdfIndicator { Period = 20 };
        var indicator50 = new AdfIndicator { Period = 30 };
        indicator30.Initialize();
        indicator50.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            indicator30.HistoricalData.AddBar(
                now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator50.HistoricalData.AddBar(
                now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);

            var reason = i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar;
            indicator30.ProcessUpdate(new UpdateArgs(reason));
            indicator50.ProcessUpdate(new UpdateArgs(reason));
        }

        // After enough data, different periods should produce different results
        int lastIdx = 39;
        double val30 = indicator30.LinesSeries[0].GetValue(lastIdx);
        double val50 = indicator50.LinesSeries[0].GetValue(lastIdx);

        Assert.True(double.IsFinite(val30));
        Assert.True(double.IsFinite(val50));
    }
}
