using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class UchannelQuantowerTests
{
    #region Constructor Tests

    [Fact]
    public void UchannelIndicator_Constructor_SetsDefaults()
    {
        var indicator = new UchannelIndicator();

        Assert.Equal(20, indicator.StrPeriod);
        Assert.Equal(20, indicator.CenterPeriod);
        Assert.Equal(1.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("UCHANNEL - Ehlers Ultimate Channel", indicator.Name);
    }

    [Fact]
    public void UchannelIndicator_Constructor_SetsDisplayProperties()
    {
        var indicator = new UchannelIndicator();

        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    #endregion

    #region MinHistoryDepths Tests

    [Fact]
    public void UchannelIndicator_MinHistoryDepths_ReturnsMaxOfPeriods()
    {
        var indicator1 = new UchannelIndicator { StrPeriod = 10, CenterPeriod = 20 };
        Assert.Equal(20, indicator1.MinHistoryDepths);

        var indicator2 = new UchannelIndicator { StrPeriod = 30, CenterPeriod = 15 };
        Assert.Equal(30, indicator2.MinHistoryDepths);

        var indicator3 = new UchannelIndicator { StrPeriod = 25, CenterPeriod = 25 };
        Assert.Equal(25, indicator3.MinHistoryDepths);
    }

    [Fact]
    public void UchannelIndicator_MinHistoryDepths_ExplicitInterface()
    {
        var indicator = new UchannelIndicator { StrPeriod = 15, CenterPeriod = 30 };

        int explicit_value = ((IWatchlistIndicator)indicator).MinHistoryDepths;

        Assert.Equal(30, explicit_value);
        Assert.Equal(indicator.MinHistoryDepths, explicit_value);
    }

    [Fact]
    public void UchannelIndicator_MinHistoryDepths_MinPeriods()
    {
        var indicator = new UchannelIndicator { StrPeriod = 1, CenterPeriod = 1 };

        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    #endregion

    #region ShortName Tests

    [Fact]
    public void UchannelIndicator_ShortName_FormatsCorrectly()
    {
        var indicator = new UchannelIndicator
        {
            StrPeriod = 15,
            CenterPeriod = 25,
            Multiplier = 2.5
        };

        Assert.Equal("UCHANNEL (15,25,2.5)", indicator.ShortName);
    }

    [Fact]
    public void UchannelIndicator_ShortName_DefaultParameters()
    {
        var indicator = new UchannelIndicator();

        Assert.Equal("UCHANNEL (20,20,1.0)", indicator.ShortName);
    }

    [Fact]
    public void UchannelIndicator_ShortName_UpdatesWithParameters()
    {
        var indicator = new UchannelIndicator();
        Assert.Equal("UCHANNEL (20,20,1.0)", indicator.ShortName);

        indicator.StrPeriod = 10;
        indicator.CenterPeriod = 30;
        indicator.Multiplier = 3.0;

        Assert.Equal("UCHANNEL (10,30,3.0)", indicator.ShortName);
    }

    #endregion

    #region SourceCodeLink Tests

    [Fact]
    public void UchannelIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new UchannelIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Uchannel.cs", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Parameter Tests

    [Fact]
    public void UchannelIndicator_Parameters_CanBeModified()
    {
        var indicator = new UchannelIndicator();

        indicator.StrPeriod = 30;
        indicator.CenterPeriod = 40;
        indicator.Multiplier = 2.0;
        indicator.ShowColdValues = false;

        Assert.Equal(30, indicator.StrPeriod);
        Assert.Equal(40, indicator.CenterPeriod);
        Assert.Equal(2.0, indicator.Multiplier);
        Assert.False(indicator.ShowColdValues);
    }

    #endregion

    #region Description Tests

    [Fact]
    public void UchannelIndicator_Description_IsNotEmpty()
    {
        var indicator = new UchannelIndicator();

        Assert.False(string.IsNullOrWhiteSpace(indicator.Description));
        Assert.Contains("Ultrasmooth", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region LineSeries Tests

    [Fact]
    public void UchannelIndicator_HasFiveLineSeries()
    {
        var indicator = new UchannelIndicator();

        // The constructor adds 5 line series: Middle, Upper, Lower, STR, Width
        Assert.Equal(5, indicator.LinesSeries.Count);
    }

    [Fact]
    public void UchannelIndicator_LineSeries_HaveCorrectNames()
    {
        var indicator = new UchannelIndicator();

        Assert.Equal("Middle", indicator.LinesSeries[0].Name);
        Assert.Equal("Upper", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower", indicator.LinesSeries[2].Name);
        Assert.Equal("STR", indicator.LinesSeries[3].Name);
        Assert.Equal("Width", indicator.LinesSeries[4].Name);
    }

    #endregion

    #region Initialize Tests

    [Fact]
    public void UchannelIndicator_Initialize_DoesNotThrow()
    {
        var indicator = new UchannelIndicator
        {
            StrPeriod = 10,
            CenterPeriod = 15,
            Multiplier = 1.5
        };

        indicator.Initialize();

        Assert.NotNull(indicator);
    }

    [Fact]
    public void UchannelIndicator_Initialize_PreservesLineSeries()
    {
        var indicator = new UchannelIndicator();

        indicator.Initialize();

        // Line series should still be present after init
        Assert.Equal(5, indicator.LinesSeries.Count);
    }

    #endregion

    #region ProcessUpdate Tests

    [Fact]
    public void UchannelIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // All 5 line series should have values
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(1, indicator.LinesSeries[i].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[i].GetValue(0)));
        }
    }

    [Fact]
    public void UchannelIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106, 1500);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void UchannelIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    #endregion

    #region Multiple Updates Tests

    [Fact]
    public void UchannelIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5, Multiplier = 1.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106, 108, 110, 109 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 3, close - 3, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All 5 series should have values for each bar
        for (int s = 0; s < 5; s++)
        {
            Assert.Equal(closes.Length, indicator.LinesSeries[s].Count);
        }

        // All last values should be finite
        for (int s = 0; s < 5; s++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[s].GetValue(0)));
        }
    }

    #endregion

    #region Band Relationship Tests

    [Fact]
    public void UchannelIndicator_BandRelationships_AreCorrect()
    {
        var indicator = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add varied data to generate band width
        double[] closes = { 100, 105, 95, 110, 90, 105, 100, 108, 92, 103 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 5, close - 5, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Get last values: Middle=0, Upper=1, Lower=2, STR=3, Width=4
        double middle = indicator.LinesSeries[0].GetValue(0);
        double upper = indicator.LinesSeries[1].GetValue(0);
        double lower = indicator.LinesSeries[2].GetValue(0);
        double width = indicator.LinesSeries[4].GetValue(0);

        // Band relationships: Upper >= Middle >= Lower
        Assert.True(upper >= middle, $"Upper ({upper}) should be >= Middle ({middle})");
        Assert.True(middle >= lower, $"Middle ({middle}) should be >= Lower ({lower})");

        // Width = Upper - Lower
        Assert.Equal(upper - lower, width, 6);
    }

    #endregion

    #region Multiplier Tests

    [Fact]
    public void UchannelIndicator_Multiplier_AffectsBandWidth()
    {
        var indicator1 = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5, Multiplier = 1.0 };
        var indicator2 = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5, Multiplier = 2.0 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 105, 95, 110, 90, 105, 100, 108, 92, 103 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), close, close + 5, close - 5, close, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), close, close + 5, close - 5, close, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double width1 = indicator1.LinesSeries[4].GetValue(0);
        double width2 = indicator2.LinesSeries[4].GetValue(0);

        // Width2 should be approximately 2x Width1
        Assert.True(Math.Abs(width2 - (2 * width1)) < 0.0001,
            $"Width2 ({width2}) should be ~2x Width1 ({width1})");
    }

    #endregion

    #region Different Period Tests

    [Fact]
    public void UchannelIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator1 = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5 };
        var indicator2 = new UchannelIndicator { StrPeriod = 20, CenterPeriod = 20 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double close = 100 + ((i % 5) * 2);
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), close, close + 3, close - 3, close, 1000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), close, close + 3, close - 3, close, 1000);
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double middle1 = indicator1.LinesSeries[0].GetValue(0);
        double middle2 = indicator2.LinesSeries[0].GetValue(0);

        // Different smoothing periods should produce different middle values
        Assert.NotEqual(middle1, middle2);
    }

    #endregion

    #region STR Series Tests

    [Fact]
    public void UchannelIndicator_STR_IsNonNegative()
    {
        var indicator = new UchannelIndicator { StrPeriod = 5, CenterPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 105, 95, 110, 90, 105, 100, 108, 92, 103 };

        for (int i = 0; i < closes.Length; i++)
        {
            double close = closes[i];
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 5, close - 5, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // STR (smoothed true range) should be non-negative
        double str = indicator.LinesSeries[3].GetValue(0);
        Assert.True(str >= 0, $"STR ({str}) should be >= 0");
    }

    #endregion

    #region ShowColdValues Tests

    [Fact]
    public void UchannelIndicator_ShowColdValues_True_ShowsValues()
    {
        var indicator = new UchannelIndicator
        {
            StrPeriod = 50,
            CenterPeriod = 50,
            ShowColdValues = true
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add fewer bars than warmup
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // With ShowColdValues = true, values should be shown even before warmup
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void UchannelIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new UchannelIndicator
        {
            StrPeriod = 50,
            CenterPeriod = 50,
            ShowColdValues = false
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add fewer bars than warmup
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 102, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // With ShowColdValues = false, cold values should be NaN before warmup
        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    #endregion
}
