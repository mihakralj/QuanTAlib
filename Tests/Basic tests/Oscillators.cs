using Xunit;
using System;
using System.Runtime.InteropServices;
using QuanTAlib;

namespace Basics;
#nullable disable
public class Oscillators
{
    private static Type[] maSeriesTypes = new[]
    {
    typeof(BIAS_Series),
    typeof(MAX_Series),
    typeof(MIN_Series),
    typeof(MIDPOINT_Series),
    typeof(ZL_Series),
    typeof(DECAY_Series),
    typeof(ENTROPY_Series),
    typeof(KURTOSIS_Series),
    typeof(MAD_Series),
    typeof(MAPE_Series),
    typeof(MAE_Series),
        typeof(MSE_Series),
    typeof(SDEV_Series),
    typeof(SMAPE_Series),
    typeof(WMAPE_Series),
    typeof(SSDEV_Series),
    typeof(VAR_Series),
    typeof(SVAR_Series),
    typeof(MEDIAN_Series),
    typeof(ZSCORE_Series),
    typeof(CMO_Series),
    typeof(RSI_Series),
    typeof(TRIX_Series),
    typeof(BBANDS_Series),
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
        data.Add(1);
        Assert.False(double.IsNaN(MA_Series.Last.v));
    }

    [Theory]
    [MemberData(nameof(MASeriesData))]
    public void Period_one(Type classType)
    {
        GBM_Feed feed = new(100);
        TSeries data = feed.OHLC4;

        var MA_Series = Activator.CreateInstance(classType, data, 1, false) as TSeries;
        Assert.False(double.IsNaN(MA_Series[^1].v));
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