using Xunit;
using System.Security.Cryptography;

namespace QuanTAlib.Tests;

public class OscillatorsUpdateTests
{
    private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
    private const int RandomUpdates = 100;
    private const double ReferenceValue = 100.0;
    private const int precision = 8;

    private double GetRandomDouble()
    {
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        return ((double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue * 200) - 100; // Range: -100 to 100
    }

    private TBar GetRandomBar(bool IsNew)
    {
        double open = GetRandomDouble();
        double high = open + Math.Abs(GetRandomDouble());
        double low = open - Math.Abs(GetRandomDouble());
        double close = low + ((high - low) * GetRandomDouble());
        return new TBar(DateTime.Now, open, high, low, close, 1000, IsNew);
    }

    [Fact]
    public void Rsi_Update()
    {
        var indicator = new Rsi(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rsx_Update()
    {
        var indicator = new Rsx(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Cmo_Update()
    {
        var indicator = new Cmo(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Ao_Update()
    {
        var indicator = new Ao();
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Ac_Update()
    {
        var indicator = new Ac();
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Aroon_Update()
    {
        var indicator = new Aroon(period: 25);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Bop_Update()
    {
        var indicator = new Bop();
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Cci_Update()
    {
        var indicator = new Cci(period: 20);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Cfo_Update()
    {
        var indicator = new Cfo(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Chop_Update()
    {
        var indicator = new Chop(period: 14);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Cog_Update()
    {
        var indicator = new Cog(period: 10);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Smi_Update()
    {
        var indicator = new Smi(period: 10, smooth1: 3, smooth2: 3);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Srsi_Update()
    {
        var indicator = new Srsi(rsiPeriod: 14, stochPeriod: 14, smoothK: 3, smoothD: 3);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Stc_Update()
    {
        var indicator = new Stc(cyclePeriod: 10, fastPeriod: 23, slowPeriod: 50);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Stoch_Update()
    {
        var indicator = new Stoch(period: 14, smoothK: 3, smoothD: 3);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Tsi_Update()
    {
        var indicator = new Tsi(firstPeriod: 25, secondPeriod: 13);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Uo_Update()
    {
        var indicator = new Uo(period1: 7, period2: 14, period3: 28);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Willr_Update()
    {
        var indicator = new Willr(period: 14);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Dosc_Update()
    {
        var indicator = new Dosc();
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Efi_Update()
    {
        var indicator = new Efi(period: 13);
        TBar r = GetRandomBar(true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }
}
