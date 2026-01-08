using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Quantower.Tests;

public class LoessIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var indicator = new LoessIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Loess - Locally Estimated Scatterplot Smoothing", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void Initialize_CreatesInternalFilter()
    {
        var indicator = new LoessIndicator { Period = 14 };
        indicator.Initialize();
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Loess", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LoessIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LoessIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LoessIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Tick update should utilize the internal filter's Update method
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Note: In test environment, ProcessUpdate might add points even for NewTick depending on Mock behavior.
        // We verify that it runs without error and the series has values.
        Assert.True(indicator.LinesSeries[0].Count > 0);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new LoessIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
            SourceType.OC2,
            SourceType.OHL3,
            SourceType.OHLC4
        };

        foreach (var source in sources)
        {
            var indicator = new LoessIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

}
