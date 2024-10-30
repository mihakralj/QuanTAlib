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
        return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue * 200 - 100; // Range: -100 to 100
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
        TBar r = new(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TBar(DateTime.Now, GetRandomDouble(), GetRandomDouble(), GetRandomDouble(), GetRandomDouble(), 1000, IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Ac_Update()
    {
        var indicator = new Ac();
        TBar r = new(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: true);
        double initialValue = indicator.Calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(new TBar(DateTime.Now, GetRandomDouble(), GetRandomDouble(), GetRandomDouble(), GetRandomDouble(), 1000, IsNew: false));
        }
        double finalValue = indicator.Calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }
}
