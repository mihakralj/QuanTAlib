using Xunit;

namespace QuanTAlib.Tests;

public class StatisticsUpdateTests : UpdateTestBase
{
    [Fact]
    public void Beta_Update()
    {
        var indicator = new Beta(period: 14);
        TestDualTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Corr_Update()
    {
        var indicator = new Corr(period: 14);
        TestDualTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Covar_Update()
    {
        var indicator = new Covar(period: 14);
        TestDualTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Curvature_Update()
    {
        var indicator = new Curvature(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Entropy_Update()
    {
        var indicator = new Entropy(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Hurst_Update()
    {
        var indicator = new Hurst(period: 100, minLength: 10);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Kendall_Update()
    {
        var indicator = new Kendall(period: 14);
        TestDualTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Kurtosis_Update()
    {
        var indicator = new Kurtosis(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Max_Update()
    {
        var indicator = new Max(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Median_Update()
    {
        var indicator = new Median(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Min_Update()
    {
        var indicator = new Min(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Mode_Update()
    {
        var indicator = new Mode(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Percentile_Update()
    {
        var indicator = new Percentile(period: 14, percent: 50);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Skew_Update()
    {
        var indicator = new Skew(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Slope_Update()
    {
        var indicator = new Slope(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Spearman_Update()
    {
        var indicator = new Spearman(period: 14);
        TestDualTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Stddev_Update()
    {
        var indicator = new Stddev(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Theil_Update()
    {
        var indicator = new Theil(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Tsf_Update()
    {
        var indicator = new Tsf(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Variance_Update()
    {
        var indicator = new Variance(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Zscore_Update()
    {
        var indicator = new Zscore(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }
}
