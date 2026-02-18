namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the Christiano-Fitzgerald band-pass filter.
/// Since CF is not implemented in any external TA library (TA-Lib, Skender, Tulip, Ooples),
/// validation relies on self-consistency checks and known mathematical properties.
/// </summary>
public class CfitzValidationTests
{
    [Fact]
    public void Validate_BatchStreamingEquivalence()
    {
        // Streaming is a "last-bar" approximation — it computes the CF formula
        // treating accumulated history as the full sample, so each streaming bar
        // only sees data up to that bar. The batch ALSO computes a full-sample
        // filter. For the LAST bar, streaming should match batch exactly.
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Streaming
        var streamInd = new Cfitz(6, 32);
        foreach (var item in series)
        {
            streamInd.Update(item);
        }

        // Batch
        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];
        Cfitz.Batch(input, output, 6, 32);

        // Last bar should match: streaming sees full history at last bar
        Assert.Equal(output[^1], streamInd.Last.Value, 1e-9);
    }

    [Fact]
    public void Validate_DCRejection()
    {
        // Band-pass must reject DC: constant input → zero output
        double[] input = Enumerable.Repeat(50.0, 200).ToArray();
        double[] output = new double[200];
        Cfitz.Batch(input, output, 6, 32);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i]) < 1e-10,
                $"DC rejection failed at [{i}]: {output[i]}");
        }
    }

    [Fact]
    public void Validate_LinearTrendRejection()
    {
        // CF under random-walk assumption should remove linear trends
        // since endpoint corrections force zero-sum weights
        double[] input = new double[200];
        for (int i = 0; i < 200; i++)
        {
            input[i] = 100.0 + 0.5 * i;  // linear trend
        }
        double[] output = new double[200];
        Cfitz.Batch(input, output, 6, 32);

        // Interior bars should be near zero (linear trend = DC + slope)
        // Allow some tolerance at endpoints
        double maxInterior = 0;
        for (int i = 10; i < 190; i++)
        {
            maxInterior = Math.Max(maxInterior, Math.Abs(output[i]));
        }
        Assert.True(maxInterior < 0.5,
            $"Linear trend should be mostly rejected, max interior value: {maxInterior}");
    }

    [Fact]
    public void Validate_InBandPassthrough()
    {
        // A sine wave with period inside the passband should pass through
        // with significant amplitude
        int n = 300;
        double period = 16.0; // inside [6, 32]
        double[] input = new double[n];
        for (int i = 0; i < n; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / period);
        }
        double[] output = new double[n];
        Cfitz.Batch(input, output, 6, 32);

        // Check amplitude in the middle section (avoid endpoints)
        double amp = GetAmplitude(output[100..200]);
        Assert.True(amp > 0.3, $"In-band signal (period={period}) should pass through, amplitude={amp}");
    }

    [Fact]
    public void Validate_OutOfBandRejection_HighFreq()
    {
        // A high-frequency signal (period < pLow) should be rejected
        int n = 300;
        double period = 3.0; // outside [6, 32] — too fast
        double[] input = new double[n];
        for (int i = 0; i < n; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / period);
        }
        double[] output = new double[n];
        Cfitz.Batch(input, output, 6, 32);

        double amp = GetAmplitude(output[100..200]);
        Assert.True(amp < 0.3, $"High-freq signal (period={period}) should be rejected, amplitude={amp}");
    }

    [Fact]
    public void Validate_OutOfBandRejection_LowFreq()
    {
        // A low-frequency signal (period > pHigh) should be mostly rejected
        int n = 500;
        double period = 100.0; // outside [6, 32] — too slow
        double[] input = new double[n];
        for (int i = 0; i < n; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / period);
        }
        double[] output = new double[n];
        Cfitz.Batch(input, output, 6, 32);

        double inAmp = GetAmplitude(input[150..350]);
        double outAmp = GetAmplitude(output[150..350]);
        double ratio = outAmp / inAmp;
        Assert.True(ratio < 0.5, $"Low-freq signal (period={period}) should be attenuated, ratio={ratio}");
    }

    [Fact]
    public void Validate_NearZeroMeanOutput()
    {
        // Over a long enough sample, the CF output should have near-zero mean
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);
        var data = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];
        Cfitz.Batch(input, output, 6, 32);

        double mean = output.Average();
        Assert.True(Math.Abs(mean) < 1.0,
            $"CF output mean should be near zero, got {mean}");
    }

    [Fact]
    public void Validate_Determinism()
    {
        // Same input → same output, every time
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var ind1 = new Cfitz(6, 32);
        var ind2 = new Cfitz(6, 32);

        foreach (var item in series)
        {
            ind1.Update(item);
            ind2.Update(item);
        }

        Assert.Equal(ind1.Last.Value, ind2.Last.Value, 15);
    }

    private static double GetAmplitude(double[] data)
    {
        double max = double.MinValue, min = double.MaxValue;
        foreach (double v in data)
        {
            if (v > max) { max = v; }
            if (v < min) { min = v; }
        }
        return (max - min) / 2.0;
    }
}
