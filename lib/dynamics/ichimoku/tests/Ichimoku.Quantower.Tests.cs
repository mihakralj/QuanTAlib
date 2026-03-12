using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class IchimokuIndicatorTests
{
    [Fact]
    public void IchimokuIndicator_Constructor_SetsDefaults()
    {
        var indicator = new IchimokuIndicator();

        Assert.Equal(9, indicator.TenkanPeriod);
        Assert.Equal(26, indicator.KijunPeriod);
        Assert.Equal(52, indicator.SenkouBPeriod);
        Assert.Equal(26, indicator.Displacement);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Ichimoku Kinko Hyo", indicator.Name);
        Assert.False(indicator.SeparateWindow); // Overlay on price chart
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void IchimokuIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new IchimokuIndicator { TenkanPeriod = 10 };

        Assert.Equal(0, IchimokuIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void IchimokuIndicator_ShortName_IncludesParameters()
    {
        var indicator = new IchimokuIndicator { TenkanPeriod = 9, KijunPeriod = 26, SenkouBPeriod = 52 };
        indicator.Initialize();

        Assert.Contains("ICHIMOKU", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("9", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("26", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("52", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void IchimokuIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new IchimokuIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ichimoku.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void IchimokuIndicator_Initialize_CreatesInternalIchimoku()
    {
        var indicator = new IchimokuIndicator { TenkanPeriod = 9, KijunPeriod = 26, SenkouBPeriod = 52 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Tenkan, Kijun, SenkouA, SenkouB, Chikou)
        Assert.Equal(5, indicator.LinesSeries.Count);
    }

    [Fact]
    public void IchimokuIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new IchimokuIndicator { TenkanPeriod = 9, KijunPeriod = 26, SenkouBPeriod = 52 };
        indicator.Initialize();

        // Add historical data - need enough bars for longest period (SenkouB = 52)
        var now = DateTime.UtcNow;
        for (int i = 0; i < 60; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have values
        double tenkan = indicator.LinesSeries[0].GetValue(0);
        double kijun = indicator.LinesSeries[1].GetValue(0);
        double senkouA = indicator.LinesSeries[2].GetValue(0);
        double senkouB = indicator.LinesSeries[3].GetValue(0);
        double chikou = indicator.LinesSeries[4].GetValue(0);

        Assert.True(double.IsFinite(tenkan));
        Assert.True(double.IsFinite(kijun));
        Assert.True(double.IsFinite(senkouA));
        Assert.True(double.IsFinite(senkouB));
        Assert.True(double.IsFinite(chikou));
    }

    [Fact]
    public void IchimokuIndicator_FiveLineSeries_HaveCorrectNames()
    {
        var indicator = new IchimokuIndicator();
        indicator.Initialize();

        Assert.Equal(5, indicator.LinesSeries.Count);
        Assert.Equal("Tenkan-sen", indicator.LinesSeries[0].Name);
        Assert.Equal("Kijun-sen", indicator.LinesSeries[1].Name);
        Assert.Equal("Senkou A", indicator.LinesSeries[2].Name);
        Assert.Equal("Senkou B", indicator.LinesSeries[3].Name);
        Assert.Equal("Chikou", indicator.LinesSeries[4].Name);
    }

    [Fact]
    public void IchimokuIndicator_CustomParameters_AppliesCorrectly()
    {
        var indicator = new IchimokuIndicator
        {
            TenkanPeriod = 10,
            KijunPeriod = 30,
            SenkouBPeriod = 60,
            Displacement = 30
        };
        indicator.Initialize();

        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("60", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void IchimokuIndicator_ConstantPrice_ProducesEqualLines()
    {
        var indicator = new IchimokuIndicator
        {
            TenkanPeriod = 3,
            KijunPeriod = 5,
            SenkouBPeriod = 10,
            Displacement = 5
        };
        indicator.Initialize();

        // Add constant price bars
        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // All Donchian midpoints should equal 100
        double tenkan = indicator.LinesSeries[0].GetValue(0);
        double kijun = indicator.LinesSeries[1].GetValue(0);
        double senkouA = indicator.LinesSeries[2].GetValue(0);
        double senkouB = indicator.LinesSeries[3].GetValue(0);

        Assert.Equal(100.0, tenkan, precision: 10);
        Assert.Equal(100.0, kijun, precision: 10);
        Assert.Equal(100.0, senkouA, precision: 10);
        Assert.Equal(100.0, senkouB, precision: 10);
    }

    [Fact]
    public void IchimokuIndicator_TrendingMarket_ComputesCorrectly()
    {
        var indicator = new IchimokuIndicator
        {
            TenkanPeriod = 3,
            KijunPeriod = 5,
            SenkouBPeriod = 10,
            Displacement = 5
        };
        indicator.Initialize();

        // Add uptrending bars
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + (i * 2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // In uptrend, faster lines should be higher
        double tenkan = indicator.LinesSeries[0].GetValue(0);
        double kijun = indicator.LinesSeries[1].GetValue(0);

        Assert.True(tenkan >= kijun);
    }

    [Fact]
    public void IchimokuIndicator_Chikou_EqualsClose()
    {
        var indicator = new IchimokuIndicator
        {
            TenkanPeriod = 3,
            KijunPeriod = 5,
            SenkouBPeriod = 10,
            Displacement = 5
        };
        indicator.Initialize();

        // Add bars with specific close price
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105.5);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double chikou = indicator.LinesSeries[4].GetValue(0);
        Assert.Equal(105.5, chikou, precision: 10);
    }
}
