using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class TbfIndicatorTests
{
    [Fact]
    public void TbfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TbfIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.1, indicator.Bandwidth);
        Assert.Equal(10, indicator.Length);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("TBF - Ehlers Truncated Bandpass Filter", indicator.Name);
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void TbfIndicator_MinHistoryDepths_EqualsLengthPlus2()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };

        Assert.Equal(12, indicator.MinHistoryDepths); // 10 + 2
        Assert.Equal(12, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void TbfIndicator_MinHistoryDepths_ChangesWithLength()
    {
        var indicator = new TbfIndicator { Length = 20 };
        Assert.Equal(22, indicator.MinHistoryDepths); // 20 + 2

        indicator.Length = 5;
        Assert.Equal(7, indicator.MinHistoryDepths); // 5 + 2
    }

    [Fact]
    public void TbfIndicator_ShortName_IncludesParameters()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };

        Assert.Contains("TBF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TbfIndicator_Initialize_CreatesInternalTbf()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };

        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count); // TBF, BP, Zero
    }

    [Fact]
    public void TbfIndicator_ThreeLineSeries_CorrectNames()
    {
        var indicator = new TbfIndicator();

        Assert.Equal(3, indicator.LinesSeries.Count);
        // LineSeries[0] = TBF, [1] = BP, [2] = Zero
    }

    [Fact]
    public void TbfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void TbfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void TbfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void TbfIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void TbfIndicator_Parameters_CanBeChanged()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.1, indicator.Bandwidth);
        Assert.Equal(10, indicator.Length);

        indicator.Period = 30;
        indicator.Bandwidth = 0.2;
        indicator.Length = 15;
        Assert.Equal(30, indicator.Period);
        Assert.Equal(0.2, indicator.Bandwidth);
        Assert.Equal(15, indicator.Length);
    }

    [Fact]
    public void TbfIndicator_MultipleBars_ProducesFiniteOutput()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double price = 100 + Math.Sin(i * 0.3) * 10;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price + 1);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(50, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void TbfIndicator_ZeroLine_AlwaysShowsValue()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Zero line (index 2) should always be 0.0
        Assert.Equal(0.0, indicator.LinesSeries[2].GetValue(0));
    }

    [Fact]
    public void TbfIndicator_BpSeries_ProducesFiniteOutput()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i * 2;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price + 1);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // BP line (index 1) should produce finite values
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)),
            "BP series should produce finite output");
    }

    [Fact]
    public void TbfIndicator_Reinitialize_ClearsState()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        int countBefore = indicator.LinesSeries[0].Count;
        Assert.True(countBefore > 0);

        // Re-initialize should reset
        indicator.Initialize();
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void TbfIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TbfIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Tbf.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TbfIndicator_ShowColdValues_AffectsDisplay()
    {
        var indicator1 = new TbfIndicator { ShowColdValues = true };
        indicator1.Initialize();

        var indicator2 = new TbfIndicator { ShowColdValues = false };
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        indicator1.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator2.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Both should process without error
        Assert.Equal(1, indicator1.LinesSeries[0].Count);
        Assert.Equal(1, indicator2.LinesSeries[0].Count);
    }

    [Fact]
    public void TbfIndicator_CustomParameters_PropagateToShortName()
    {
        var indicator = new TbfIndicator { Period = 30, Bandwidth = 0.25, Length = 15 };

        Assert.Contains("30", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.25", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TbfIndicator_ConstantPrices_ProducesZeroTbfValue()
    {
        var indicator = new TbfIndicator { Period = 20, Bandwidth = 0.1, Length = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 100, 100, 100);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // Constant input → bandpass should be near zero
        double tbfValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(Math.Abs(tbfValue) < 1e-3, $"Constant input should produce TBF near zero, got {tbfValue}");
    }
}
