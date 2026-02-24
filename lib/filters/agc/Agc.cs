using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AGC: Automatic Gain Control (Ehlers)
/// Amplitude normalization via exponential peak tracking. Normalizes any oscillating
/// input signal to the [-1, +1] range by dividing by a decaying running peak.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/numerics/agc.md
///
/// Key properties:
///   - Pure normalizer: does NOT contain an internal filter stage
///   - Input should oscillate around zero (use after a bandpass/roofing/SSF filter)
///   - Peak decays exponentially each bar (decay=0.991 ≈ 110-bar half-life)
///   - Peak ratchets up instantly when |input| exceeds decayed peak
///   - Output bounded to [-1, +1] for well-behaved oscillating inputs
///
/// Complexity: O(1) — one multiply, one compare, one divide per bar
/// </remarks>
[SkipLocalsInit]
public sealed class Agc : AbstractBase
{
    private readonly double _decay;
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Peak;
        public double LastValid;
        public int Count;
    }

    private State _state;
    private State _p_state;

    /// <summary>
    /// Peak decay factor per bar. Controls how quickly the normalizer adapts
    /// to decreasing amplitude. 0.991 ≈ 110-bar half-life.
    /// </summary>
    public double Decay => _decay;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.Count > 0;

    public Agc(double decay = 0.991)
    {
        if (decay is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(decay), "Decay must be between 0 and 1 exclusive.");
        }

        _decay = decay;
        Name = $"AGC({decay:F3})";
        WarmupPeriod = 1;
        _state.Peak = 1e-10; // tiny positive to avoid div-by-zero on first bar
        _state.LastValid = 0.0;
    }

    public Agc(ITValuePublisher source, double decay = 0.991) : this(decay)
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

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        double[] values = source.Values.ToArray();
        double[] results = new double[values.Length];

        Batch(values, results, _decay);

        TSeries output = [];
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Sync internal state by replaying
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
        _isNew = isNew;
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        var s = _state;

        // Handle bad data
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = val;
        }

        // Exponential peak decay
        s.Peak *= _decay;

        // Ratchet up when signal exceeds decayed peak
        double absVal = Math.Abs(val);
        if (absVal > s.Peak)
        {
            s.Peak = absVal;
        }

        // Normalize: output = val / peak
        double result = s.Peak > 0.0 ? val / s.Peak : 0.0;

        if (isNew)
        {
            s.Count++;
        }

        _state = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public static TSeries Batch(TSeries source, double decay = 0.991)
    {
        var indicator = new Agc(decay);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double decay = 0.991)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        double peak = 1e-10;
        double lastValid = 0.0;

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

            // Decay peak
            peak *= decay;

            // Ratchet
            double absVal = Math.Abs(val);
            if (absVal > peak)
            {
                peak = absVal;
            }

            // Normalize
            output[i] = peak > 0.0 ? val / peak : 0.0;
        }
    }

    public override void Reset()
    {
        _state = default;
        _state.Peak = 1e-10;
        _state.LastValid = 0.0;
        _p_state = default;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, Agc Indicator) Calculate(TSeries source, double decay = 0.991)
    {
        var indicator = new Agc(decay);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
            _publisher = null;
            _handler = null;
        }
        base.Dispose(disposing);
    }
}
