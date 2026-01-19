using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class YzvamaIndicatorTests
{
    [Fact]
    public void YzvamaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new YzvamaIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(3, indicator.ShortYzvPeriod);
        Assert.Equal(50, indicator.LongYzvPeriod);
        Assert.Equal(100, indicator.PercentileLookback);
        Assert.Equal(5, indicator.MinLength);
        Assert.Equal(100, indicator.MaxLength);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("YZVAMA - Yang-Zhang Volatility Adjusted Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void YzvamaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new YzvamaIndicator { ShortYzvPeriod = 3 };

        Assert.Equal(0, YzvamaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void YzvamaIndicator_ShortName_IncludesParametersAndSource()
    {
        var indicator = new YzvamaIndicator
        {
            ShortYzvPeriod = 5,
            LongYzvPeriod = 60,
            PercentileLookback = 200,
            Source = SourceType.HLC3
        };

        Assert.Contains("YZVAMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("60", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("200", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("HLC3", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void YzvamaIndicator_Initialize_CreatesInternalYzvama()
    {
        var indicator = new YzvamaIndicator { ShortYzvPeriod = 3 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void YzvamaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new YzvamaIndicator { ShortYzvPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void YzvamaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new YzvamaIndicator { ShortYzvPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 112, 98, 110);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void YzvamaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new YzvamaIndicator { ShortYzvPeriod = 3 };
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
    public void YzvamaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new YzvamaIndicator { Source = source, ShortYzvPeriod = 3 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }
}

