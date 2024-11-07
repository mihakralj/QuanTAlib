using Xunit;
using System.Security.Cryptography;

namespace QuanTAlib.Tests;

public abstract class UpdateTestBase
{
    protected readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
    protected const int RandomUpdates = 100;
    protected const double ReferenceValue = 100.0;
    protected const int precision = 8;

    protected double GetRandomDouble()
    {
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        return ((double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue * 200) - 100; // Range: -100 to 100
    }

    protected TBar GetRandomBar(bool IsNew)
    {
        double open = GetRandomDouble();
        double high = open + Math.Abs(GetRandomDouble());
        double low = open - Math.Abs(GetRandomDouble());
        double close = low + ((high - low) * GetRandomDouble());
        return new TBar(DateTime.Now, open, high, low, close, 1000, IsNew);
    }

    protected void TestTValueUpdate<T>(T indicator, Func<TValue, TValue> calc) where T : class
    {
        var initialValue = calc(new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        var finalValue = calc(new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue.Value, finalValue.Value, precision);
    }

    protected void TestTBarUpdate<T>(T indicator, Func<TBar, TValue> calc) where T : class
    {
        TBar r = GetRandomBar(true);
        var initialValue = calc(r);

        for (int i = 0; i < RandomUpdates; i++)
        {
            calc(GetRandomBar(IsNew: false));
        }
        var finalValue = calc(new TBar(r.Time, r.Open, r.High, r.Low, r.Close, r.Volume, IsNew: false));

        Assert.Equal(initialValue.Value, finalValue.Value, precision);
    }

    protected void TestDualTValueUpdate<T>(T indicator, Func<TValue, TValue, TValue> calc) where T : class
    {
        var initialValue = calc(
            new TValue(DateTime.Now, ReferenceValue, IsNew: true),
            new TValue(DateTime.Now, ReferenceValue, IsNew: true));

        for (int i = 0; i < RandomUpdates; i++)
        {
            calc(
                new TValue(DateTime.Now, GetRandomDouble(), IsNew: false),
                new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
        }
        var finalValue = calc(
            new TValue(DateTime.Now, ReferenceValue, IsNew: false),
            new TValue(DateTime.Now, ReferenceValue, IsNew: false));

        Assert.Equal(initialValue.Value, finalValue.Value, precision);
    }

    protected void TestDualTBarUpdate<T>(T indicator, Func<TBar, TBar, TValue> calc) where T : class
    {
        TBar bar1 = GetRandomBar(true);
        TBar bar2 = GetRandomBar(true);
        var initialValue = calc(bar1, bar2);

        for (int i = 0; i < RandomUpdates; i++)
        {
            calc(GetRandomBar(false), GetRandomBar(false));
        }
        var finalValue = calc(
            new TBar(bar1.Time, bar1.Open, bar1.High, bar1.Low, bar1.Close, bar1.Volume, false),
            new TBar(bar2.Time, bar2.Open, bar2.High, bar2.Low, bar2.Close, bar2.Volume, false));

        Assert.Equal(initialValue.Value, finalValue.Value, precision);
    }
}
