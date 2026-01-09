using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class EllipticIndicatorTests
{
    [Fact]
    public void EllipticIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EllipticIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Elliptic - 2nd Order Elliptic Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void EllipticIndicator_MinHistoryDepths_EqualsFive()
    {
        var indicator = new EllipticIndicator { Period = 20 };

        // The property calls static member
        Assert.Equal(5, EllipticIndicator.MinHistoryDepths);
        Assert.Equal(5, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EllipticIndicator_ShortName_IncludesParameters()
    {
        var indicator = new EllipticIndicator { Period = 20 };
        Assert.Contains("Elliptic", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void EllipticIndicator_Initialize_CreatesInternalElliptic()
    {
        var indicator = new EllipticIndicator { Period = 20 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void EllipticIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EllipticIndicator { Period = 20 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void EllipticIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EllipticIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EllipticIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new EllipticIndicator { Period = 20, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void EllipticIndicator_Period_CanBeChanged()
    {
        var indicator = new EllipticIndicator { Period = 20 };
        Assert.Equal(20, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }
}