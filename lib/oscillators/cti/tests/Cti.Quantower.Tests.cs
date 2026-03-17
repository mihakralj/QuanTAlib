using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class CtiIndicatorTests
{
    [Fact]
    public void CtiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CtiIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CTI - Ehlers Correlation Trend Indicator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CtiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CtiIndicator { Period = 20 };

        Assert.Equal(0, CtiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CtiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CtiIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("CTI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CtiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CtiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Cti.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CtiIndicator_Initialize_CreatesInternalCti()
    {
        var indicator = new CtiIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CtiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CtiIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void CtiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CtiIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CtiIndicator_ProcessUpdate_Tick_ComputesValue()
    {
        var indicator = new CtiIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Simulate a tick update on current bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void CtiIndicator_Parameters_CanBeChanged()
    {
        var indicator = new CtiIndicator();
        indicator.Period = 30;

        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void CtiIndicator_DifferentSources_Work()
    {
        var now = DateTime.UtcNow;
        foreach (var source in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new CtiIndicator { Period = 5, Source = source };
            indicator.Initialize();

            for (int i = 0; i < 10; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double value = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(value));
        }
    }

    [Fact]
    public void CtiIndicator_OutputBounded_MinusOneToOne()
    {
        var indicator = new CtiIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Perfect ascending price series
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        for (int i = 0; i < indicator.LinesSeries[0].Count; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(i);
            if (double.IsFinite(value))
            {
                Assert.InRange(value, -1.0, 1.0);
            }
        }
    }
}
