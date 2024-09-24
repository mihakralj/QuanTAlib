using Xunit;
using System;
using QuanTAlib;

namespace Basics;
#nullable disable
public class Indicators
{
    private static Type[] maSeriesTypes = new Type[]
    {
    typeof(SMA_Series),
    typeof(EMA_Series),
    typeof(DEMA_Series),
    typeof(TEMA_Series),
    typeof(WMA_Series),
    typeof(ALMA_Series),
    typeof(DWMA_Series),
    typeof(FWMA_Series),
    typeof(HMA_Series),
    typeof(ZLEMA_Series),
    typeof(RMA_Series),
    typeof(HEMA_Series),
    typeof(JMA_Series),
    typeof(CUSUM_Series),
    typeof(SMMA_Series),
    typeof(T3_Series),
    typeof(KAMA_Series),
    typeof(TRIMA_Series),
    typeof(MAMA_Series),
    typeof(HWMA_Series),
  };

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Name_exists(Type classType)
    {
        TSeries data = new("Data") { 1, 2, 3 };

        var MA_Series = Activator.CreateInstance(classType, data, 5, false) as TSeries;
        Assert.NotEmpty(MA_Series.Name);
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Series_Length(Type classType)
    {
        GBM_Feed feed = new(1000);
        TSeries data = feed.OHLC4;

        var MA_Series = Activator.CreateInstance(classType, data, 5, false) as TSeries;
        Assert.Equal(1000, MA_Series.Count);
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Return_data(Type classType)
    {
        TSeries data = new() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var MA_Series = Activator.CreateInstance(classType, data, 5, false) as TSeries;
        var result = MA_Series.Add(20);
        Assert.Equal(result.v, MA_Series.Last.v);
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Update(Type classType)
    {
        TSeries data = new() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var MA_Series = Activator.CreateInstance(classType, data, 5, false) as TSeries;
        var pre_update = MA_Series.Last.v;

        double pre_data = data.Last.v;
        data.Add(20, true);
        data.Add(pre_data, true);

        Assert.Equal(pre_update, MA_Series.Last.v);
        Assert.Equal(data.Count, MA_Series.Count);
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Period_zero(Type classType)
    {
        GBM_Feed feed = new(100);
        TSeries data = feed.OHLC4;

        var MA_Series = Activator.CreateInstance(classType, data, 0, false) as TSeries;
        Assert.Equal(data.Count, MA_Series.Count);
        Assert.False(double.IsNaN(MA_Series.Last.v));
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Reset(Type classType)
    {
        GBM_Feed feed = new(10);
        TSeries data = feed.OHLC4;
        var MA_Series = Activator.CreateInstance(classType, data, 10, false) as TSeries;
        MA_Series.Reset();
        data.Add(0);
        Assert.Equal(data.Last.v, MA_Series.Last.v);
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Period_one(Type classType)
    {
        GBM_Feed feed = new(100);
        TSeries data = feed.OHLC4;

        var MA_Series = Activator.CreateInstance(classType, data, 1, false) as TSeries;
        Assert.InRange(MA_Series.Last.v - data.Last.v, -10e-6, 10e-6);
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void NaN_test(Type classType)
    {
        GBM_Feed feed = new(100);
        TSeries data = feed.OHLC4;

        var MA_Series = Activator.CreateInstance(classType, data, 10, true) as TSeries;
        Assert.True(double.IsNaN(MA_Series[0].v));
        Assert.True(double.IsNaN(MA_Series[8].v));
        Assert.False(double.IsNaN(MA_Series[9].v));
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Edge_numbers(Type classType)
    {
        TSeries data = new() { double.Epsilon, double.PositiveInfinity, double.MaxValue, double.NegativeInfinity };
        var MA_Series = Activator.CreateInstance(classType, data, 10, true) as TSeries;
        Assert.Equal(4, MA_Series.Count);
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void handling_NaN(Type classType)
    {
        TSeries data = new("Name") { 1, 2, 3, 4, 5, 6, double.NaN, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        var MA_Series = Activator.CreateInstance(classType, data, 10, true) as TSeries;
        Assert.False(double.IsNaN(MA_Series.Last.v));
    }

    public static IEnumerable<object[]> MASeriesData()
    {
        foreach (var type in maSeriesTypes)
        {
            yield return new object[] { type };
        }
    }
}
#nullable restore