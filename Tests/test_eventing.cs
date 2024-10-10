using Xunit;
using System.Security.Cryptography;

#pragma warning disable S1944, S2053, S2222, S2259, S2583, S2589, S3329, S3655, S3900, S3949, S3966, S4158, S4347, S5773, S6781

namespace QuanTAlib;

public class EventingTests
{
    [Fact]
    public void VerifyEventBasedCalculations()
    {
        // Create a cryptographically secure random number generator
        using var rng = RandomNumberGenerator.Create();

        // Create an input series to hold our random values
        var input = new TSeries();
        int p = 10;

        // Create a list of indicator pairs (direct calculation and event-based) with names
        var indicators = new List<(string Name, AbstractBase Direct, AbstractBase EventBased)>
        {
            ("Afirma", new Afirma(p,p,Afirma.WindowType.BlackmanHarris), new Afirma(input, p,p,Afirma.WindowType.BlackmanHarris)),
            ("Alma", new Alma(p), new Alma(input, p)),
            ("Convolution", new Convolution(new double[] {1,2,3,2,1}), new Convolution(input, new double[] {1,2,3,2,1})),
            ("Dema", new Dema(p), new Dema(input, p)),
            ("Dsma", new Dsma(p), new Dsma(input, p)),
            ("Dwma", new Dwma(p), new Dwma(input, p)),
            ("Ema", new Ema(p), new Ema(input, p)),
            ("Epma", new Epma(p), new Epma(input, p)),
            ("Pwma", new Pwma(p), new Pwma(input, p)),
            ("Frama", new Frama(p), new Frama(input, p)),
            ("Fwma", new Fwma(p), new Fwma(input, p)),
            ("Gma", new Gma(p), new Gma(input, p)),
            ("Hma", new Hma(p), new Hma(input, p)),
            ("Htit", new Htit(), new Htit(input)),
            ("Hwma", new Hwma(p), new Hwma(input, p)),
            ("Jma", new Jma(p), new Jma(input, p)),
            ("Kama", new Kama(p), new Kama(input, p)),
            ("Ltma", new Ltma(gamma: 0.2), new Ltma(input, gamma: 0.2)),
            ("Maaf", new Maaf(p), new Maaf(input, p)),
            ("Mama", new Mama(p), new Mama(input, p)),
            ("Mgdi", new Mgdi(p, kFactor: 0.6), new Mgdi(input, p, kFactor: 0.6)),
            ("Mma", new Mma(p), new Mma(input, p)),
            ("Qema", new Qema(k1: 0.2, k2: 0.2, k3: 0.2, k4: 0.2), new Qema(input, k1: 0.2, k2: 0.2, k3: 0.2, k4: 0.2)),
            ("Rema", new Rema(p), new Rema(input, p)),
            ("Rma", new Rma(p), new Rma(input, p)),
            ("Sma", new Sma(p), new Sma(input, p)),
            ("Wma", new Wma(p), new Wma(input, p)),
            ("Rma", new Rma(p), new Rma(input, p)),
            ("Tema", new Tema(p), new Tema(input, p)),
            ("Kama", new Kama(2, 30, 6), new Kama(input, 2, 30, 6)),
            ("Zlema", new Zlema(p), new Zlema(input, p))
        };

        // Generate 200 random values and feed them to both direct and event-based indicators
        for (int i = 0; i < 200; i++)
        {
            double randomValue = GetRandomDouble(rng) * 100;
            input.Add(randomValue);

            // Calculate direct indicators
            foreach (var (_, direct, _) in indicators)
            {
                direct.Calc(randomValue);
            }
        }

        // Compare the results of direct and event-based calculations
        for (int i = 0; i < indicators.Count; i++)
        {
            var (name, direct, eventBased) = indicators[i];
            bool areEqual = (double.IsNaN(direct.Value) && double.IsNaN(eventBased.Value)) ||
                            Math.Abs(direct.Value - eventBased.Value) < 1e-9;
            Assert.True(areEqual, $"Indicator {name} failed: Expected {direct.Value}, Actual {eventBased.Value}");
        }
    }

    private static double GetRandomDouble(RandomNumberGenerator rng)
    {
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue;
    }
}
