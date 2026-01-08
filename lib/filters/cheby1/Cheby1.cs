using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CHEBY1: Chebyshev Type I Lowpass Filter
/// A 2nd order lowpass filter that allows for ripple in the passband but has a steeper rolloff than Butterworth.
/// </summary>
/// <remarks>
/// Complexity: O(1)
/// Computation: 5 multiplications, 4 additions per cycle
/// </remarks>
[SkipLocalsInit]
public sealed class Cheby1 : AbstractBase
{
    private readonly double _b0, _b1, _b2, _a1, _a2;

    // State buffer: [src1, src2, filt1, filt2]
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Src1, Src2;
        public double Filt1, Filt2;
        public double LastValid;
    }

    private State _state;
    private State _p_state; // Previous state for rollback

    /// <summary>
    /// Cutoff period (related to cutoff frequency).
    /// </summary>
    public int Period { get; }

    /// <summary>
    /// Passband ripple in decibels (dB).
    /// </summary>
    public double Ripple { get; }

    public override bool IsHot => Math.Abs(_state.Filt2) > double.Epsilon; // Sufficiently warm when we have history

    public Cheby1(int period, double ripple = 1.0)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");
        if (ripple <= 0)
            throw new ArgumentOutOfRangeException(nameof(ripple), "Ripple must be > 0");

        Period = period;
        Ripple = ripple;
        Name = $"Cheby1({period},{ripple:F1})";
        WarmupPeriod = period;

        // Precompute coefficients
        double safeRipple = Math.Max(ripple, 0.01);
        double wc = 2.0 * Math.PI / period;
        double Wc = Math.Tan(wc * 0.5);
        double epsilon = Math.Sqrt(Math.Pow(10.0, safeRipple * 0.1) - 1.0);
        double mu = System.Math.Asinh(1.0 / epsilon) * 0.5;
        double sinhMu = Math.Sinh(mu);
        double coshMu = Math.Cosh(mu);
        double sigma = -sinhMu * Wc;
        double omegaD = coshMu * Wc;
        double K = sigma * sigma + omegaD * omegaD;

        double a0z = 1.0 - 2.0 * sigma + K;
        double a1z = 2.0 * K - 2.0;
        double a2z = 1.0 + 2.0 * sigma + K;
        double b0z = K;
        double b1z = 2.0 * K;
        double b2z = K;

        // Normalize
        _b0 = b0z / a0z;
        _b1 = b1z / a0z;
        _b2 = b2z / a0z;
        _a1 = a1z / a0z;
        _a2 = a2z / a0z;
    }

    public Cheby1(ITValuePublisher source, int period, double ripple = 1.0) : this(period, ripple)
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

        Calculate(values, results, Period, Ripple);

        TSeries output = new TSeries();
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Update internal state to match the end of the batch
        // We re-run the last few updates to sync the state.
        // For IIR filters, this is an approximation unless we run the whole series again.
        // Given O(1) complexity, we can actually just re-run the whole series to be perfectly accurate with state,
        // or we can just Reset and re-run.
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

        if (isNew)
        {
            _state.Src2 = _state.Src1;
            _state.Src1 = val;
            _state.Filt2 = _state.Filt1;
            _state.Filt1 = filt;
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
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, double ripple = 1.0)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));

        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");
        if (ripple <= 0)
            throw new ArgumentOutOfRangeException(nameof(ripple), "Ripple must be > 0");

        // Coefficients
        double safeRipple = Math.Max(ripple, 0.01);
        double wc = 2.0 * Math.PI / period;
        double Wc = Math.Tan(wc * 0.5);
        double epsilon = Math.Sqrt(Math.Pow(10.0, safeRipple * 0.1) - 1.0);
        double mu = System.Math.Asinh(1.0 / epsilon) * 0.5;
        double sinhMu = Math.Sinh(mu);
        double coshMu = Math.Cosh(mu);
        double sigma = -sinhMu * Wc;
        double omegaD = coshMu * Wc;
        double K = sigma * sigma + omegaD * omegaD;

        double a0z = 1.0 - 2.0 * sigma + K;
        double a1z = 2.0 * K - 2.0;
        double a2z = 1.0 + 2.0 * sigma + K;
        double b0z = K;
        double b1z = 2.0 * K;
        double b2z = K;

        double b0 = b0z / a0z;
        double b1 = b1z / a0z;
        double b2 = b2z / a0z;
        double a1 = a1z / a0z;
        double a2 = a2z / a0z;

        // State variables
        double src1 = 0, src2 = 0;
        double filt1 = 0, filt2 = 0;
        double lastValid = 0;

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

            output[i] = filt;

            src2 = src1;
            src1 = val;
            filt2 = filt1;
            filt1 = filt;
        }
    }
}