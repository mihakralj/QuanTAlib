using Xunit;
using System.Security.Cryptography;

namespace QuanTAlib.Tests;

public class MomentumUpdateTests
{
    private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
    private const int RandomUpdates = 100;
    private const double ReferenceValue = 100.0;
    private const int precision = 8;

    private double GetRandomDouble()
    {
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue * 200 - 100; // Range: -100 to 100
    }

    private TBar GetRandomBar(bool IsNew)
    {
        double open = GetRandomDouble();
        double high = open + Math.Abs(GetRandomDouble());
        double low = open - Math.Abs(GetRandomDouble());
        double close = low + (high - low) * GetRandomDouble();
        return new TBar(DateTime.Now, open, high, low, close, 1000, IsNew);
    }

    [Fact]
    public void Adx_Update()
    {
        var indicator = new Adx(period: 14);
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
    public void Adxr_Update()
    {
        var indicator = new Adxr(period: 14);
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
    public void Apo_Update()
    {
        var indicator = new Apo(fastPeriod: 12, slowPeriod: 26);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Dmi_Update()
    {
        var indicator = new Dmi(period: 14);
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
    public void Dmx_Update()
    {
        var indicator = new Dmx(period: 14);
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
    public void Dpo_Update()
    {
        var indicator = new Dpo(period: 20);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Macd_Update()
    {
        var indicator = new Macd(fastPeriod: 12, slowPeriod: 26, signalPeriod: 9);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble() + 100, IsNew: false)); // Ensure positive prices
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Pmo_Update()
    {
        var indicator = new Pmo(period1: 35, period2: 20);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Po_Update()
    {
        var indicator = new Po(fastPeriod: 10, slowPeriod: 21);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Ppo_Update()
    {
        var indicator = new Ppo(fastPeriod: 12, slowPeriod: 26);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Prs_Update()
    {
        var indicator = new Prs();
        indicator.SetBenchmark(ReferenceValue);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.SetBenchmark(GetRandomDouble() + 100); // Ensure positive benchmark
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }

        indicator.SetBenchmark(ReferenceValue);
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Roc_Update()
    {
        var indicator = new Roc(period: 12);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble() + 100, IsNew: false)); // Ensure positive prices
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mom_Update()
    {
        var indicator = new Mom(period: 10);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Trix_Update()
    {
        var indicator = new Trix(period: 18);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble() + 100, IsNew: false)); // Ensure positive prices
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Tsi_Update()
    {
        var indicator = new Tsi(firstPeriod: 25, secondPeriod: 13);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble() + 100, IsNew: false)); // Ensure positive prices
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Vel_Update()
    {
        var indicator = new Vel(period: 10);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Vortex_Update()
    {
        var indicator = new Vortex(period: 14);
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
