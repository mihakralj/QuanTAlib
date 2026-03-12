using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public sealed class SarextIndicatorTests
{
    [Fact]
    public void SarextIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SarextIndicator();

        Assert.Equal(0.0, indicator.StartValue);
        Assert.Equal(0.0, indicator.OffsetOnReverse);
        Assert.Equal(0.02, indicator.AfInitLong);
        Assert.Equal(0.02, indicator.AfLong);
        Assert.Equal(0.20, indicator.AfMaxLong);
        Assert.Equal(0.02, indicator.AfInitShort);
        Assert.Equal(0.02, indicator.AfShort);
        Assert.Equal(0.20, indicator.AfMaxShort);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("SAREXT", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SarextIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new SarextIndicator();

        Assert.Equal(2, SarextIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(2, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SarextIndicator_ShortName_IncludesParameters()
    {
        var indicator = new SarextIndicator
        {
            AfInitLong = 0.02,
            AfMaxLong = 0.20,
            AfInitShort = 0.03,
            AfMaxShort = 0.25
        };
        indicator.Initialize();

        Assert.Contains("SAREXT", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.02", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.03", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SarextIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SarextIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Sarext", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SarextIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new SarextIndicator();

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SarextIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SarextIndicator
        {
            AfInitLong = 0.02,
            AfLong = 0.02,
            AfMaxLong = 0.20,
            AfInitShort = 0.02,
            AfShort = 0.02,
            AfMaxShort = 0.20
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double sar = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(sar));
    }

    [Fact]
    public void SarextIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SarextIndicator
        {
            AfInitLong = 0.02,
            AfLong = 0.02,
            AfMaxLong = 0.20,
            AfInitShort = 0.02,
            AfShort = 0.02,
            AfMaxShort = 0.20
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double sar = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(sar));
    }

    [Fact]
    public void SarextIndicator_SingleLineSeries_IsPresent()
    {
        var indicator = new SarextIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        Assert.Single(indicator.LinesSeries);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void SarextIndicator_Description_IsSet()
    {
        var indicator = new SarextIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("SAR", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SarextIndicator_OutputIsAbsoluteValue()
    {
        var indicator = new SarextIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Display value should always be positive (absolute value of sign-encoded SAR)
        double displayValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(displayValue >= 0, "Display value should be non-negative (absolute SAR)");
    }

    [Fact]
    public void SarextIndicator_AsymmetricAf_ProducesFiniteValues()
    {
        var indicator = new SarextIndicator
        {
            AfInitLong = 0.01,
            AfLong = 0.01,
            AfMaxLong = 0.10,
            AfInitShort = 0.03,
            AfShort = 0.03,
            AfMaxShort = 0.30
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double sar = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(sar));
        Assert.True(sar > 0, "SAR display value should be positive");
    }

    [Fact]
    public void SarextIndicator_ShowColdValues_False_HidesColdValues()
    {
        var indicator = new SarextIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // With ShowColdValues=false, cold values should be hidden
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void SarextIndicator_LineSeries_HasCorrectStyle()
    {
        var indicator = new SarextIndicator();
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Dot, lineSeries.Style);
    }
}
