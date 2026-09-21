using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VOSS: Ehlers Voss Predictive Filter
/// A predictive bandpass filter using negative group delay via weighted feedback.
/// Stage 1: Two-pole bandpass filter extracts the dominant cycle.
/// Stage 2: Voss predictor applies negative group delay for anticipatory coupling.
/// Both outputs oscillate around zero.
/// </summary>
/// <remarks>
/// The algorithm is based on:
/// John Ehlers, "A Peek Into the Future," TASC August 2019.
/// Based on Henning U. Voss universal negative delay filter.
///
/// Key properties:
///   - BPF stage isolates cycles near the specified period
///   - Voss predictor reduces group delay (leads the bandpass)
///   - Both Filt and Voss oscillate around zero
///   - Crossings between Filt and Voss generate trade signals
///
/// Complexity: O(Order) per bar, where Order = 3 * Predict
/// </remarks>
[SkipLocalsInit]
public sealed class Voss : AbstractBase
{
    private readonly double _f1, _s1;
    private readonly int _order;
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Src1;       // src[1] — previous bar value for delay chain
        public double Src2;       // src[2] — value from two bars ago
        public double Filt1;      // Filt[1] for BPF recursion
        public double Filt2;      // Filt[2] for BPF recursion
        public double LastFilt;   // Current Filt value (exposed as property)
        public double LastValid;  // Last finite input
        public int Count;         // Bar count for warmup suppression
    }

    private State _s;
    private State _ps;

    // Ring buffer for Voss history (Order+1 elements)
    private readonly double[] _vossRing;
    private int _vossIdx;
    private double[]? _p_vossRing;
    private int _p_vossIdx;

    /// <summary>Primary cycle period in bars.</summary>
    public int Period { get; }

    /// <summary>Prediction bars (negative delay amount).</summary>
    public int Predict { get; }

    /// <summary>Bandpass tolerance as fraction of period.</summary>
    public double Bandwidth { get; }

    /// <summary>The Order of the Voss predictor (3 * Predict).</summary>
    public int Order => _order;

    /// <summary>Last computed Bandpass Filter value (Filt output).</summary>
    public double LastFilt => _s.LastFilt;

    public bool IsNew => _isNew;
    public override bool IsHot => _s.Count > 5;

    public Voss(int period = 20, int predict = 3, double bandwidth = 0.25)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 2");
        }

        if (predict < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(predict), "Predict must be >= 1");
        }

        if (bandwidth <= 0 || bandwidth >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth), "Bandwidth must be in (0, 1)");
        }

        Period = period;
        Predict = predict;
        Bandwidth = bandwidth;
        _order = 3 * predict;
        Name = $"VOSS({period},{predict},{bandwidth:F2})";
        WarmupPeriod = period;

        // Precompute BPF coefficients
        // F1 = cos(2π / Period)
        // G1 = cos(Bandwidth * 2π / Period)
        // S1 = 1/G1 - sqrt(1/G1² - 1)
        double twoPiOverPeriod = 2.0 * Math.PI / period;
        _f1 = Math.Cos(twoPiOverPeriod);
        double g1 = Math.Cos(bandwidth * twoPiOverPeriod);
        _s1 = (1.0 / g1) - Math.Sqrt((1.0 / (g1 * g1)) - 1.0);

        _vossRing = new double[_order + 1];
        _s.LastValid = double.NaN;
    }

    public Voss(ITValuePublisher source, int period = 20, int predict = 3, double bandwidth = 0.25)
        : this(period, predict, bandwidth)
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

        Batch(values, results, Period, Predict, Bandwidth);

        TSeries output = [];
        for (int i = 0; i < values.Length; i++)
        {
            output.Add(source[i].Time, results[i]);
        }

        // Resync internal state by replaying
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
            _ps = _s;
            _p_vossRing ??= new double[_vossRing.Length];
            Array.Copy(_vossRing, _p_vossRing, _vossRing.Length);
            _p_vossIdx = _vossIdx;
        }
        else
        {
            _s = _ps;
            if (_p_vossRing != null)
            {
                Array.Copy(_p_vossRing, _vossRing, _vossRing.Length);
            }
            _vossIdx = _p_vossIdx;
        }

        var s = _s;

        // Handle bad data — last-valid substitution
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }
        else
        {
            s.LastValid = val;
        }

        // Stage 1: Two-pole Bandpass Filter
        // Filt = 0.5*(1-S1)*(src - src[2]) + F1*(1+S1)*Filt[1] - S1*Filt[2]
        double diff = val - s.Src2;
        double filt = Math.FusedMultiplyAdd(
            0.5 * (1.0 - _s1), diff,
            Math.FusedMultiplyAdd(_f1 * (1.0 + _s1), s.Filt1, -_s1 * s.Filt2));

        if (s.Count <= 5)
        {
            filt = 0.0;
        }

        // Stage 2: Voss Predictor
        // SumC = sum of ((count+1)/Order) * Voss[Order - count] for count=0..Order-1
        double sumC = 0.0;
        int ringLen = _vossRing.Length;
        for (int count = 0; count < _order; count++)
        {
            int idx = _order - count; // lookback distance
            int ringPos = (_vossIdx - idx + (ringLen * 2)) % ringLen;
            sumC += (double)(count + 1) / _order * _vossRing[ringPos];
        }

        double vossVal = ((double)(3 + _order) / 2.0 * filt) - sumC;

        // State shifts for next bar
        if (isNew)
        {
            // Shift delay chain: src[2] = previous src[1], src[1] = current val
            s.Src2 = s.Src1;
            s.Src1 = val;

            // Advance Filt history
            s.Filt2 = s.Filt1;
            s.Filt1 = filt;

            // Write to current position FIRST, then advance for next bar
            // skipcq: CS-R1140 - write-then-advance keeps ring index aligned with bar index
            _vossRing[_vossIdx] = vossVal;
            _vossIdx = (_vossIdx + 1) % ringLen;

            s.Count++;
        }
        else
        {
            // Bar correction: overwrite current bar's position
            // _vossIdx was restored to pre-advance state, so it points to current bar
            _vossRing[_vossIdx] = vossVal;
        }

        s.LastFilt = filt;
        _s = s;

        Last = new TValue(input.Time, vossVal);
        PubEvent(Last, isNew);
        return Last;
    }

    public static TSeries Batch(TSeries source, int period = 20, int predict = 3, double bandwidth = 0.25)
    {
        var indicator = new Voss(period, predict, bandwidth);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int period = 20, int predict = 3, double bandwidth = 0.25)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of the same length.", nameof(output));
        }

        // Precompute BPF coefficients
        double twoPiOverPeriod = 2.0 * Math.PI / period;
        double f1 = Math.Cos(twoPiOverPeriod);
        double g1 = Math.Cos(bandwidth * twoPiOverPeriod);
        double s1 = (1.0 / g1) - Math.Sqrt((1.0 / (g1 * g1)) - 1.0);

        int order = 3 * predict;
        double[] vossHistory = new double[source.Length];

        double filt1 = 0, filt2 = 0;
        double lastValid = 0;

        if (source.Length > 0)
        {
            lastValid = source[0];
            if (!double.IsFinite(lastValid))
            {
                lastValid = 0;
            }
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

            // BPF: (src - src[2])
            double src2 = i >= 2 ? source[i - 2] : val;
            if (!double.IsFinite(src2))
            {
                src2 = lastValid;
            }

            double diff = val - src2;
            double filt = Math.FusedMultiplyAdd(
                0.5 * (1.0 - s1), diff,
                Math.FusedMultiplyAdd(f1 * (1.0 + s1), filt1, -s1 * filt2));

            if (i <= 5)
            {
                filt = 0.0;
            }

            // Voss predictor
            double sumC = 0.0;
            for (int count = 0; count < order; count++)
            {
                int idx = order - count;
                int histIdx = i - idx;
                if (histIdx >= 0)
                {
                    sumC += (double)(count + 1) / order * vossHistory[histIdx];
                }
            }

            double vossVal = ((double)(3 + order) / 2.0 * filt) - sumC;
            vossHistory[i] = vossVal;
            output[i] = vossVal;

            filt2 = filt1;
            filt1 = filt;
        }
    }

    public override void Reset()
    {
        _s = default;
        _s.LastValid = double.NaN;
        _ps = default;
        Array.Clear(_vossRing);
        _vossIdx = 0;
        _p_vossRing = null;
        _p_vossIdx = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, Voss Indicator) Calculate(TSeries source,
        int period = 20, int predict = 3, double bandwidth = 0.25)
    {
        var indicator = new Voss(period, predict, bandwidth);
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
