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
    public void Historical_Update()
    {
        var indicator = new Historical(period: 14);
        double initialValue = indicator.Calc(new TBar(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(false));
        }
        double finalValue = indicator.Calc(new TBar(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Realized_Update()
    {
        var indicator = new Realized(period: 14);
        double initialValue = indicator.Calc(new TBar(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(false));
        }
        double finalValue = indicator.Calc(new TBar(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }

    [Fact]
    public void Rvi_Update()
    {
        var indicator = new Rvi(period: 14);
        double initialValue = indicator.Calc(new TBar(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            indicator.Calc(GetRandomBar(false));
        }
        double finalValue = indicator.Calc(new TBar(DateTime.Now, ReferenceValue, ReferenceValue, ReferenceValue, ReferenceValue, 1000, IsNew: false));

        Assert.Equal(initialValue, finalValue, precision);
    }
}
