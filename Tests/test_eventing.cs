using Xunit;
using System.Security.Cryptography;

#pragma warning disable S1944, S2053, S2222, S2259, S2583, S2589, S3329, S3655, S3900, S3949, S3966, S4158, S4347, S5773, S6781

namespace QuanTAlib;

public class EventingTests
{
    [Fact]
    public void EventBasedCalculations()
    {
        // Create a cryptographically secure random number generator
        using var rng = RandomNumberGenerator.Create();

        // Create input series to hold our random values
        var input = new TSeries();
        var barInput = new TBarSeries();
        int p = 10;

        // Create a list of value-based indicator pairs
        var valueIndicators = new List<(string Name, AbstractBase Direct, AbstractBase EventBased)>
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
            ("Zlema", new Zlema(p), new Zlema(input, p)),
            ("Sinema", new Sinema(p), new Sinema(input, p)),
            ("Smma", new Smma(p), new Smma(input, p)),
            ("T3", new T3(p), new T3(input, p)),
            ("Trima", new Trima(p), new Trima(input, p)),
            ("Vidya", new Vidya(p), new Vidya(input, p)),
            ("Apo", new Apo(12, 26), new Apo(input, 12, 26)),
            ("Macd", new Macd(12, 26, 9), new Macd(input, 12, 26, 9)),
            ("Rsi", new Rsi(p), new Rsi(input, p)),
            ("Rsx", new Rsx(p), new Rsx(input, p)),
            ("Cmo", new Cmo(p), new Cmo(input, p)),
            ("Cog", new Cog(p), new Cog(input, p)),
            ("Curvature", new Curvature(p), new Curvature(input, p)),
            ("Entropy", new Entropy(p), new Entropy(input, p)),
            ("Kurtosis", new Kurtosis(p), new Kurtosis(input, p)),
            ("Max", new Max(p), new Max(input, p)),
            ("Median", new Median(p), new Median(input, p)),
            ("Min", new Min(p), new Min(input, p)),
            ("Mode", new Mode(p), new Mode(input, p)),
            ("Percentile", new Percentile(p, 0.5), new Percentile(input, p, 0.5)),
            ("Skew", new Skew(p), new Skew(input, p)),
            ("Slope", new Slope(p), new Slope(input, p)),
            ("Stddev", new Stddev(p), new Stddev(input, p)),
            ("Variance", new Variance(p), new Variance(input, p)),
            ("Zscore", new Zscore(p), new Zscore(input, p)),
            // Volatility indicators (value-based)
            ("Hv", new Hv(p), new Hv(input, p)),
            ("Jvolty", new Jvolty(p), new Jvolty(input, p)),
            ("Rv", new Rv(p), new Rv(input, p)),
            ("Rvi", new Rvi(p), new Rvi(input, p)),
            // Error classes
            ("Mae", new Mae(p), new Mae(input, p)),
            ("Mapd", new Mapd(p), new Mapd(input, p)),
            ("Mape", new Mape(p), new Mape(input, p)),
            ("Mase", new Mase(p), new Mase(input, p)),
            ("Mda", new Mda(p), new Mda(input, p)),
            ("Me", new Me(p), new Me(input, p)),
            ("Mpe", new Mpe(p), new Mpe(input, p)),
            ("Mse", new Mse(p), new Mse(input, p)),
            ("Msle", new Msle(p), new Msle(input, p)),
            ("Rae", new Rae(p), new Rae(input, p)),
            ("Rmse", new Rmse(p), new Rmse(input, p)),
            ("Rmsle", new Rmsle(p), new Rmsle(input, p)),
            ("Rse", new Rse(p), new Rse(input, p)),
            ("Smape", new Smape(p), new Smape(input, p)),
            ("Rsquared", new Rsquared(p), new Rsquared(input, p)),
            ("Huber", new Huber(p), new Huber(input, p))
        };

        // Create a list of bar-based indicator pairs
        var barIndicators = new List<(string Name, AbstractBase Direct, AbstractBase EventBased)>
        {
            // Volume indicators
            ("Adl", new Adl(), new Adl(barInput)),
            ("Adosc", new Adosc(3, 10), new Adosc(barInput, 3, 10)),
            ("Aobv", new Aobv(), new Aobv(barInput)),
            ("Cmf", new Cmf(20), new Cmf(barInput, 20)),
            ("Eom", new Eom(14), new Eom(barInput, 14)),
            ("Kvo", new Kvo(34, 55), new Kvo(barInput, 34, 55)),
            // Volatility indicators (bar-based)
            ("Atr", new Atr(14), new Atr(barInput, 14)),
            // Oscillators (bar-based)
            ("Chop", new Chop(14), new Chop(barInput, 14)),
            ("Dosc", new Dosc(), new Dosc(barInput))
        };

        // Generate 200 random values and feed them to indicators
        for (int i = 0; i < 200; i++)
        {
            // Generate random value for value-based indicators
            double randomValue = GetRandomDouble(rng) * 100;
            input.Add(randomValue);

            // Calculate value-based indicators
            foreach (var (_, direct, _) in valueIndicators)
            {
                direct.Calc(randomValue);
            }

            // Generate random bar for bar-based indicators
            var bar = new TBar(
                DateTime.Now,
                randomValue,
                randomValue + Math.Abs(GetRandomDouble(rng) * 10),
                randomValue - Math.Abs(GetRandomDouble(rng) * 10),
                randomValue + (GetRandomDouble(rng) * 5),
                Math.Abs(GetRandomDouble(rng) * 1000),
                true
            );
            barInput.Add(bar);

            // Calculate bar-based indicators
            foreach (var (_, direct, _) in barIndicators)
            {
                direct.Calc(bar);
            }
        }

        // Compare the results for value-based indicators
        foreach (var (name, direct, eventBased) in valueIndicators)
        {
            bool areEqual = (double.IsNaN(direct.Value) && double.IsNaN(eventBased.Value)) ||
                            Math.Abs(direct.Value - eventBased.Value) < 1e-9;
            Assert.True(areEqual, $"Value indicator {name} failed: Expected {direct.Value}, Actual {eventBased.Value}");
        }

        // Compare the results for bar-based indicators
        foreach (var (name, direct, eventBased) in barIndicators)
        {
            bool areEqual = (double.IsNaN(direct.Value) && double.IsNaN(eventBased.Value)) ||
                            Math.Abs(direct.Value - eventBased.Value) < 1e-9;
            Assert.True(areEqual, $"Bar indicator {name} failed: Expected {direct.Value}, Actual {eventBased.Value}");
        }
    }

    private static double GetRandomDouble(RandomNumberGenerator rng)
    {
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue;
    }
}
