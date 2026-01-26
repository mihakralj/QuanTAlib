using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BwmaIndicatorTests
{
    [Fact]
    public void BwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BwmaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(0, indicator.Order);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BWMA - Bessel-Weighted Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BwmaIndicator_MinHistoryDepths_IsZero()
    {
        var indicator = new BwmaIndicator { Period = 20 };

        Assert.Equal(0, BwmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BwmaIndicator_ShortName_IncludesPeriodOrderAndSource()
    {
        var indicator = new BwmaIndicator { Period = 15, Order = 2 };

        Assert.Contains("BWMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BwmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BwmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Bwma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BwmaIndicator_Initialize_CreatesInternalBwma()
    {
        var indicator = new BwmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BwmaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);

            // Process update
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        Assert.True(indicator.LinesSeries[0].Count > 0);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void BwmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BwmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void BwmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new BwmaIndicator { Period = 3, Order = 0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        // BWMA result should be in reasonable range
        double lastBwma = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastBwma >= 100 && lastBwma <= 110);
    }

    [Fact]
    public void BwmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BwmaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BwmaIndicator_DifferentOrders_Work()
    {
        int[] orders = { 0, 1, 2, 3 };

        foreach (var order in orders)
        {
            var indicator = new BwmaIndicator { Period = 5, Order = order };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Order {order} should produce finite value");
        }
    }

    [Fact]
    public void BwmaIndicator_Period_CanBeChanged()
    {
        var indicator = new BwmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, BwmaIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BwmaIndicator_Order_CanBeChanged()
    {
        var indicator = new BwmaIndicator { Order = 0 };
        Assert.Equal(0, indicator.Order);

        indicator.Order = 3;
        Assert.Equal(3, indicator.Order);
    }

    [Fact]
    public void BwmaIndicator_DescriptionIsSet()
    {
        var indicator = new BwmaIndicator();

        Assert.Contains("Bessel", indicator.Description, StringComparison.Ordinal);
    }
}
