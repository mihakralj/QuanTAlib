using Xunit;
using System.Security.Cryptography;
using System.Diagnostics;

namespace QuanTAlib;

public class test_iTValue
{
    private readonly RandomNumberGenerator rng;
    private const int RandomInputs = 50;
    private const double ReferenceInput = 100.0;

    public test_iTValue()
    {
        rng = RandomNumberGenerator.Create();
    }

    private double GetRandomDouble(double minValue, double maxValue)
    {
        byte[] randomBytes = new byte[8];
        rng.GetBytes(randomBytes);
        return minValue + (BitConverter.ToDouble(randomBytes, 0) % (maxValue - minValue));
    }

    [Theory]
    [MemberData(nameof(GetAllIndicators))]
    public void TestIndicatorStateControl(string name, AbstractBase indicator)
    {
        // Feed the reference input with IsNew=true
        var initialInput = new TValue(DateTime.Now, ReferenceInput, true);
        indicator.Calc(initialInput);
        double initialOutput = indicator.Value;

        // Feed 50 random numbers with IsNew=false
        for (int i = 0; i < RandomInputs; i++)
        {
            var randomInput = new TValue(DateTime.Now, GetRandomDouble(-100, 100), false);
            indicator.Calc(randomInput);
        }

        // Feed the reference input again with IsNew=false
        var finalInput = new TValue(DateTime.Now, ReferenceInput, false);
        indicator.Calc(finalInput);
        double finalOutput = indicator.Value;

        // Compare the initial and final outputs
        try
        {
            Assert.Equal(initialOutput, finalOutput, 1e-6);
        }
        catch (Exception)
        {
            Debug.WriteLine($"Assertion failed for {name}:");
            Debug.WriteLine($"  Initial output: {initialOutput}");
            Debug.WriteLine($"  Final output: {finalOutput}");
            Debug.WriteLine($"  Difference: {Math.Abs(initialOutput - finalOutput)}");
            throw;
        }
    }

    public static IEnumerable<object[]> GetAllIndicators()
    {
        var indicators = new List<(string Name, AbstractBase Indicator)>
        {
            ("Ema", new Ema(period: 10, useSma: true)),
            ("Alma", new Alma(period: 14, offset: 0.85, sigma: 6)),
            ("Afirma", new Afirma(periods: 4, taps: 4, window: Afirma.WindowType.Blackman)),
            ("Convolution", new Convolution(new[] { 1.0, 2, 3, 2, 1 })),
            ("Dema", new Dema(period: 14)),
            ("Dsma", new Dsma(period: 14)),
            ("Dwma", new Dwma(period: 14)),
            ("Epma", new Epma(period: 14)),
            ("Frama", new Frama(period: 14)),
            ("Fwma", new Fwma(period: 14)),
            ("Gma", new Gma(period: 14)),
            ("Hma", new Hma(period: 14)),
            ("Hwma", new Hwma(period: 14)),
            ("Kama", new Kama(period: 14)),
            ("Ltma", new Ltma(gamma: 0.1)),
            ("Mama", new Mama(fastLimit: 0.5, slowLimit: 0.05)),
            ("Mgdi", new Mgdi(period: 14)),
            ("Mma", new Mma(period: 14)),
            ("Qema", new Qema()),
            ("Rema", new Rema(period: 14)),
            ("Rma", new Rma(period: 14)),
            ("Sinema", new Sinema(period: 14)),
            ("Sma", new Sma(period: 14)),
            ("Smma", new Smma(period: 14)),
            ("T3", new T3(period: 14)),
            ("Tema", new Tema(period: 14)),
            ("Trima", new Trima(period: 14)),
            ("Vidya", new Vidya(shortPeriod: 14, longPeriod: 30, alpha: 0.2)),
            ("Wma", new Wma(period: 14)),
            ("Zlema", new Zlema(period: 14)),
            ("Curvature", new Curvature(period: 14)),
            ("Entropy", new Entropy(period: 14)),
            ("Kurtosis", new Kurtosis(period: 14)),
            ("Max", new Max(period: 14, decay: 0.01)),
            ("Median", new Median(period: 14)),
            ("Min", new Min(period: 14, decay: 0.01)),
            ("Mode", new Mode(period: 14)),
            ("Percentile", new Percentile(period: 14, percent: 50)),
            ("Skew", new Skew(period: 14)),
            ("Slope", new Slope(period: 14)),
            ("Stddev", new Stddev(period: 14)),
            ("Variance", new Variance(period: 14)),
            ("Zscore", new Zscore(period: 14)),
            ("Historical", new Historical(period: 14)),
            ("Realized", new Realized(period: 14))
        };

        return indicators.Select(i => new object[] { i.Name, i.Indicator });
    }
}
