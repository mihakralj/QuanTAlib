using Xunit;

namespace QuanTAlib.Tests;

public class OscillatorsUpdateTests : UpdateTestBase
{
    [Fact]
    public void Rsi_Update()
    {
        var indicator = new Rsi(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Rsx_Update()
    {
        var indicator = new Rsx(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Cmo_Update()
    {
        var indicator = new Cmo(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Ao_Update()
    {
        var indicator = new Ao();
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Ac_Update()
    {
        var indicator = new Ac();
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Aroon_Update()
    {
        var indicator = new Aroon(period: 25);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Bop_Update()
    {
        var indicator = new Bop();
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Cci_Update()
    {
        var indicator = new Cci(period: 20);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Cfo_Update()
    {
        var indicator = new Cfo(period: 14);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Chop_Update()
    {
        var indicator = new Chop(period: 14);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Cog_Update()
    {
        var indicator = new Cog(period: 10);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Coppock_Update()
    {
        var indicator = new Coppock(roc1Period: 14, roc2Period: 11, wmaPeriod: 10);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Crsi_Update()
    {
        var indicator = new Crsi(period1: 10, period2: 14, period3: 30);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Smi_Update()
    {
        var indicator = new Smi(period: 10, smooth1: 3, smooth2: 3);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Srsi_Update()
    {
        var indicator = new Srsi(rsiPeriod: 14, stochPeriod: 14, smoothK: 3, smoothD: 3);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Stc_Update()
    {
        var indicator = new Stc(cyclePeriod: 10, fastPeriod: 23, slowPeriod: 50);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Stoch_Update()
    {
        var indicator = new Stoch(period: 14, smoothK: 3, smoothD: 3);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Tsi_Update()
    {
        var indicator = new Tsi(firstPeriod: 25, secondPeriod: 13);
        TestTValueUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Uo_Update()
    {
        var indicator = new Uo(period1: 7, period2: 14, period3: 28);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Willr_Update()
    {
        var indicator = new Willr(period: 14);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Dosc_Update()
    {
        var indicator = new Dosc();
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Efi_Update()
    {
        var indicator = new Efi(period: 13);
        TestTBarUpdate(indicator, indicator.Calc);
    }

    [Fact]
    public void Fisher_Update()
    {
        var indicator = new Fisher(period: 10);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Cti_Update()
    {
        var indicator = new Cti(period: 20);
        TestTValueUpdate(indicator, indicator.Calc);
    }
}
