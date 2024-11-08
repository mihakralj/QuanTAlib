using Xunit;

namespace QuanTAlib.Tests;

public class VolatilityUpdateTests : UpdateTestBase
{
    [Fact]
    public void Adr_Update()
    {
        var indicator = new Adr(period: 14);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Atr_Update()
    {
        var indicator = new Atr(period: 14);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Atrs_Update()
    {
        var indicator = new Atrs(period: 14, factor: 2.0);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Ap_Update()
    {
        var indicator = new Ap(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Atrp_Update()
    {
        var indicator = new Atrp(period: 14);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Bband_Update()
    {
        var indicator = new Bband(period: 20, multiplier: 2.0);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Ccv_Update()
    {
        var indicator = new Ccv(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Ce_Update()
    {
        var indicator = new Ce(period: 22, multiplier: 3.0);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Cv_Update()
    {
        var indicator = new Cv(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Cvi_Update()
    {
        var indicator = new Cvi(period: 10, smoothPeriod: 10);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Dchn_Update()
    {
        var indicator = new Dchn(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Ewma_Update()
    {
        var indicator = new Ewma(period: 20, lambda: 0.94);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Fcb_Update()
    {
        var indicator = new Fcb(period: 20, smoothing: 0.5);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Gkv_Update()
    {
        var indicator = new Gkv(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Historical_Update()
    {
        var indicator = new Hv(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Hlv_Update()
    {
        var indicator = new Hlv(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Jvolty_Update()
    {
        var indicator = new Jvolty(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Natr_Update()
    {
        var indicator = new Natr(period: 14);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Pch_Update()
    {
        var indicator = new Pch(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Pv_Update()
    {
        var indicator = new Pv(period: 10);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Realized_Update()
    {
        var indicator = new Rv(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Rsv_Update()
    {
        var indicator = new Rsv(period: 10);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Rvi_Update()
    {
        var indicator = new Rvi(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Sv_Update()
    {
        var indicator = new Sv(period: 20, lambda: 0.94);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Tr_Update()
    {
        var indicator = new Tr();
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Ui_Update()
    {
        var indicator = new Ui(period: 14);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Vc_Update()
    {
        var indicator = new Vc(period: 20, deviations: 2.0);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Vov_Update()
    {
        var indicator = new Vov(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Vr_Update()
    {
        var indicator = new Vr(shortPeriod: 10, longPeriod: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Vs_Update()
    {
        var indicator = new Vs(period: 14, multiplier: 2.0);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Yzv_Update()
    {
        var indicator = new Yzv(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }
}
