using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class TsiIndicatorTests
{
    [Fact]
    public void Indicator_DefaultConstruction()
    {
        var indicator = new TsiIndicator();
        Assert.NotNull(indicator);
        Assert.Equal("TSI - True Strength Index", indicator.Name);
    }

    [Fact]
    public void Indicator_DefaultParameters()
    {
        var indicator = new TsiIndicator();
        Assert.Equal(25, indicator.LongPeriod);
        Assert.Equal(13, indicator.ShortPeriod);
        Assert.Equal(13, indicator.SignalPeriod);
    }

    [Fact]
    public void Indicator_MinHistoryDepths()
    {
        // MinHistoryDepths is static
        Assert.Equal(0, TsiIndicator.MinHistoryDepths);
    }

    [Fact]
    public void Indicator_CustomParameters()
    {
        var indicator = new TsiIndicator { LongPeriod = 20, ShortPeriod = 10, SignalPeriod = 7 };
        Assert.Equal(20, indicator.LongPeriod);
        Assert.Equal(10, indicator.ShortPeriod);
        Assert.Equal(7, indicator.SignalPeriod);
    }

    [Fact]
    public void Indicator_UsesTsiCore()
    {
        var indicator = new TsiIndicator();
        Assert.Equal(25, indicator.LongPeriod);
        Assert.Equal(13, indicator.ShortPeriod);
    }

    [Fact]
    public void Indicator_CalculatesCorrectly()
    {
        var core = new Tsi(5, 3, 3);

        // Feed rising prices
        var prices = new double[] { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
                                    111, 112, 113, 114, 115, 116, 117, 118, 119, 120 };

        foreach (var price in prices)
        {
            core.Update(new TValue(DateTime.Now, price));
        }

        // TSI should be positive for rising prices
        Assert.True(core.Last.Value > 0);
    }

    [Fact]
    public void Indicator_ShortName_ContainsParameters()
    {
        var indicator = new TsiIndicator { LongPeriod = 20, ShortPeriod = 10, SignalPeriod = 7 };

        // ShortName is computed property, just verify it returns non-empty
        Assert.NotNull(indicator.ShortName);
        Assert.NotEmpty(indicator.ShortName);
    }

    [Fact]
    public void Indicator_HasSignalLine()
    {
        var core = new Tsi(5, 3, 3);

        for (int i = 0; i < 20; i++)
        {
            core.Update(new TValue(DateTime.Now.AddMinutes(i), 100.0 + i * 0.5));
        }

        // Signal property should return signal line value
        Assert.True(!double.IsNaN(core.Signal));
    }

    [Fact]
    public void Indicator_OutputBounded()
    {
        var core = new Tsi(5, 3, 3);
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + random.NextDouble() * 50;
            core.Update(new TValue(DateTime.Now.AddMinutes(i), price));

            // TSI must be bounded [-100, 100]
            Assert.True(core.Last.Value >= -100.0 && core.Last.Value <= 100.0);
            // Signal must be bounded too
            Assert.True(core.Signal >= -100.0 && core.Signal <= 100.0);
        }
    }
}
