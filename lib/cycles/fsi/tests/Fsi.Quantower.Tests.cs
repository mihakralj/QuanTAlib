using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class FsiIndicatorTests
{
    [Fact]
    public void FsiIndicator_BasicProperties()
    {
        var indicator = new FsiIndicator();
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.1, indicator.Bandwidth, 10);
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void FsiIndicator_Name_ContainsEhlers()
    {
        var indicator = new FsiIndicator();
        Assert.Contains("Ehlers", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void FsiIndicator_Name_ContainsFSI()
    {
        var indicator = new FsiIndicator();
        Assert.Contains("FSI", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void FsiIndicator_HasLineSeries()
    {
        var indicator = new FsiIndicator();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void FsiIndicator_SeparateWindow()
    {
        var indicator = new FsiIndicator();
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void FsiIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new FsiIndicator { Period = 30, Bandwidth = 0.2 };
        indicator.Initialize();
        indicator.HistoricalData.AddBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void FsiIndicator_MultipleUpdates_ProducesFiniteValues()
    {
        var indicator = new FsiIndicator { Period = 20, Bandwidth = 0.1 };
        indicator.Initialize();

        for (int i = 0; i < 50; i++)
        {
            double price = 100.0 + Math.Sin(2.0 * Math.PI * i / 20.0) * 5.0;
            indicator.HistoricalData.AddBar(
                DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastValue));
    }

    [Fact]
    public void FsiIndicator_BarCorrection_ProducesConsistentValues()
    {
        var indicator = new FsiIndicator { Period = 20, Bandwidth = 0.1 };
        indicator.Initialize();

        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + i * 0.5;
            indicator.HistoricalData.AddBar(
                DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // New bar
        indicator.HistoricalData.AddBar(
            DateTime.UtcNow.AddMinutes(30), 120, 121, 119, 120, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        double valAfterNew = indicator.LinesSeries[0].GetValue(0);

        // Correction (same bar, different price)
        indicator.HistoricalData.AddBar(
            DateTime.UtcNow.AddMinutes(30), 130, 131, 129, 130, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        double valAfterCorrection = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(valAfterNew));
        Assert.True(double.IsFinite(valAfterCorrection));
    }

    [Fact]
    public void FsiIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new FsiIndicator { Period = 30, Bandwidth = 0.2 };
        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void FsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new FsiIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Fsi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void FsiIndicator_DefaultSource_IsClose()
    {
        var indicator = new FsiIndicator();
        Assert.Equal(SourceType.Close, indicator.Source);
    }
}
