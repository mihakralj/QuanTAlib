using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class GatorIndicatorTests
{
    [Fact]
    public void GatorIndicator_Constructor_SetsDefaults()
    {
        var indicator = new GatorIndicator();

        Assert.Equal(13, indicator.JawPeriod);
        Assert.Equal(8, indicator.JawShift);
        Assert.Equal(8, indicator.TeethPeriod);
        Assert.Equal(5, indicator.TeethShift);
        Assert.Equal(5, indicator.LipsPeriod);
        Assert.Equal(3, indicator.LipsShift);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("GATOR - Williams Gator Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void GatorIndicator_ShortName_IncludesParameters()
    {
        var indicator = new GatorIndicator { JawPeriod = 21, TeethPeriod = 13, LipsPeriod = 8 };
        indicator.Initialize();

        Assert.Contains("GATOR", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("21", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("13", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("8", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void GatorIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new GatorIndicator();

        Assert.Equal(0, GatorIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void GatorIndicator_Initialize_CreatesInternalGator()
    {
        var indicator = new GatorIndicator();

        indicator.Initialize();

        // Should have two line series (Upper + Lower)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void GatorIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new GatorIndicator
        {
            JawPeriod = 5, JawShift = 3,
            TeethPeriod = 3, TeethShift = 2,
            LipsPeriod = 2, LipsShift = 1
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double upperVal = indicator.LinesSeries[0].GetValue(0);
        double lowerVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(upperVal));
        Assert.True(double.IsFinite(lowerVal));
        Assert.True(upperVal >= 0);
        Assert.True(lowerVal <= 0);
    }

    [Fact]
    public void GatorIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new GatorIndicator
        {
            JawPeriod = 5, JawShift = 3,
            TeethPeriod = 3, TeethShift = 2,
            LipsPeriod = 2, LipsShift = 1
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 128, 115, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        Assert.Equal(2, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void GatorIndicator_DifferentPeriods_Work()
    {
        int[][] paramSets =
        {
            new[] { 5, 3, 3, 2, 2, 1 },
            new[] { 13, 8, 8, 5, 5, 3 },
            new[] { 21, 13, 13, 8, 8, 5 }
        };

        foreach (var ps in paramSets)
        {
            var indicator = new GatorIndicator
            {
                JawPeriod = ps[0], JawShift = ps[1],
                TeethPeriod = ps[2], TeethShift = ps[3],
                LipsPeriod = ps[4], LipsShift = ps[5]
            };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double upperVal = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(upperVal), $"Periods ({ps[0]},{ps[2]},{ps[4]}) should produce finite upper");
        }
    }

    [Fact]
    public void GatorIndicator_Period_CanBeChanged()
    {
        var indicator = new GatorIndicator();
        Assert.Equal(13, indicator.JawPeriod);

        indicator.JawPeriod = 21;
        indicator.TeethPeriod = 13;
        indicator.LipsPeriod = 8;
        Assert.Equal(21, indicator.JawPeriod);
        Assert.Equal(13, indicator.TeethPeriod);
        Assert.Equal(8, indicator.LipsPeriod);
    }

    [Fact]
    public void GatorIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new GatorIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void GatorIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new GatorIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Gator.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void GatorIndicator_HasTwoLineSeries_WithCorrectNames()
    {
        var indicator = new GatorIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("Upper", indicator.LinesSeries[0].Name);
        Assert.Equal("Lower", indicator.LinesSeries[1].Name);
    }
}
