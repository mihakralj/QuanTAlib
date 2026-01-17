using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SqrtIndicatorTests
{
    [Fact]
    public void SqrtIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SqrtIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SQRT - Square Root Transform", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SqrtIndicator_MinHistoryDepths_IsOne()
    {
        var indicator = new SqrtIndicator();
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void SqrtIndicator_ShortName_IsCorrect()
    {
        var indicator = new SqrtIndicator();
        Assert.Equal("SQRT", indicator.ShortName);
    }

    [Fact]
    public void SqrtIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new SqrtIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Sqrt", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void SqrtIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SqrtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 100);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Sqrt of 100 is 10.0
        Assert.Equal(10.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void SqrtIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SqrtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 25, 30, 20, 25);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        // Sqrt of 25 is 5.0
        Assert.Equal(5.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void SqrtIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SqrtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 100);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SqrtIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
        };

        foreach (var source in sources)
        {
            var indicator = new SqrtIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 144, 64, 81);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }

    [Fact]
    public void SqrtIndicator_PerfectSquareValues_ComputesExactly()
    {
        var indicator = new SqrtIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Test perfect squares: 1, 4, 9, 16, 25
        double[] squares = { 1, 4, 9, 16, 25 };
        double[] expectedRoots = { 1, 2, 3, 4, 5 };

        for (int i = 0; i < squares.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), squares[i], squares[i] + 1, squares[i] - 1, squares[i]);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));

            Assert.Equal(expectedRoots[i], indicator.LinesSeries[0].GetValue(0), 1e-10);
        }
    }
}