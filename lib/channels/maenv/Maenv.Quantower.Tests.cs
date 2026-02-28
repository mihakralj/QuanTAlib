using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class MaenvIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ind = new MaenvIndicator();

        Assert.Equal(20, ind.Period);
        Assert.Equal(1.0, ind.Percentage);
        Assert.Equal(MaenvType.EMA, ind.MaType);
        Assert.Equal(PriceType.Close, ind.SourceType);
        Assert.True(ind.ShowColdValues);
        Assert.Equal("Maenv - Moving Average Envelope", ind.Name);
        Assert.False(ind.SeparateWindow);
        Assert.True(ind.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_EqualsPeriod()
    {
        var ind = new MaenvIndicator { Period = 15 };
        Assert.Equal(15, ind.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_ReflectsParameters()
    {
        var ind = new MaenvIndicator { Period = 12, Percentage = 2.5, MaType = MaenvType.SMA };
        Assert.Contains("12", ind.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", ind.ShortName, StringComparison.Ordinal);
        Assert.Contains("SMA", ind.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_AddsThreeLineSeries()
    {
        var ind = new MaenvIndicator { Period = 14, Percentage = 2.0 };
        ind.Initialize();

        Assert.Equal(3, ind.LinesSeries.Count);
        Assert.Equal("Middle", ind.LinesSeries[0].Name);
        Assert.Equal("Upper", ind.LinesSeries[1].Name);
        Assert.Equal("Lower", ind.LinesSeries[2].Name);
    }

    [Fact]
    public void ProcessUpdate_Historical_ComputesValues()
    {
        var ind = new MaenvIndicator { Period = 3, Percentage = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, ind.LinesSeries[0].Count);
        Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(0)));
        Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_Appends()
    {
        var ind = new MaenvIndicator { Period = 3, Percentage = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 102);
        ind.HistoricalData.AddBar(now.AddMinutes(1), 102, 112, 92, 104);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, ind.LinesSeries[0].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_DoesNotThrow()
    {
        var ind = new MaenvIndicator { Period = 5, Percentage = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 105, 95, 102);

        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, ind.LinesSeries[0].Count);
    }

    [Fact]
    public void MultipleUpdates_ProducesFiniteSeries()
    {
        var ind = new MaenvIndicator { Period = 5, Percentage = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(20, ind.LinesSeries[0].Count);
        Assert.Equal(20, ind.LinesSeries[1].Count);
        Assert.Equal(20, ind.LinesSeries[2].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(i)));
        }
    }

    [Fact]
    public void Bands_Order_Correct()
    {
        var ind = new MaenvIndicator { Period = 5, Percentage = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 100, 1000);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        Assert.True(upper > middle, $"Upper ({upper}) should be > Middle ({middle})");
        Assert.True(lower < middle, $"Lower ({lower}) should be < Middle ({middle})");
    }

    [Fact]
    public void FirstBar_BandsAtPercentage()
    {
        var ind = new MaenvIndicator { Period = 10, Percentage = 2.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        ind.HistoricalData.AddBar(now, 100, 110, 90, 100);
        ind.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        // First bar: middle = close, bands at ±2%
        Assert.Equal(100.0, middle, 1e-10);
        Assert.Equal(102.0, upper, 1e-10);
        Assert.Equal(98.0, lower, 1e-10);
    }

    [Fact]
    public void Percentage_AffectsBandWidth()
    {
        var ind1 = new MaenvIndicator { Period = 10, Percentage = 1.0 };
        var ind2 = new MaenvIndicator { Period = 10, Percentage = 2.0 };
        ind1.Initialize();
        ind2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            ind1.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 100);
            ind2.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 100);
            ind1.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            ind2.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double width1 = ind1.LinesSeries[1].GetValue(0) - ind1.LinesSeries[2].GetValue(0);
        double width2 = ind2.LinesSeries[1].GetValue(0) - ind2.LinesSeries[2].GetValue(0);

        Assert.Equal(width2, width1 * 2, 1e-9);
    }

    [Fact]
    public void Bands_Symmetric_AroundMiddle()
    {
        var ind = new MaenvIndicator { Period = 10, Percentage = 3.0 };
        ind.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 100 + i);
            ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double middle = ind.LinesSeries[0].GetValue(0);
        double upper = ind.LinesSeries[1].GetValue(0);
        double lower = ind.LinesSeries[2].GetValue(0);

        double upperDist = upper - middle;
        double lowerDist = middle - lower;

        Assert.Equal(upperDist, lowerDist, 1e-10);
    }

    [Fact]
    public void AllmaTypes_ProduceFiniteResults()
    {
        foreach (MaenvType maType in Enum.GetValues<MaenvType>())
        {
            var ind = new MaenvIndicator { Period = 10, Percentage = 2.0, MaType = maType };
            ind.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 20; i++)
            {
                ind.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 100 + i);
                ind.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            }

            for (int i = 0; i < 20; i++)
            {
                Assert.True(double.IsFinite(ind.LinesSeries[0].GetValue(i)), $"{maType} Middle finite at {i}");
                Assert.True(double.IsFinite(ind.LinesSeries[1].GetValue(i)), $"{maType} Upper finite at {i}");
                Assert.True(double.IsFinite(ind.LinesSeries[2].GetValue(i)), $"{maType} Lower finite at {i}");
            }
        }
    }

    [Fact]
    public void DifferentPriceTypes_Work()
    {
        var indClose = new MaenvIndicator { Period = 5, Percentage = 1.0, SourceType = PriceType.Close };
        var indHigh = new MaenvIndicator { Period = 5, Percentage = 1.0, SourceType = PriceType.High };
        indClose.Initialize();
        indHigh.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indClose.HistoricalData.AddBar(now.AddMinutes(i), 100, 120, 80, 100);
            indHigh.HistoricalData.AddBar(now.AddMinutes(i), 100, 120, 80, 100);
            indClose.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            indHigh.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // High should be higher than Close for the same percentage
        double closeMiddle = indClose.LinesSeries[0].GetValue(0);
        double highMiddle = indHigh.LinesSeries[0].GetValue(0);

        Assert.True(highMiddle > closeMiddle, "High price type should produce higher middle than Close");
    }
}
