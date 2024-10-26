using Xunit;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace QuanTAlib;

/// <summary>
/// Contains unit tests for bar-based indicators in QuanTAlib.
/// </summary>
public class BarIndicatorTests
{
    private readonly RandomNumberGenerator rng;
    private const int SeriesLen = 1000;
    private const int Corrections = 100;

    /// <summary>
    /// Initializes a new instance of the BarIndicatorTests class.
    /// </summary>
    public BarIndicatorTests()
    {
        rng = RandomNumberGenerator.Create();
    }

    private static readonly ITValue[] indicators = new ITValue[]
    {
        new Atr(period: 14),

        // Add other TBar-based indicators here
    };

    /// <summary>
    /// Tests if the indicator produces consistent results when processing new and updated bars.
    /// </summary>
    /// <param name="indicator">The indicator to test.</param>
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
            TBar item1 = GenerateRandomBar(isNew: true);
            InvokeCalc(indicator1, calcMethod, item1);

            for (int j = 0; j < Corrections; j++)
            {
                item1 = GenerateRandomBar(isNew: false);
                InvokeCalc(indicator1, calcMethod, item1);
            }

            var item2 = new TBar(item1.Time, item1.Open, item1.High, item1.Low, item1.Close, item1.Volume, IsNew: true);
            InvokeCalc(indicator2, calcMethod, item2);

            Assert.Equal(indicator1.Value, indicator2.Value);
        }
    }

    /// <summary>
    /// Finds the appropriate Calc method for the given indicator type.
    /// </summary>
    /// <param name="type">The type of the indicator.</param>
    /// <returns>The MethodInfo for the Calc method.</returns>
    private static MethodInfo FindCalcMethod(Type type)
    {
        while (type != null && type != typeof(object))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                              .Where(m => m.Name == "Calc")
                              .ToList();

            if (methods.Count > 0)
            {
                // Prefer the method with TBar parameter
                var method = methods.Find(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(TBar);
                });

                // If not found, return the first method
                return method ?? methods[0];
            }

            type = type.BaseType!;
        }
        return null!;
    }

    /// <summary>
    /// Invokes the Calc method on the given indicator with the provided input.
    /// </summary>
    /// <param name="indicator">The indicator instance.</param>
    /// <param name="calcMethod">The Calc method to invoke.</param>
    /// <param name="input">The input TBar.</param>
    private static void InvokeCalc(ITValue indicator, MethodInfo calcMethod, TBar input)
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

    /// <summary>
    /// Generates a random TBar for testing purposes.
    /// </summary>
    /// <param name="isNew">Indicates whether the generated bar should be marked as new.</param>
    /// <returns>A randomly generated TBar.</returns>
    private TBar GenerateRandomBar(bool isNew)
    {
        double open = GetRandomDouble() * 200 - 100;
        double close = GetRandomDouble() * 200 - 100;
        double high = Math.Max(open, close) + GetRandomDouble() * 10;
        double low = Math.Min(open, close) - GetRandomDouble() * 10;
        long volume = GetRandomNumber(0, 10000);

        return new TBar(Time: DateTime.Now, Open: open, High: high, Low: low, Close: close, Volume: volume, IsNew: isNew);
    }

    /// <summary>
    /// Generates a random double between 0 and 1.
    /// </summary>
    /// <returns>A random double between 0 and 1.</returns>
    private double GetRandomDouble()
    {
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue;
    }

    /// <summary>
    /// Generates a random integer between minValue (inclusive) and maxValue (exclusive).
    /// </summary>
    /// <param name="minValue">The minimum value (inclusive).</param>
    /// <param name="maxValue">The maximum value (exclusive).</param>
    /// <returns>A random integer between minValue and maxValue.</returns>
    private int GetRandomNumber(int minValue, int maxValue)
    {
        byte[] randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        int randomInt = BitConverter.ToInt32(randomBytes, 0);
        return Math.Abs(randomInt % (maxValue - minValue)) + minValue;
    }

    /// <summary>
    /// Provides the list of indicators for parameterized tests.
    /// </summary>
    /// <returns>An enumerable of object arrays, each containing an indicator instance.</returns>
    public static IEnumerable<object[]> GetIndicators()
    {
        return indicators.Select(indicator => new object[] { indicator });
    }
}
