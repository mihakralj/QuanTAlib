using Xunit;
using System.Security.Cryptography;

namespace QuanTAlib.Tests;

public class VolatilityUpdateTests
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
    public void Adr_Update()
    {
        var indicator = new Adr(period: 14);
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
    public void Atr_Update()
    {
        var indicator = new Atr(period: 14);
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
    public void Ap_Update()
    {
        var indicator = new Ap(period: 20);
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
    public void Atrp_Update()
    {
        var indicator = new Atrp(period: 14);
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
    public void Historical_Update()
    {
        var indicator = new Hv(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Jvolty_Update()
    {
        var indicator = new Jvolty(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Realized_Update()
    {
        var indicator = new Rv(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rvi_Update()
    {
        var indicator = new Rvi(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Cvi_Update()
    {
        var indicator = new Cvi(period: 14);
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
    public void Tr_Update()
    {
        var indicator = new Tr();
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
    public void Ui_Update()
    {
        var indicator = new Ui(period: 14);
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
    public void Vc_Update()
    {
        var indicator = new Vc(period: 20, deviations: 2.0);
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
    public void Vov_Update()
    {
        var indicator = new Vov(period: 20);
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
    public void Vr_Update()
    {
        var indicator = new Vr(shortPeriod: 10, longPeriod: 20);
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
    public void Vs_Update()
    {
        var indicator = new Vs(period: 14, multiplier: 2.0);
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
