using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BAXTERKING: Baxter-King Band-Pass Filter
/// A symmetric FIR filter that approximates the ideal band-pass by truncating
/// the infinite impulse response at lag K and normalizing weights to sum to zero.
/// Extracts cyclical components with periodicities between pLow and pHigh bars.
/// </summary>
/// <remarks>
/// The algorithm is based on a Pine Script implementation:
/// https://github.com/mihakralj/pinescript/blob/main/indicators/filters/baxterking.md
///
/// Key properties:
///   - Ideal band-pass weights: B_0 = (b-a)/π, B_j = (sin(jb)-sin(ja))/(πj)
///   - where a = 2π/pHigh, b = 2π/pLow
///   - BK normalization: subtract mean so weights sum to zero (DC rejection)
///   - Symmetric filter → zero phase shift, output delayed by K bars
///   - Oscillates around zero — extracts cyclical component only
///   - Separate window indicator (not overlay)
///   - O(K) per bar — single weighted sum over 2K+1 lagged values
///
/// Complexity: O(K) per bar
/// </remarks>
[SkipLocalsInit]
public sealed class BaxterKing : AbstractBase
{
    private readonly int _pLow;
    private readonly int _pHigh;
    private readonly int _k;
    private readonly int _filterLen;     // 2K + 1
    private readonly double[] _weights;  // precomputed normalized weights [0..2K]
    private readonly RingBuffer _buffer;
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

    /// <summary>Filter half-length (number of leads/lags).</summary>
    public int K => _k;

    public bool IsNew => _isNew;
    public override bool IsHot => _state.Count >= _filterLen;

    public BaxterKing(int pLow = 6, int pHigh = 32, int k = 12)
    {
        if (pLow < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(pLow), "pLow must be >= 2.");
        }

        if (pHigh <= pLow)
        {
            throw new ArgumentOutOfRangeException(nameof(pHigh), "pHigh must be > pLow.");
        }

        if (k < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "K must be >= 1.");
        }

        _pLow = pLow;
        _pHigh = pHigh;
        _k = k;
        _filterLen = 2 * k + 1;

        Name = $"BaxterKing({pLow},{pHigh},{k})";
        WarmupPeriod = _filterLen;

        // Precompute BK weights
        _weights = new double[_filterLen];
        ComputeWeights(_weights, pLow, pHigh, k);

        _buffer = new RingBuffer(_filterLen);
        _state.LastValid = double.NaN;
    }

    public BaxterKing(ITValuePublisher source, int pLow = 6, int pHigh = 32, int k = 12)
        : this(pLow, pHigh, k)
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

        Batch(values, results, _pLow, _pHigh, _k);

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

        // Input buffer: Add for new bars, UpdateNewest for corrections
        if (isNew)
        {
            _buffer.Add(val);
        }
        else
        {
            _buffer.UpdateNewest(val);
        }

        double result;

        if (_buffer.Count < _filterLen)
        {
            // During warmup, output 0 (band-pass oscillates around zero)
            result = 0.0;
        }
        else
        {
            result = ComputeFilter();
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
    private double ComputeFilter()
    {
        // Apply symmetric FIR: dot product of weights with buffer (oldest to newest)
        // The "center" is buffer[K], which represents time t-K (delayed output)
        _buffer.GetSequencedSpans(out var first, out var second);
        ReadOnlySpan<double> w = _weights;
        double sum = w.Slice(0, first.Length).DotProduct(first);
        if (!second.IsEmpty)
        {
            sum += w.Slice(first.Length).DotProduct(second);
        }
        return sum;
    }

    /// <summary>
    /// Precomputes the BK band-pass filter weights.
    /// Ideal weights are truncated at K and normalized so they sum to zero.
    /// </summary>
    private static void ComputeWeights(double[] weights, int pLow, int pHigh, int k)
    {
        double a = 2.0 * Math.PI / pHigh;  // low cutoff angular frequency
        double b = 2.0 * Math.PI / pLow;   // high cutoff angular frequency
        int filterLen = 2 * k + 1;

        // Compute ideal band-pass weights B[j] for j = 0..K
        // B_0 = (b - a) / pi
        // B_j = (sin(j*b) - sin(j*a)) / (pi*j) for j >= 1
        double[] ideal = new double[k + 1];
        ideal[0] = (b - a) / Math.PI;

        double idealSum = ideal[0];
        for (int j = 1; j <= k; j++)
        {
            ideal[j] = (Math.Sin(j * b) - Math.Sin(j * a)) / (Math.PI * j);
            idealSum += 2.0 * ideal[j]; // symmetric: each appears twice
        }

        // Normalization constant: ensure weights sum to zero
        double theta = idealSum / filterLen;

        // Fill the symmetric weight array
        // Index mapping: weights[K-j] = weights[K+j] = ideal[j] - theta
        // weights[K] = ideal[0] - theta (center)
        weights[k] = ideal[0] - theta;
        for (int j = 1; j <= k; j++)
        {
            double w = ideal[j] - theta;
            weights[k - j] = w;
            weights[k + j] = w;
        }
    }

    public static TSeries Batch(TSeries source, int pLow = 6, int pHigh = 32, int k = 12)
    {
        double[] input = source.Values.ToArray();
        double[] output = new double[input.Length];
        Batch(input, output, pLow, pHigh, k);

        TSeries result = [];
        for (int i = 0; i < input.Length; i++)
        {
            result.Add(source[i].Time, output[i]);
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int pLow = 6, int pHigh = 32, int k = 12)
    {
        int filterLen = 2 * k + 1;
        double[] weights = new double[filterLen];
        ComputeWeights(weights, pLow, pHigh, k);

        int n = source.Length;
        ReadOnlySpan<double> w = weights;

        for (int i = 0; i < n; i++)
        {
            if (i < filterLen - 1)
            {
                output[i] = 0.0;  // warmup: output zero
            }
            else
            {
                output[i] = w.DotProduct(source.Slice(i - filterLen + 1, filterLen));
            }
        }
    }

    public override void Reset()
    {
        _state = default;
        _state.LastValid = double.NaN;
        _p_state = default;
        _buffer.Clear();
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double val in source)
        {
            Update(new TValue(DateTime.UtcNow, val), isNew: true);
        }
    }

    public static (TSeries Results, BaxterKing Indicator) Calculate(TSeries source,
        int pLow = 6, int pHigh = 32, int k = 12)
    {
        var indicator = new BaxterKing(pLow, pHigh, k);
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
