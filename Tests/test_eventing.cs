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

        // Create a list of indicator pairs (direct calculation and event-based)
        var indicators = new List<(AbstractBase Direct, AbstractBase EventBased)>
        {
            (new Afirma(p,p,Afirma.WindowType.BlackmanHarris), new Afirma(input, p,p,Afirma.WindowType.BlackmanHarris)),
            (new Alma(p), new Alma(input, p)),
            (new Convolution([1,2,3,2,1]), new Convolution(input, [1,2,3,2,1])),
            (new Dema(p), new Dema(input, p)),
            (new Dsma(p), new Dsma(input, p)),
            (new Dwma(p), new Dwma(input, p)),
            (new Ema(p), new Ema(input, p)),
            (new Epma(p), new Epma(input, p)),
            (new Frama(p), new Frama(input, p)),
            (new Fwma(p), new Fwma(input, p)),
            (new Gma(p), new Gma(input, p)),
            (new Hma(p), new Hma(input, p)),
            (new Htit(), new Htit(input)),
            (new Hwma(p), new Hwma(input, p)),
            (new Jma(p), new Jma(input, p)),
            (new Kama(p), new Kama(input, p)),
            (new Ltma(gamma: 0.2), new Ltma(input, gamma: 0.2)),
            (new Maaf(p), new Maaf(input, p)),
            (new Mama(p), new Mama(input, p)),
            (new Mgdi(p), new Mgdi(input, p)),
            (new Mma(p), new Mma(input, p)),
            (new Qema(k1: 0.2, k2: 0.2, k3: 0.2, k4: 0.2), new Qema(input, k1: 0.2, k2: 0.2, k3: 0.2, k4: 0.2)),
            (new Rema(p), new Rema(input, p)),
            (new Rma(p), new Rma(input, p)),
            (new Sma(p), new Sma(input, p)),
            (new Wma(p), new Wma(input, p)),
            (new Rma(p), new Rma(input, p)),
            (new Tema(p), new Tema(input, p)),
            (new Kama(2, 30, 6), new Kama(input, 2, 30, 6)),
            (new Zlema(p), new Zlema(input, p))
        };

        // Generate 200 random values and feed them to both direct and event-based indicators
        for (int i = 0; i< 200; i++)
        {
            double randomValue = GetRandomDouble(rng) * 100;
    input.Add(randomValue);

            // Calculate direct indicators
            foreach (var (direct, _) in indicators)
            {
                direct.Calc(randomValue);
            }
}

// Compare the results of direct and event-based calculations
foreach (var (direct, eventBased) in indicators)
{
    Assert.Equal(direct.Value, eventBased.Value, 9);
}
    }

private static double GetRandomDouble(RandomNumberGenerator rng)
{
    byte[] bytes = new byte[8];
    rng.GetBytes(bytes);
    return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue;
}
}
