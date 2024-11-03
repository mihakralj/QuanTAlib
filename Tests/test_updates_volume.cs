using Xunit;
using System.Security.Cryptography;

namespace QuanTAlib.Tests;

public class VolumeUpdateTests
{
    private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
    private const int RandomUpdates = 100;
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
        double volume = Math.Abs(GetRandomDouble()) * 1000; // Random positive volume
        return new TBar(DateTime.Now, open, high, low, close, volume, IsNew);
    }

    [Fact]
    public void Adl_Update()
    {
        var indicator = new Adl();
        TBar r = GetRandomBar(true);

        // First calculation with IsNew: true
        double value1 = indicator.Calc(r);

        // Multiple recalculations with IsNew: false should not change the value
        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));
        }

        // Final calculation with IsNew: false should match initial value
        double value2 = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));
        Assert.Equal(value1, value2, precision);

        // New calculation with IsNew: true should update the value
        double value3 = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: true));
        Assert.NotEqual(value1, value3, precision);
    }

    [Fact]
    public void Adosc_Update()
    {
        var indicator = new Adosc(shortPeriod: 3, longPeriod: 10);
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
    public void Aobv_Update()
    {
        var indicator = new Aobv();
        TBar r = GetRandomBar(true);

        // First calculation with IsNew: true
        double value1 = indicator.Calc(r);

        // Multiple recalculations with IsNew: false should not change the value
        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));
        }

        // Final calculation with IsNew: false should match initial value
        double value2 = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));
        Assert.Equal(value1, value2, precision);

        // New calculation with IsNew: true should update the value
        double value3 = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: true));
        Assert.NotEqual(value1, value3, precision);
    }

    [Fact]
    public void Cmf_Update()
    {
        var indicator = new Cmf(period: 20);
        TBar r = GetRandomBar(true);

        // Generate a sequence of bars for warmup
        var warmupBars = new List<TBar>();
        for (int i = 0; i < indicator.WarmupPeriod; i++)
        {
            var bar = GetRandomBar(IsNew: true);
            warmupBars.Add(bar);
            indicator.Calc(bar);
        }

        // Calculate initial value after warmup
        double initialValue = indicator.Calc(r);

        // Apply random updates
        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }

        // Reset and replay the same sequence
        indicator.Init();
        foreach (var bar in warmupBars)
        {
            indicator.Calc(bar);
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Eom_Update()
    {
        var indicator = new Eom(period: 14);
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
    public void Kvo_Update()
    {
        var indicator = new Kvo(shortPeriod: 34, longPeriod: 55);
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
    public void Mfi_Update()
    {
        var indicator = new Mfi(period: 14);
        TBar r = GetRandomBar(true);

        // Generate a sequence of bars for warmup
        var warmupBars = new List<TBar>();
        for (int i = 0; i < indicator.WarmupPeriod; i++)
        {
            var bar = GetRandomBar(IsNew: true);
            warmupBars.Add(bar);
            indicator.Calc(bar);
        }

        // Calculate initial value after warmup
        double initialValue = indicator.Calc(r);

        // Apply random updates
        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(IsNew: false));
        }

        // Reset and replay the same sequence
        indicator.Init();
        foreach (var bar in warmupBars)
        {
            indicator.Calc(bar);
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Nvi_Update()
    {
        var indicator = new Nvi();
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
    public void Obv_Update()
    {
        var indicator = new Obv();
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
    public void Pvi_Update()
    {
        var indicator = new Pvi();
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
    public void Pvol_Update()
    {
        var indicator = new Pvol();
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
    public void Pvo_Update()
    {
        var indicator = new Pvo(shortPeriod: 12, longPeriod: 26);
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
    public void Pvr_Update()
    {
        var indicator = new Pvr();
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
    public void Pvt_Update()
    {
        var indicator = new Pvt();
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
    public void Tvi_Update()
    {
        var indicator = new Tvi(minTick: 0.5);
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
    public void Vf_Update()
    {
        var indicator = new Vf(period: 13);
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
    public void Vp_Update()
    {
        var indicator = new Vp(period: 14);
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
    public void Vwap_Update()
    {
        var indicator = new Vwap();
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
    public void Vwma_Update()
    {
        var indicator = new Vwma(period: 20);
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
