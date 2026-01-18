using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ELLIPTIC: 2nd Order Elliptic Lowpass Filter
/// A 2nd order lowpass filter with 1dB passband ripple and 40dB stopband attenuation.
/// Elliptic filters offer the steepest transition bandwidth for a given order.
/// </summary>
/// <remarks>
/// Complexity: O(1)
/// Computation: 5 multiplications, 4 additions per cycle
/// </remarks>
[SkipLocalsInit]
public sealed class Elliptic : AbstractBase
{
    private readonly double _b0, _b1, _b2, _a1, _a2;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private const double C_wz = 2.15499;
    private const double C_sigma = -0.31323;
    private const double C_Kp_norm = 0.91598;
    private const double C_k = 0.14735;

    // State buffer: [src1, src2, filt1, filt2]
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Src1, Src2;
        public double Filt1, Filt2;
        public double LastValid;
        public bool IsInitialized;
    }

    private State _state;
    private State _p_state; // Previous state for rollback

    /// <summary>
    /// Cutoff period (related to cutoff frequency).
    /// </summary>
    public int Period { get; }

    public override bool IsHot => Math.Abs(_state.Filt2) > double.Epsilon; // Sufficiently warm when we have history

    public Elliptic(int period)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");

        Period = period;
        Name = $"Elliptic({period})";
        WarmupPeriod = period;

        // Precompute coefficients based on hardcoded Rp=1dB, Rs=40dB
        double Wc = Math.Tan(Math.PI / period);
        if (Wc < 1e-9) Wc = 1e-9;

        double omega_z_scaled = C_wz * Wc;
        double sigma_scaled = C_sigma * Wc;
        double Kp_scaled = C_Kp_norm * Wc * Wc;

        double a0_denom = 1.0 - 2.0 * sigma_scaled + Kp_scaled;
        if (Math.Abs(a0_denom) < 1e-9) a0_denom = 1e-9;

        const double norm_factor = C_Kp_norm / (C_k * C_wz * C_wz);

        double b0_val = norm_factor * C_k * (1.0 + omega_z_scaled * omega_z_scaled) / a0_denom;
        double b1_val = norm_factor * C_k * (2.0 * omega_z_scaled * omega_z_scaled - 2.0) / a0_denom;
        double b2_val = b0_val;

        double a1_val = (2.0 * Kp_scaled - 2.0) / a0_denom;
        double a2_val = (1.0 + 2.0 * sigma_scaled + Kp_scaled) / a0_denom;

        // Validate and Normalize for Unity Gain at DC
        // DC Gain = (b0 + b1 + b2) / (1 + a1 + a2)
        // We use difference equation: y[n] = ... - a1*y[n-1] - a2*y[n-2]
        // Transfer function H(z) denominator is 1 + a1*z^-1 + a2*z^-2
        double sum_b = b0_val + b1_val + b2_val;
        double sum_a = 1.0 + a1_val + a2_val;

        double gain_corr = sum_a / sum_b;

        _b0 = b0_val * gain_corr;
        _b1 = b1_val * gain_corr;
        _b2 = b2_val * gain_corr;
        _a1 = a1_val;
        _a2 = a2_val;
    }

    public Elliptic(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
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
        if (source.Count == 0) return [];

        double[] values = source.Values.ToArray();
        double[] results = new double[values.Length];

        // Calculate and capture ending state
        CalculateWithState(values, results, Period, out var endState);

        TSeries output = [];
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Set internal state from calculated end state (no double-processing)
        _state = endState;
        _p_state = endState;
        Last = new TValue(source[source.Count - 1].Time, results[results.Length - 1]);

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

        if (!_state.IsInitialized)
        {
            _state.IsInitialized = true;
            _state.Src1 = val;
            _state.Src2 = val;
            _state.Filt1 = val;
            _state.Filt2 = val;
        }

        // Apply filter:
        // filt = b0 * s0 + b1 * s1 + b2 * s2 - a1 * f1 - a2 * f2

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
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        CalculateWithState(source, output, period, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateWithState(ReadOnlySpan<double> source, Span<double> output, int period, out State endState)
    {
        endState = default;

        if (source.Length != output.Length)
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));

        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");

        // Precompute coefficients based on hardcoded Rp=1dB, Rs=40dB
        double Wc = Math.Tan(Math.PI / period);
        if (Wc < 1e-9) Wc = 1e-9;

        double omega_z_scaled = C_wz * Wc;
        double sigma_scaled = C_sigma * Wc;
        double Kp_scaled = C_Kp_norm * Wc * Wc;

        double a0_denom = 1.0 - 2.0 * sigma_scaled + Kp_scaled;
        if (Math.Abs(a0_denom) < 1e-9) a0_denom = 1e-9;

        const double norm_factor = C_Kp_norm / (C_k * C_wz * C_wz);

        double b0 = norm_factor * C_k * (1.0 + omega_z_scaled * omega_z_scaled) / a0_denom;
        double b1 = norm_factor * C_k * (2.0 * omega_z_scaled * omega_z_scaled - 2.0) / a0_denom;
        double b2 = b0;

        double a1 = (2.0 * Kp_scaled - 2.0) / a0_denom;
        double a2 = (1.0 + 2.0 * sigma_scaled + Kp_scaled) / a0_denom;

        // Normalize constants for Unity Gain
        double sum_b = b0 + b1 + b2;
        double sum_a = 1.0 + a1 + a2;
        double gain_corr = sum_a / sum_b;
        b0 *= gain_corr;
        b1 *= gain_corr;
        b2 *= gain_corr;

        // State variables
        double src1 = 0, src2 = 0;
        double filt1 = 0, filt2 = 0;
        double lastValid = 0;
        bool initialized = false;

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

            if (!initialized)
            {
                src1 = val;
                src2 = val;
                filt1 = val;
                filt2 = val;
                initialized = true;
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

        // Capture ending state
        endState = new State
        {
            Src1 = src1,
            Src2 = src2,
            Filt1 = filt1,
            Filt2 = filt2,
            LastValid = lastValid,
            IsInitialized = initialized
        };
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}