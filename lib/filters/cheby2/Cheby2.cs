using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CHEBY2: Chebyshev Type II Lowpass Filter
/// A 2nd order lowpass filter (Inverse Chebyshev) that is maximally flat in the passband and has equiripple in the stopband.
/// </summary>
/// <remarks>
/// Complexity: O(1)
/// Computation: 5 multiplications, 4 additions per cycle
/// </remarks>
[SkipLocalsInit]
public sealed class Cheby2 : AbstractBase
{
    private readonly double _b0, _b1, _b2, _a1, _a2;

    // State buffer: [src1, src2, filt1, filt2]
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Src1, Src2;
        public double Filt1, Filt2;
        public double LastValid;
        public int Count;
    }

    private State _state;
    private State _p_state; // Previous state for rollback

    /// <summary>
    /// Cutoff period (related to cutoff frequency).
    /// </summary>
    public int Period { get; }

    /// <summary>
    /// Stopband attenuation in decibels (dB).
    /// </summary>
    public double Attenuation { get; }

    public override bool IsHot => Math.Abs(_state.Filt2) > double.Epsilon; // Sufficiently warm when we have history

    public Cheby2(int period, double attenuation = 5.0)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");
        if (attenuation <= 0)
            throw new ArgumentOutOfRangeException(nameof(attenuation), "Attenuation must be > 0");

        Period = period;
        Attenuation = attenuation;
        Name = $"Cheby2({period},{attenuation:F1})";
        WarmupPeriod = period;

        // Precompute coefficients
        double safeAtten = Math.Max(attenuation, 0.1);
        double wc = 2.0 * Math.PI / period;
        double Wc = 2.0 * Math.Tan(wc * 0.5);
        double epsilon = 1.0 / Math.Sqrt(Math.Pow(10.0, safeAtten * 0.1) - 1.0);
        double mu = System.Math.Asinh(1.0 / epsilon) * 0.5;
        double sinhMu = Math.Sinh(mu);
        double coshMu = Math.Cosh(mu);
        double sqrt2 = Math.Sqrt(2.0);
        double sigmaP = -Wc * sinhMu / sqrt2;
        double omegaP = Wc * coshMu / sqrt2;
        double omegaZ = Wc / Math.Cos(Math.PI * 0.25); // Cos(pi/4) = 1/sqrt(2), so this is Wc * sqrt(2)
        
        double Kp = sigmaP * sigmaP + omegaP * omegaP;
        double Kz = omegaZ * omegaZ;
        double dcGain = Kz / Kp;

        double a0z = 1.0 - 2.0 * sigmaP + Kp;
        double a1z = 2.0 * Kp - 2.0;
        double a2z = 1.0 + 2.0 * sigmaP + Kp;
        
        double b0z = dcGain * (1.0 + Kz);
        double b1z = dcGain * (2.0 * Kz - 2.0);
        double b2z = dcGain * (1.0 + Kz);

        // Normalize
        double b0 = b0z / a0z;
        double b1 = b1z / a0z;
        double b2 = b2z / a0z;
        double a1 = a1z / a0z;
        double a2 = a2z / a0z;

        // Apply Gain Correction (matching Pine Script)
        // Ensure strictly unity gain at DC
        double sumB = b0 + b1 + b2;
        double sumA = 1.0 + a1 + a2;
        double norm = sumA / sumB;
        
        _b0 = b0 * norm;
        _b1 = b1 * norm;
        _b2 = b2 * norm;
        _a1 = a1; // A coeff don't change relative to output scale (except output is scaled by norm, so feedback is same)
        _a2 = a2;
    }

    public Cheby2(ITValuePublisher source, int period, double attenuation = 5.0) : this(period, attenuation)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        double[] values = source.Values.ToArray();
        double[] results = new double[values.Length];

        Calculate(values, results, Period, Attenuation);

        TSeries output = new TSeries();
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Update internal state to match the end of the batch
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i]);
        }

        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        // Handle bad data
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = _state.LastValid;
        }
        else
        {
            _state.LastValid = val;
        }

        // Apply filter:
        // filt = B0 * src + B1 * src1 + B2 * src2 - A1 * filt1 - A2 * filt2
        
        // Use FMA for precision and performance
        double term1 = Math.FusedMultiplyAdd(_b1, _state.Src1, _b0 * val);
        double term2 = Math.FusedMultiplyAdd(_b2, _state.Src2, term1);
        double term3 = Math.FusedMultiplyAdd(-_a1, _state.Filt1, term2);
        double filt = Math.FusedMultiplyAdd(-_a2, _state.Filt2, term3);

        // Handle warmup for first 2 samples
        if (_state.Count < 2)
        {
            filt = val;
        }

        if (isNew)
        {
            _state.Src2 = _state.Src1;
            _state.Src1 = val;
            _state.Filt2 = _state.Filt1;
            _state.Filt1 = filt;

            if (_state.Count < 2)
                _state.Count++;
        }

        Last = new TValue(input.Time, filt);
        PubEvent(Last, isNew);
        return Last;
    }

    public override void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, double attenuation = 5.0)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));

        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");
        if (attenuation <= 0)
            throw new ArgumentOutOfRangeException(nameof(attenuation), "Attenuation must be > 0");

        // Coefficients
        double safeAtten = Math.Max(attenuation, 0.1);
        double wc = 2.0 * Math.PI / period;
        double Wc = 2.0 * Math.Tan(wc * 0.5);
        double epsilon = 1.0 / Math.Sqrt(Math.Pow(10.0, safeAtten * 0.1) - 1.0);
        double mu = System.Math.Asinh(1.0 / epsilon) * 0.5;
        double sinhMu = Math.Sinh(mu);
        double coshMu = Math.Cosh(mu);
        double sqrt2 = Math.Sqrt(2.0);
        double sigmaP = -Wc * sinhMu / sqrt2;
        double omegaP = Wc * coshMu / sqrt2;
        double omegaZ = Wc / Math.Cos(Math.PI * 0.25);
        
        double Kp = sigmaP * sigmaP + omegaP * omegaP;
        double Kz = omegaZ * omegaZ;
        double dcGain = Kz / Kp;

        double a0z = 1.0 - 2.0 * sigmaP + Kp;
        double a1z = 2.0 * Kp - 2.0;
        double a2z = 1.0 + 2.0 * sigmaP + Kp;
        
        double b0z = dcGain * (1.0 + Kz);
        double b1z = dcGain * (2.0 * Kz - 2.0);
        double b2z = dcGain * (1.0 + Kz);

        double b0 = b0z / a0z;
        double b1 = b1z / a0z;
        double b2 = b2z / a0z;
        double a1 = a1z / a0z;
        double a2 = a2z / a0z;

        // Apply Gain Correction
        double sumB = b0 + b1 + b2;
        double sumA = 1.0 + a1 + a2;
        double norm = sumA / sumB;
        
        b0 *= norm;
        b1 *= norm;
        b2 *= norm;

        // State variables
        double src1 = 0, src2 = 0;
        double filt1 = 0, filt2 = 0;
        double lastValid = 0;
        int count = 0;

        // Handle first value initialization
        if (source.Length > 0)
        {
            lastValid = source[0];
            if (!double.IsFinite(lastValid)) lastValid = 0;
        }

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = lastValid;
            }
            else
            {
                lastValid = val;
            }

            double term1 = Math.FusedMultiplyAdd(b1, src1, b0 * val);
            double term2 = Math.FusedMultiplyAdd(b2, src2, term1);
            double term3 = Math.FusedMultiplyAdd(-a1, filt1, term2);
            double filt = Math.FusedMultiplyAdd(-a2, filt2, term3);

            if (count < 2)
            {
                filt = val;
                count++;
            }

            output[i] = filt;

            src2 = src1;
            src1 = val;
            filt2 = filt1;
            filt1 = filt;
        }
    }
}
