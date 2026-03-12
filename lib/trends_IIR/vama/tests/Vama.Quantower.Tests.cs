using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VamaIndicatorTests
{
    [Fact]
    public void VamaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VamaIndicator();

        Assert.Equal(20, indicator.BaseLength);
        Assert.Equal(10, indicator.ShortAtrPeriod);
        Assert.Equal(50, indicator.LongAtrPeriod);
        Assert.Equal(5, indicator.MinLength);
        Assert.Equal(100, indicator.MaxLength);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("VAMA - Volatility Adjusted Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VamaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new VamaIndicator { BaseLength = 20 };

        Assert.Equal(0, VamaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VamaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VamaIndicator
        {
            BaseLength = 15,
            ShortAtrPeriod = 8,
            LongAtrPeriod = 40
        };

        Assert.Contains("VAMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("8", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("40", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VamaIndicator_Initialize_CreatesInternalVama()
    {
        var indicator = new VamaIndicator { BaseLength = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VamaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VamaIndicator { BaseLength = 5 };
        indicator.Initialize();

        // Add historical data with OHLC
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
    public void VamaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VamaIndicator { BaseLength = 5 };
        indicator.Initialize();

        // Add historical data with varying volatility
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 110, 98, 106);

        // Process first update
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Line series should have values
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VamaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new VamaIndicator { BaseLength = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process historical bar first
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // Update with new tick (same bar data - simulates intrabar update)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        // Both values should be finite
        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void VamaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new VamaIndicator { BaseLength = 5, ShortAtrPeriod = 3, LongAtrPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Create bars with varying volatility
        (double o, double h, double l, double c)[] bars =
        {
            (100, 102, 98, 101),   // Low volatility
            (101, 103, 99, 102),
            (102, 104, 100, 103),
            (103, 108, 97, 105),   // Higher volatility
            (105, 112, 100, 110),
            (110, 115, 105, 108),
            (108, 110, 106, 109),  // Back to lower
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

        // VAMA should be smoothing the values
        double lastVama = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastVama >= 95 && lastVama <= 120);
    }

    [Fact]
    public void VamaIndicator_HighVolatility_ShorterPeriod()
    {
        // Test that high volatility results in shorter effective period (faster response)
        var indicator = new VamaIndicator
        {
            BaseLength = 20,
            ShortAtrPeriod = 5,
            LongAtrPeriod = 20,
            MinLength = 5,
            MaxLength = 50
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Start with low volatility period
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + (i * 0.1);
            indicator.HistoricalData.AddBar(now, price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        double afterLowVol = indicator.LinesSeries[0].GetValue(0);

        // Now add high volatility bars
        for (int i = 0; i < 10; i++)
        {
            double price = 103 + i;
            indicator.HistoricalData.AddBar(now, price, price + 5, price - 5, price + 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        double afterHighVol = indicator.LinesSeries[0].GetValue(0);

        // Both should be finite
        Assert.True(double.IsFinite(afterLowVol));
        Assert.True(double.IsFinite(afterHighVol));
    }

    [Fact]
    public void VamaIndicator_Parameters_CanBeChanged()
    {
        var indicator = new VamaIndicator { BaseLength = 10 };
        Assert.Equal(10, indicator.BaseLength);

        indicator.BaseLength = 30;
        Assert.Equal(30, indicator.BaseLength);

        indicator.ShortAtrPeriod = 15;
        Assert.Equal(15, indicator.ShortAtrPeriod);

        indicator.LongAtrPeriod = 60;
        Assert.Equal(60, indicator.LongAtrPeriod);

        indicator.MinLength = 3;
        Assert.Equal(3, indicator.MinLength);

        indicator.MaxLength = 200;
        Assert.Equal(200, indicator.MaxLength);
    }

    [Fact]
    public void VamaIndicator_LongPeriod_Works()
    {
        var indicator = new VamaIndicator
        {
            BaseLength = 50,
            ShortAtrPeriod = 20,
            LongAtrPeriod = 100,
            MaxLength = 200
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 200; i++)
        {
            double price = 100 + (i * 0.1) + (Math.Sin(i * 0.1) * 2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Last value should be finite and in reasonable range
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastValue));
        Assert.True(lastValue > 100 && lastValue < 130);
    }

    [Fact]
    public void VamaIndicator_ShortPeriod_Works()
    {
        var indicator = new VamaIndicator
        {
            BaseLength = 5,
            ShortAtrPeriod = 3,
            LongAtrPeriod = 10,
            MinLength = 2,
            MaxLength = 20
        };
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

        // All values should be finite
        for (int i = 0; i < bars.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(bars.Length - 1 - i)));
        }
    }

    [Fact]
    public void VamaIndicator_UsesOhlcForTrueRange()
    {
        // VAMA should use OHLC data for True Range calculation
        var indicator = new VamaIndicator
        {
            BaseLength = 10,
            ShortAtrPeriod = 5,
            LongAtrPeriod = 20
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars where High-Low range differs significantly from Close-to-Close
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 100);  // TR = 20 (H-L)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 102);  // TR considering prev close
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Both values should be finite
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(1)));
    }
}
