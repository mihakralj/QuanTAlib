using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class AdxvmaIndicatorTests
{
    [Fact]
    public void AdxvmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AdxvmaIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ADXVMA - ADX Variable Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AdxvmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AdxvmaIndicator { Period = 14 };

        Assert.Equal(0, AdxvmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AdxvmaIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new AdxvmaIndicator { Period = 20 };

        Assert.Contains("ADXVMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AdxvmaIndicator_Initialize_CreatesInternalAdxvma()
    {
        var indicator = new AdxvmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AdxvmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AdxvmaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void AdxvmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AdxvmaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 110, 98, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AdxvmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AdxvmaIndicator { Period = 5 };
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
    public void AdxvmaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AdxvmaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        (double o, double h, double l, double c)[] bars =
        {
            (100, 102, 98, 101),
            (101, 103, 99, 102),
            (102, 104, 100, 103),
            (103, 108, 97, 105),
            (105, 112, 100, 110),
            (110, 115, 105, 108),
            (108, 110, 106, 109),
            (109, 111, 107, 110),
            (110, 112, 108, 111),
            (111, 113, 109, 112)
        };

        foreach (var (o, h, l, c) in bars)
        {
            indicator.HistoricalData.AddBar(now, o, h, l, c);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < bars.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(bars.Length - 1 - i)));
        }

        // ADXVMA should be smoothing the values
        double lastAdxvma = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastAdxvma >= 95 && lastAdxvma <= 120);
    }

    [Fact]
    public void AdxvmaIndicator_Parameters_CanBeChanged()
    {
        var indicator = new AdxvmaIndicator { Period = 10 };
        Assert.Equal(10, indicator.Period);

        indicator.Period = 30;
        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void AdxvmaIndicator_LongPeriod_Works()
    {
        var indicator = new AdxvmaIndicator { Period = 28 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 200; i++)
        {
            double price = 100 + (i * 0.1) + Math.Sin(i * 0.1) * 2;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastValue));
        Assert.True(lastValue > 100 && lastValue < 130);
    }

    [Fact]
    public void AdxvmaIndicator_ShortPeriod_Works()
    {
        var indicator = new AdxvmaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        (double o, double h, double l, double c)[] bars =
        {
            (100, 103, 97, 102),
            (102, 106, 100, 105),
            (105, 108, 102, 104),
            (104, 107, 101, 106),
            (106, 110, 104, 108),
            (108, 112, 105, 110)
        };

        foreach (var (o, h, l, c) in bars)
        {
            indicator.HistoricalData.AddBar(now, o, h, l, c);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < bars.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(bars.Length - 1 - i)));
        }
    }

    [Fact]
    public void AdxvmaIndicator_UsesOhlcForTrueRange()
    {
        var indicator = new AdxvmaIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars where High-Low range differs significantly from Close-to-Close
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(1)));
    }
}
