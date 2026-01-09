using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class BpfValidationTests
{
    [Fact]
    public void Validate_BandpassBehavior_Synthetic()
    {
        // Define periods
        // HP Cutoff (High Freq / Short Period) = 40. Passes P < 40.
        // LP Cutoff (Low Freq / Long Period) = 10. Passes P > 10.
        // Bandpass: [10, 40].
        
        int T = 1000;
        double[] sine5 = new double[T];   // Period 5 (Too fast, should be blocked by LP(10)). LP(10) passes P > 10.
        double[] sine15 = new double[T];  // Period 15 (Inside band, 10 < P < 40). Should pass.
        double[] sine100 = new double[T]; // Period 100 (Too slow, should be blocked by HP(40)). HP(40) passes P < 40.
        
        for (int i = 0; i < T; i++) {
            sine5[i] = Math.Sin(2 * Math.PI * i / 5.0);
            sine15[i] = Math.Sin(2 * Math.PI * i / 15.0);
            sine100[i] = Math.Sin(2 * Math.PI * i / 100.0);
        }

        double[] out5 = new double[T];
        double[] out15 = new double[T];
        double[] out100 = new double[T];

        // Instantiate BPF with LowerPeriod=40 (HP cutoff), UpperPeriod=10 (LP cutoff)
        Bpf.Calculate(sine5, out5, 40, 10);
        Bpf.Calculate(sine15, out15, 40, 10);
        Bpf.Calculate(sine100, out100, 40, 10);

        // Analysis of results (last 100 samples to avoid warmup)
        double amp5 = GetAmplitude(out5);
        double amp15 = GetAmplitude(out15);
        double amp100 = GetAmplitude(out100);

        // Expectation:
        Assert.True(amp15 > 0.5, $"Signal in band (P=15) should pass. Amplitude: {amp15}");
        Assert.True(amp5 < 0.35, $"Signal above band freq (P=5) should be attenuated. Amplitude: {amp5}");
        Assert.True(amp100 < 0.35, $"Signal below band freq (P=100) should be attenuated. Amplitude: {amp100}");
    }

    private static double GetAmplitude(double[] signal)
    {
        double max = 0;
        for (int i = signal.Length - 100; i < signal.Length; i++)
        {
            max = Math.Max(max, Math.Abs(signal[i]));
        }
        return max;
    }
}
