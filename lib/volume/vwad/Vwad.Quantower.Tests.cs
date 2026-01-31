using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VwadIndicatorTests
{
    [Fact]
    public void VwadIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VwadIndicator();

        Assert.Equal("VWAD - Volume Weighted Accumulation/Distribution", indicator.Name);
        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void VwadIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new VwadIndicator { Period = 14 };
        Assert.Equal("VWAD(14)", indicator.ShortName);
    }

    [Fact]
    public void VwadIndicator_MinHistoryDepths_EqualsDefault()
    {
        var indicator = new VwadIndicator();

        Assert.Equal(20, indicator.MinHistoryDepths);
        Assert.Equal(20, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VwadIndicator_Initialize_CreatesInternalVwad()
    {
        var indicator = new VwadIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VwadIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VwadIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void VwadIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VwadIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VwadIndicator_Value_IsCumulative()
    {
        var indicator = new VwadIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1; // Alternate high/low closes
            double volume = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (i > 0)
            {
                double val = indicator.LinesSeries[0].GetValue(0);
                values.Add(val);
            }
        }

        // VWAD is cumulative and unbounded - values should change over time
        Assert.True(values.Count > 0, "Should have recorded values");

        // Check that values are changing (not all the same)
        int changeCount = 0;
        for (int i = 1; i < values.Count; i++)
        {
            if (Math.Abs(values[i] - values[i - 1]) > 1e-10)
            {
                changeCount++;
            }
        }
        Assert.True(changeCount > values.Count / 2, "VWAD values should change for most bars");
    }

    [Fact]
    public void VwadIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator10 = new VwadIndicator { Period = 10 };
        var indicator20 = new VwadIndicator { Period = 20 };

        indicator10.Initialize();
        indicator20.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double open = 100 + i;
            double high = open + 10;
            double low = open - 5;
            double close = open + 5;
            double volume = 1000 + (i * 50);

            indicator10.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator20.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);

            indicator10.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator20.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val10 = indicator10.LinesSeries[0].GetValue(0);
        double val20 = indicator20.LinesSeries[0].GetValue(0);

        // Different periods should produce different results
        Assert.NotEqual(val10, val20, 6);
    }
}