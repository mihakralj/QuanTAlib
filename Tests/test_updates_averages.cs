using Xunit;
using System.Security.Cryptography;

namespace QuanTAlib.Tests;

public class AveragesUpdateTests
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

    [Fact]
    public void Afirma_Update()
    {
        var indicator = new Afirma(periods: 14, taps: 4, window: Afirma.WindowType.Blackman);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Alma_Update()
    {
        var indicator = new Alma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Convolution_Update()
    {
        var indicator = new Convolution(new double[] { 1, 2, 3, 2, 1 });
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Dema_Update()
    {
        var indicator = new Dema(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Dsma_Update()
    {
        var indicator = new Dsma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Dwma_Update()
    {
        var indicator = new Dwma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Ema_Update()
    {
        var indicator = new Ema(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Epma_Update()
    {
        var indicator = new Epma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Frama_Update()
    {
        var indicator = new Frama(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Fwma_Update()
    {
        var indicator = new Fwma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Gma_Update()
    {
        var indicator = new Gma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Hma_Update()
    {
        var indicator = new Hma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Htit_Update()
    {
        var indicator = new Htit();
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Hwma_Update()
    {
        var indicator = new Hwma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Jma_Update()
    {
        var indicator = new Jma(period: 14, phase: 0);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Kama_Update()
    {
        var indicator = new Kama(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Ltma_Update()
    {
        var indicator = new Ltma(gamma: 0.2);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Maaf_Update()
    {
        var indicator = new Maaf(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mama_Update()
    {
        var indicator = new Mama(fastLimit: 0.5, slowLimit: 0.05);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mgdi_Update()
    {
        var indicator = new Mgdi(period: 14, kFactor: 0.6);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mma_Update()
    {
        var indicator = new Mma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Pwma_Update()
    {
        var indicator = new Pwma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Qema_Update()
    {
        var indicator = new Qema(k1: 0.2, k2: 0.2, k3: 0.2, k4: 0.2);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rema_Update()
    {
        var indicator = new Rema(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rma_Update()
    {
        var indicator = new Rma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Sinema_Update()
    {
        var indicator = new Sinema(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Sma_Update()
    {
        var indicator = new Sma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Smma_Update()
    {
        var indicator = new Smma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void T3_Update()
    {
        var indicator = new T3(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Tema_Update()
    {
        var indicator = new Tema(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Trima_Update()
    {
        var indicator = new Trima(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Vidya_Update()
    {
        var indicator = new Vidya(shortPeriod: 14, longPeriod: 30, alpha: 0.2);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Wma_Update()
    {
        var indicator = new Wma(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Zlema_Update()
    {
        var indicator = new Zlema(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }
}
