using Xunit;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace QuanTAlib;

public class IndicatorTests
{
    private readonly RandomNumberGenerator rng;
    private const int SeriesLen = 1000;
    private const int Corrections = 100;

    public IndicatorTests()
    {
        rng = RandomNumberGenerator.Create();
    }

    private int GetRandomNumber(int minValue, int maxValue)
    {
        byte[] randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        int randomInt = BitConverter.ToInt32(randomBytes, 0);
        return Math.Abs(randomInt % (maxValue - minValue)) + minValue;
    }

    // skipcq: CS-R1055
    private static readonly ITValue[] indicators =
    {
        new Ema(period: 10, useSma: true),
        new Alma(period: 14, offset: 0.85, sigma: 6),
        new Afirma(periods: 4, taps: 4, window: Afirma.WindowType.Blackman),
        new Convolution(new[] { 1.0, 2, 3, 2, 1 }),
        new Dema(period: 14),
        new Dsma(period: 14),
        new Dwma(period: 14),
        new Epma(period: 14),
        new Frama(period: 14),
        new Fwma(period: 14),
        new Gma(period: 14),
        new Hma(period: 14),
        new Hwma(period: 14),
        new Kama(period: 14),
        new Mama(fastLimit: 0.5, slowLimit: 0.05),
        new Mgdi(period: 14),
        new Mma(period: 14),
        new Qema(),
        new Rema(period: 14),
        new Rma(period: 14),
        new Sinema(period: 14),
        new Sma(period: 14),
        new Smma(period: 14),
        new T3(period: 14),
        new Tema(period: 14),
        new Trima(period: 14),
        new Vidya(shortPeriod: 14, longPeriod: 30, alpha: 0.2),
        new Wma(period: 14),
        new Zlema(period: 14),

        new Curvature(period: 14),
        new Entropy(period: 14),
        new Kurtosis(period: 14),
        new Max(period: 14, decay: 0.01),
        new Median(period: 14),
        new Min(period: 14, decay: 0.01),
        new Median(period: 14),
        new Mode(period: 14),
        new Percentile(period: 14, percent: 50),
        new Skew(period: 14),
        new Slope(period: 14),
        new Stddev(period: 14),
        new Variance(period: 14),
        new Zscore(period: 14),

        new Historical(period: 14),
        new Realized(period: 14)
    };

    [Theory]
    [MemberData(nameof(GetIndicators))]
    public void IndicatorIsNew(ITValue indicator)
    {
        var indicator1 = indicator;
        var indicator2 = indicator;

        MethodInfo calcMethod = FindCalcMethod(indicator.GetType());
        if (calcMethod == null)
        {
            throw new InvalidOperationException($"Calc method not found for indicator type: {indicator.GetType().Name}");
        }

        for (int i = 0; i < SeriesLen; i++)
        {
            TValue item1 = new(Time: DateTime.Now, Value: GetRandomNumber(-100, 100), IsNew: true);
            InvokeCalc(indicator1, calcMethod, item1);

            for (int j = 0; j < Corrections; j++)
            {
                item1 = new(Time: DateTime.Now, Value: GetRandomNumber(-100, 100), IsNew: false);
                InvokeCalc(indicator1, calcMethod, item1);
            }

            var item2 = new TValue(item1.Time, item1.Value, IsNew: true);
            InvokeCalc(indicator2, calcMethod, item2);

            Assert.Equal(indicator1.Value, indicator2.Value);
        }
    }

    private static MethodInfo FindCalcMethod(Type type)
    {
        while (type != null && type != typeof(object))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                              .Where(m => m.Name == "Calc")
                              .ToList();

            if (methods.Count > 0)
            {
                // Prefer the method with TValue parameter
                var method = methods.FirstOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(TValue);
                });

                // If not found, return the first method
                return method ?? methods.First();
            }

            type = type.BaseType!;
        }
        return null!;
    }

    private static void InvokeCalc(ITValue indicator, MethodInfo calcMethod, TValue input)
    {
        var parameters = calcMethod.GetParameters();
        if (parameters.Length == 1)
        {
            calcMethod.Invoke(indicator, new object[] { input });
        }
        else if (parameters.Length == 2)
        {
            calcMethod.Invoke(indicator, new object[] { input, double.NaN });
        }
        else
        {
            throw new InvalidOperationException($"Invalid number of parameters for Calc method in indicator type: {indicator.GetType().Name}");
        }
    }

    public static IEnumerable<object[]> GetIndicators()
    {
        return indicators.Select(indicator => new object[] { indicator });
    }
}
