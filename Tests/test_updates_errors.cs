using Xunit;
using System.Security.Cryptography;

namespace QuanTAlib.Tests;

public class UpdateTests
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
    public void Huberloss_Update()
    {
        var indicator = new Huber(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mae_Update()
    {
        var indicator = new Mae(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mapd_Update()
    {
        var indicator = new Mapd(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mape_Update()
    {
        var indicator = new Mape(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mase_Update()
    {
        var indicator = new Mase(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mda_Update()
    {
        var indicator = new Mda(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Me_Update()
    {
        var indicator = new Me(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mpe_Update()
    {
        var indicator = new Mpe(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Mse_Update()
    {
        var indicator = new Mse(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Msle_Update()
    {
        var indicator = new Msle(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rae_Update()
    {
        var indicator = new Rae(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rmse_Update()
    {
        var indicator = new Rmse(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rmsle_Update()
    {
        var indicator = new Rmsle(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rse_Update()
    {
        var indicator = new Rse(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Smape_Update()
    {
        var indicator = new Smape(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rsquared_Update()
    {
        var indicator = new Rsquared(period: 14);
        double initialValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        double finalValue = indicator.Calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }
}
