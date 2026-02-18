using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CFITZ: Christiano-Fitzgerald Band-Pass Filter
/// An asymmetric full-sample band-pass filter that is optimal under a random-walk
/// assumption. Unlike the symmetric Baxter-King filter, CF uses ALL available data
/// and produces output for every bar — no data loss at endpoints.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/cfitz.md
///
/// Key properties:
///   - Ideal band-pass weights: B_0 = (ωh-ωl)/π, B_j = (sin(jωh)-sin(jωl))/(πj)
///   - where ωl = 2π/pHigh, ωh = 2π/pLow
///   - Endpoint corrections force weights to sum to zero (DC rejection)
///   - Asymmetric: weights vary by position in the sample
///   - Full-sample: uses all accumulated history (no fixed truncation K)
///   - Oscillates around zero — extracts cyclical component only
///   - Separate window indicator (not overlay)
///   - O(T) per bar for streaming, O(T²) total for batch
///
/// Reference: Christiano &amp; Fitzgerald (2003), "The Band Pass Filter,"
/// International Economic Review, 44(2), 435-465.
///
/// Complexity: O(T) per bar (streaming), O(N) total (batch with precomputed weights)
/// </remarks>
[SkipLocalsInit]
public sealed class Cfitz : AbstractBase
{
    private readonly int _pLow;
    private readonly int _pHigh;
    private readonly double _b0;     // central ideal weight
    private readonly double _wl;     // low cutoff angular frequency
    private readonly double _wh;     // high cutoff angular frequency

    // skipcq: CS-R1073 - List<double> is the SoA storage pattern mandated by protocol
    private readonly List<double> _history;
    private ITValuePublisher? _publisher;
    private TValuePublishedHandler? _handler;
    private bool _isNew;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double LastValid;
        public int Count;
    }

    private State _state;
    private State _p_state;

    /// <summary>Minimum period of the passband (bars).</summary>
    public int PLow => _pLow;

    /// <summary>Maximum period of the passband (bars).</summary>
    public int PHigh => _pHigh;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.Count >= 2;

    public Cfitz(int pLow = 6, int pHigh = 32)
    {
        if (pLow < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(pLow), "pLow must be >= 2.");
        }

        if (pHigh <= pLow)
        {
            throw new ArgumentOutOfRangeException(nameof(pHigh), "pHigh must be > pLow.");
        }

        _pLow = pLow;
        _pHigh = pHigh;

        _wl = 2.0 * Math.PI / pHigh;
        _wh = 2.0 * Math.PI / pLow;
        _b0 = (_wh - _wl) / Math.PI;

        Name = $"Cfitz({pLow},{pHigh})";
        WarmupPeriod = 2;

        // skipcq: CS-R1073 - List<double> is the SoA storage pattern mandated by protocol
        _history = new List<double>(256);
        _state.LastValid = double.NaN;
    }

    public Cfitz(ITValuePublisher source, int pLow = 6, int pHigh = 32)
        : this(pLow, pHigh)
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

        Batch(values, results, _pLow, _pHigh);

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
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        var s = _state;

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

        // History management
        if (isNew)
        {
            _history.Add(val);
        }
        else
        {
            if (_history.Count > 0)
            {
                _history[^1] = val;
            }
            else
            {
                _history.Add(val);
            }
        }

        double result;
        int T = _history.Count;

        if (T < 2)
        {
            // Need at least 2 bars for the filter
            result = 0.0;
        }
        else
        {
            // CF formula for t = T (last bar in sample):
            // c_T = 0.5*B_0*y_T + Σ(j=1..T-2) B_j*y_{T-j} + b̃*y_1
            // b̃ = -0.5*B_0 - Σ(j=1..T-2) B_j
            result = ComputeCfForLastBar();
        }

        if (isNew)
        {
            s.Count++;
        }

        _state = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeCfForLastBar()
    {
        int T = _history.Count;

        if (T == 2)
        {
            // c_T = 0.5*B_0*(y_T - y_1)
            return 0.5 * _b0 * (_history[1] - _history[0]);
        }

        // General: c_T = 0.5*B_0*y_T + Σ(j=1..T-2) B_j*y_{T-j} + b̃*y_1
        double weightedSum = 0.5 * _b0 * _history[T - 1];
        double sumBj = 0.0;

        for (int j = 1; j <= T - 2; j++)
        {
            double bj = (Math.Sin(j * _wh) - Math.Sin(j * _wl)) / (Math.PI * j);
            weightedSum += bj * _history[T - 1 - j];
            sumBj += bj;
        }

        // Endpoint correction: b̃ = -0.5*B_0 - Σ B_j
        double btilde = -0.5 * _b0 - sumBj;
        weightedSum += btilde * _history[0];

        return weightedSum;
    }

    /// <summary>
    /// Computes the ideal band-pass weight B_j for lag j.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double IdealWeight(double wl, double wh, int j)
    {
        if (j == 0)
        {
            return (wh - wl) / Math.PI;
        }
        return (Math.Sin(j * wh) - Math.Sin(j * wl)) / (Math.PI * j);
    }

    public static TSeries Batch(TSeries source, int pLow = 6, int pHigh = 32)
    {
        double[] input = source.Values.ToArray();
        double[] output = new double[input.Length];
        Batch(input, output, pLow, pHigh);

        TSeries result = [];
        for (int i = 0; i < input.Length; i++)
        {
            result.Add(source[i].Time, output[i]);
        }
        return result;
    }

    /// <summary>
    /// Full-sample CF band-pass filter. For each bar t (1-indexed), computes:
    ///   c_t = 0.5*B_0*y_t + Σ(j=1..T-t-1) B_j*y_{t+j} + b̃_fwd*y_T
    ///         + Σ(j=1..t-2) B_j*y_{t-j} + b̃_bwd*y_1
    /// This is the TRUE full-sample asymmetric CF filter (not the streaming approximation).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int pLow = 6, int pHigh = 32)
    {
        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output span must be at least as long as source.", nameof(output));
        }

        int T = source.Length;
        if (T == 0)
        {
            return;
        }

        double wl = 2.0 * Math.PI / pHigh;
        double wh = 2.0 * Math.PI / pLow;
        double b0 = (wh - wl) / Math.PI;

        // Precompute ideal weights B_j for j = 0..T-2
        int maxJ = T - 1;
        double[] bWeights = new double[maxJ + 1];

        bWeights[0] = b0;
        for (int j = 1; j <= maxJ; j++)
        {
            bWeights[j] = (Math.Sin(j * wh) - Math.Sin(j * wl)) / (Math.PI * j);
        }

        if (T == 1)
        {
            output[0] = 0.0;
            return;
        }

        // Full-sample CF filter: for each bar t (0-indexed: t goes 0..T-1)
        // Using 1-indexed math from the paper, t_paper = t + 1
        for (int t = 0; t < T; t++)
        {
            int tp = t + 1;  // 1-indexed position

            if (tp == 1)
            {
                // c_1 = 0.5*B_0*y_1 + Σ(j=1..T-2) B_j*y_{j+1} + b̃_{T-1}*y_T
                double ws = 0.5 * b0 * source[0];
                double sBj = 0.0;
                for (int j = 1; j <= T - 2; j++)
                {
                    ws += bWeights[j] * source[j];  // y_{j+1} in 0-index is source[j]
                    sBj += bWeights[j];
                }
                double bt = -0.5 * b0 - sBj;
                ws += bt * source[T - 1];
                output[t] = ws;
            }
            else if (tp == T)
            {
                // c_T = 0.5*B_0*y_T + Σ(j=1..T-2) B_j*y_{T-j} + b̃_{T-1}*y_1
                double ws = 0.5 * b0 * source[T - 1];
                double sBj = 0.0;
                for (int j = 1; j <= T - 2; j++)
                {
                    ws += bWeights[j] * source[T - 1 - j];
                    sBj += bWeights[j];
                }
                double bt = -0.5 * b0 - sBj;
                ws += bt * source[0];
                output[t] = ws;
            }
            else
            {
                // Interior bar: c_t = B_0*y_t
                //   + Σ(j=1..T-t-1) B_j*y_{t+j}   [forward]
                //   + b̃_fwd*y_T                     [far endpoint]
                //   + Σ(j=1..t-2) B_j*y_{t-j}       [backward]
                //   + b̃_bwd*y_1                     [near endpoint]
                double ws = b0 * source[t];

                // Forward terms: j=1..T-tp = T-t-1 (0-indexed)
                double sumFwd = 0.0;
                int fwdMax = T - tp - 1; // = T - t - 2 in 0-indexed terms
                for (int j = 1; j <= fwdMax; j++)
                {
                    ws += bWeights[j] * source[t + j];
                    sumFwd += bWeights[j];
                }
                // Far endpoint correction
                double btFwd = -0.5 * b0 - sumFwd;
                ws += btFwd * source[T - 1];

                // Backward terms: j=1..tp-2 = t-1 in 0-indexed
                double sumBwd = 0.0;
                int bwdMax = tp - 2; // = t in 0-indexed
                for (int j = 1; j <= bwdMax; j++)
                {
                    ws += bWeights[j] * source[t - j];
                    sumBwd += bWeights[j];
                }
                // Near endpoint correction
                double btBwd = -0.5 * b0 - sumBwd;
                ws += btBwd * source[0];

                output[t] = ws;
            }
        }
    }

    public override void Reset()
    {
        _state = default;
        _state.LastValid = double.NaN;
        _p_state = default;
        _history.Clear();
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, Cfitz Indicator) Calculate(TSeries source,
        int pLow = 6, int pHigh = 32)
    {
        var indicator = new Cfitz(pLow, pHigh);
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
