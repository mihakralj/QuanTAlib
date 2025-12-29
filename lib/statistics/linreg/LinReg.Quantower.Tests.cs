using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class LinRegIndicatorTests
{
    [Fact]
    public void LinRegIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LinRegIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(0, indicator.Offset);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LinReg - Linear Regression Curve", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void LinRegIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LinRegIndicator { Period = 20 };

        Assert.Equal(0, LinRegIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void LinRegIndicator_Initialize_CreatesInternalLinReg()
    {
        var indicator = new LinRegIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("LinReg", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void LinRegIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LinRegIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double linreg = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(linreg));
    }
}

public class LinRegSlopeIndicatorTests
{
    [Fact]
    public void LinRegSlopeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LinRegSlopeIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LinReg Slope", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void LinRegSlopeIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LinRegSlopeIndicator { Period = 20 };

        Assert.Equal(0, LinRegSlopeIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void LinRegSlopeIndicator_Initialize_CreatesInternalLinReg()
    {
        var indicator = new LinRegSlopeIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Slope", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void LinRegSlopeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LinRegSlopeIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double slope = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(slope));
    }
}

public class LinRegInterceptIndicatorTests
{
    [Fact]
    public void LinRegInterceptIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LinRegInterceptIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LinReg Intercept", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void LinRegInterceptIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LinRegInterceptIndicator { Period = 20 };

        Assert.Equal(0, LinRegInterceptIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void LinRegInterceptIndicator_Initialize_CreatesInternalLinReg()
    {
        var indicator = new LinRegInterceptIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Intercept", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void LinRegInterceptIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LinRegInterceptIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double intercept = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(intercept));
    }
}

public class LinRegRSquaredIndicatorTests
{
    [Fact]
    public void LinRegRSquaredIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LinRegRSquaredIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LinReg R-Squared", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void LinRegRSquaredIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new LinRegRSquaredIndicator { Period = 20 };

        Assert.Equal(0, LinRegRSquaredIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void LinRegRSquaredIndicator_Initialize_CreatesInternalLinReg()
    {
        var indicator = new LinRegRSquaredIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("RSquared", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void LinRegRSquaredIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LinRegRSquaredIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double r2 = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(r2));
    }
}
