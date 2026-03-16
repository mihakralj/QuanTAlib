using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class DymiIndicatorTests
{
    [Fact]
    public void DymiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DymiIndicator();

        Assert.Equal(14, indicator.BasePeriod);
        Assert.Equal(5, indicator.ShortPeriod);
        Assert.Equal(10, indicator.LongPeriod);
        Assert.Equal(3, indicator.MinPeriod);
        Assert.Equal(30, indicator.MaxPeriod);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DYMI - Dynamic Momentum Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DymiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new DymiIndicator();

        Assert.Equal(0, DymiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DymiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new DymiIndicator
        {
            BasePeriod = 10,
            ShortPeriod = 4,
            LongPeriod = 8,
            MinPeriod = 2,
            MaxPeriod = 20
        };
        indicator.Initialize();

        Assert.Contains("DYMI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("4", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("8", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DymiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DymiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dymi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DymiIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new DymiIndicator
        {
            BasePeriod = 14,
            ShortPeriod = 5,
            LongPeriod = 10,
            MinPeriod = 3,
            MaxPeriod = 30
        };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DymiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DymiIndicator
        {
            BasePeriod = 14,
            ShortPeriod = 5,
            LongPeriod = 10,
            MinPeriod = 3,
            MaxPeriod = 30
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 60; i++)
        {
            double price = 100.0 + Math.Sin(i * 0.3) * 10.0 + i * 0.1;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price + 5, price + 10, price - 5, price);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
        Assert.True(value >= 0.0 && value <= 100.0, $"DYMI={value} out of [0,100]");
    }

    [Fact]
    public void DymiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new DymiIndicator
        {
            BasePeriod = 14,
            ShortPeriod = 5,
            LongPeriod = 10,
            MinPeriod = 3,
            MaxPeriod = 30
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double price = 100.0 + i * 0.5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price + 3, price + 6, price - 3, price);

            var reason = i < 49 ? UpdateReason.HistoricalBar : UpdateReason.NewBar;
            var args = new UpdateArgs(reason);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void DymiIndicator_DifferentSourceTypes_ComputeWithoutError()
    {
        foreach (var sourceType in new[] { SourceType.Close, SourceType.Open, SourceType.High, SourceType.Low })
        {
            var indicator = new DymiIndicator
            {
                BasePeriod = 14,
                ShortPeriod = 5,
                LongPeriod = 10,
                MinPeriod = 3,
                MaxPeriod = 30,
                Source = sourceType
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 30; i++)
            {
                double price = 100.0 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price + 1);

                var args = new UpdateArgs(UpdateReason.HistoricalBar);
                indicator.ProcessUpdate(args);
            }

            double value = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(value), $"SourceType {sourceType}: value={value}");
        }
    }
}
