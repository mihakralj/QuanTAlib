using Xunit;
using System.Security.Cryptography;
using System.Reflection;

namespace QuanTAlib.Tests;

public class EventingTests
{
    private const int TestDataPoints = 200;
    private const int DefaultPeriod = 10;
    private const double Tolerance = 1e-9;

    private static readonly (string Name, object[] DirectParams, object[] EventParams)[] ValueIndicators =
    {
        ("Afirma", new object[] { DefaultPeriod, DefaultPeriod, Afirma.WindowType.BlackmanHarris }, new object[] { new TSeries(), DefaultPeriod, DefaultPeriod, Afirma.WindowType.BlackmanHarris }),
        ("Alma", new object[] { DefaultPeriod, 0.85, 6.0 }, new object[] { new TSeries(), DefaultPeriod, 0.85, 6.0 }),
        ("Beta", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Convolution", new object[] { new double[] {1,2,3,2,1} }, new object[] { new TSeries(), new double[] {1,2,3,2,1} }),
        ("Corr", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Covar", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Curvature", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Dema", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Dsma", new object[] { DefaultPeriod, 0.9 }, new object[] { new TSeries(), DefaultPeriod, 0.9 }),
        ("Dwma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Ema", new object[] { DefaultPeriod, true }, new object[] { new TSeries(), DefaultPeriod, true }),
        ("Entropy", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Epma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Fisher", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Frama", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Fwma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Gma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Granger", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Hma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Htit", Array.Empty<object>(), new object[] { new TSeries() }),
        ("Hwma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Jma", new object[] { DefaultPeriod, 0, 0.45, 10 }, new object[] { new TSeries(), DefaultPeriod, 0, 0.45, 10 }),
        ("Kama", new object[] { DefaultPeriod, 2, 30 }, new object[] { new TSeries(), DefaultPeriod, 2, 30 }),
        ("Kendall", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Kurtosis", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Ltma", new object[] { 0.2 }, new object[] { new TSeries(), 0.2 }),
        ("Maaf", new object[] { 39, 0.002 }, new object[] { new TSeries(), 39, 0.002 }),
        ("Mama", new object[] { 0.5, 0.05 }, new object[] { new TSeries(), 0.5, 0.05 }),
        ("Max", new object[] { DefaultPeriod, 0.0 }, new object[] { new TSeries(), DefaultPeriod, 0.0 }),
        ("Median", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Mgdi", new object[] { DefaultPeriod, 0.6 }, new object[] { new TSeries(), DefaultPeriod, 0.6 }),
        ("Min", new object[] { DefaultPeriod, 0.0 }, new object[] { new TSeries(), DefaultPeriod, 0.0 }),
        ("Mma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Mode", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Percentile", new object[] { DefaultPeriod, 0.5 }, new object[] { new TSeries(), DefaultPeriod, 0.5 }),
        ("Pwma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Qema", new object[] { 0.2, 0.2, 0.2, 0.2 }, new object[] { new TSeries(), 0.2, 0.2, 0.2, 0.2 }),
        ("Rema", new object[] { DefaultPeriod, 0.5 }, new object[] { new TSeries(), DefaultPeriod, 0.5 }),
        ("Rma", new object[] { DefaultPeriod, true }, new object[] { new TSeries(), DefaultPeriod, true }),
        ("Skew", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Slope", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Sma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Smma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Spearman", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Stddev", new object[] { DefaultPeriod, false }, new object[] { new TSeries(), DefaultPeriod, false }),
        ("T3", new object[] { DefaultPeriod, 0.7, true }, new object[] { new TSeries(), DefaultPeriod, 0.7, true }),
        ("Tema", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Trima", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Variance", new object[] { DefaultPeriod, false }, new object[] { new TSeries(), DefaultPeriod, false }),
        ("Vidya", new object[] { DefaultPeriod, 0, 0.2 }, new object[] { new TSeries(), DefaultPeriod, 0, 0.2 }),
        ("Wma", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Zlema", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod }),
        ("Zscore", new object[] { DefaultPeriod }, new object[] { new TSeries(), DefaultPeriod })
    };

    private static readonly (string Name, object[] DirectParams, object[] EventParams)[] BarIndicators =
    {
        ("Adl", Array.Empty<object>(), new object[] { new TBarSeries() }),
        ("Adosc", new object[] { 3, 10 }, new object[] { new TBarSeries(), 3, 10 }),
        ("Aobv", Array.Empty<object>(), new object[] { new TBarSeries() }),
        ("Cmf", new object[] { 20 }, new object[] { new TBarSeries(), 20 }),
        ("Eom", new object[] { 14 }, new object[] { new TBarSeries(), 14 }),
        ("Kvo", new object[] { 34, 55 }, new object[] { new TBarSeries(), 34, 55 }),
        ("Atr", new object[] { 14 }, new object[] { new TBarSeries(), 14 }),
        ("Chop", new object[] { 14 }, new object[] { new TBarSeries(), 14 }),
        ("Dosc", Array.Empty<object>(), new object[] { new TBarSeries() })
    };

    public static IEnumerable<object[]> GetValueIndicatorData()
        => ValueIndicators.Select(x => new object[] { x.Name, x.DirectParams, x.EventParams });

    public static IEnumerable<object[]> GetBarIndicatorData()
        => BarIndicators.Select(x => new object[] { x.Name, x.DirectParams, x.EventParams });

    private static double GetRandomDouble(RandomNumberGenerator rng)
    {
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue;
    }

    private static TBar GenerateRandomBar(RandomNumberGenerator rng, double baseValue)
    {
        return new TBar(
            DateTime.Now,
            baseValue,
            baseValue + Math.Abs(GetRandomDouble(rng) * 10),
            baseValue - Math.Abs(GetRandomDouble(rng) * 10),
            baseValue + (GetRandomDouble(rng) * 5),
            Math.Abs(GetRandomDouble(rng) * 1000),
            true
        );
    }

    [Theory]
    [MemberData(nameof(GetValueIndicatorData))]
    public void ValueIndicatorEventTest(string indicatorName, object[] directParams, object[] eventParams)
    {
        using var rng = RandomNumberGenerator.Create();
        var input = (TSeries)eventParams[0];

        // Create indicator instances using reflection
        var indicatorType = Type.GetType($"QuanTAlib.{indicatorName}, QuanTAlib")!;
        var directIndicator = (AbstractBase)Activator.CreateInstance(indicatorType, directParams)!;
        var eventIndicator = (AbstractBase)Activator.CreateInstance(indicatorType, eventParams)!;

        // Generate test data and calculate
        for (int i = 0; i < TestDataPoints; i++)
        {
            double randomValue = GetRandomDouble(rng) * 100;
            input.Add(randomValue);
            directIndicator.Calc(randomValue);
        }

        bool areEqual = (double.IsNaN(directIndicator.Value) && double.IsNaN(eventIndicator.Value)) ||
                        Math.Abs(directIndicator.Value - eventIndicator.Value) < Tolerance;

        Assert.True(areEqual, $"Value indicator {indicatorName} failed: Expected {directIndicator.Value}, Actual {eventIndicator.Value}");
    }

    [Theory]
    [MemberData(nameof(GetBarIndicatorData))]
    public void BarIndicatorEventTest(string indicatorName, object[] directParams, object[] eventParams)
    {
        using var rng = RandomNumberGenerator.Create();
        var barInput = (TBarSeries)eventParams[0];

        // Create indicator instances using reflection
        var indicatorType = Type.GetType($"QuanTAlib.{indicatorName}, QuanTAlib")!;
        var directIndicator = (AbstractBase)Activator.CreateInstance(indicatorType, directParams)!;
        var eventIndicator = (AbstractBase)Activator.CreateInstance(indicatorType, eventParams)!;

        // Generate test data and calculate
        for (int i = 0; i < TestDataPoints; i++)
        {
            var bar = GenerateRandomBar(rng, GetRandomDouble(rng) * 100);
            barInput.Add(bar);
            directIndicator.Calc(bar);
        }

        bool areEqual = (double.IsNaN(directIndicator.Value) && double.IsNaN(eventIndicator.Value)) ||
                        Math.Abs(directIndicator.Value - eventIndicator.Value) < Tolerance;

        Assert.True(areEqual, $"Bar indicator {indicatorName} failed: Expected {directIndicator.Value}, Actual {eventIndicator.Value}");
    }
}
