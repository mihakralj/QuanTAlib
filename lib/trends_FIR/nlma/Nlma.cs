using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// NLMA: Non-Lag Moving Average
/// </summary>
/// <remarks>
/// FIR filter using the original Igorad (TrendLaboratory) two-phase kernel.
/// Kernel length = 5*period - 1. Two zones:
///   Phase zone (i=0..period-2): t ramps 0→1, cosine focus with unity gain for t≤0.5
///   Cycle zone (i=period-1..flen-2): t continues 1→~9, cosine oscillation with 1/(3πt+1) decay
/// Weight: w(i) = g(t) × cos(πt), where g = 1 for t≤0.5, else 1/(3πt+1).
/// Last tap (i=flen-1) has weight 0. Signed-sum normalization preserves DC gain = 1.
///
/// Origin: Igorad / TrendLaboratory NonLagMA v7.1.
/// </remarks>
[SkipLocalsInit]
public sealed class Nlma : AbstractBase
{
    private readonly int _period;
    private readonly int _flen;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _pubHandler;
    private bool _isNew = true;
    private bool _disposed;
    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    public bool IsNew => _isNew;
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates NLMA with specified period.
    /// </summary>
    /// <param name="period">Length parameter; kernel spans 5*period-1 bars (must be >= 2)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Nlma(int period = 14)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }

        _period = period;
        _flen = ComputeFilterLength(period);
        Name = $"Nlma({_period})";
        WarmupPeriod = _flen;

        _buffer = new RingBuffer(_flen);
        _weights = new double[_flen];

        _weightSum = ComputeIgoradWeights(_weights, _period, _flen);
    }

    /// <summary>
    /// Creates NLMA connected to a data source for event-based updates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Nlma(ITValuePublisher source, int period = 14) : this(period)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    // ── Filter length ─────────────────────────────────────────────────

    /// <summary>
    /// Computes the Igorad kernel length: Cycle*period + (period-1) = 5*period - 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeFilterLength(int period)
    {
        const int Cycle = 4;
        int phase = period - 1;
        return (Cycle * period) + phase; // = 5*period - 1
    }

    // ── Update overloads (adjacent per S4136) ──────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        return UpdateCore(input, isNew, publish: true);
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying last flen bars
        Reset();
        int startIndex = Math.Max(0, len - _flen);
        for (int i = startIndex; i < len; i++)
        {
            UpdateCore(source[i], isNew: true, publish: false);
        }

        return new TSeries(t, v);
    }

    // ── Internal update logic ──────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(TValue input, bool isNew, bool publish)
    {
        if (isNew)
        {
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = _p_lastValidValue;
        }

        double val = GetValidValue(input.Value);

        if (!double.IsFinite(val))
        {
            Last = new TValue(input.Time, double.NaN);
            if (publish)
            {
                PubEvent(Last, isNew);
            }
            return Last;
        }

        if (isNew)
        {
            _lastValidValue = val;
            _buffer.Add(val);

            int count = _buffer.Count;
            double result;

            if (count < _flen)
            {
                // During warmup, return the input price (no partial kernel)
                result = val;
            }
            else
            {
                result = ConvolveFull();
            }

            Last = new TValue(input.Time, result);
            if (publish)
            {
                PubEvent(Last, isNew);
            }
            return Last;
        }
        else
        {
            // Bar correction: snapshot, compute, restore
            _buffer.Snapshot();
            double prevLast = _lastValidValue;
            double prevPLast = _p_lastValidValue;

            _lastValidValue = val;
            _buffer.UpdateNewest(val);

            int count = _buffer.Count;
            double result;

            if (count < _flen)
            {
                result = val;
            }
            else
            {
                result = ConvolveFull();
            }

            Last = new TValue(input.Time, result);

            // Restore buffer and state
            _buffer.Restore();
            _lastValidValue = prevLast;
            _p_lastValidValue = prevPLast;

            if (publish)
            {
                PubEvent(Last, isNew);
            }
            return Last;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => UpdateCore(e.Value, e.IsNew, publish: true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            return input;
        }
        return double.IsFinite(_lastValidValue) ? _lastValidValue : double.NaN;
    }

    // ── Weight computation ─────────────────────────────────────────────

    /// <summary>
    /// Computes original Igorad two-phase kernel weights.
    /// Phase zone (i=0..period-2): t = i/(period-2), g = t≤0.5 ? 1 : 1/(3πt+1), w = g*cos(πt)
    /// Cycle zone (i=period-1..flen-2): t = 1 + (i-phase)*(2*Cycle-1)/(Cycle*period-1), same g/w
    /// Last tap (i=flen-1): weight = 0.
    /// weights[0] = newest bar, weights[flen-1] = oldest bar.
    /// Returns the signed weight sum for normalization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeIgoradWeights(Span<double> weights, int period, int flen)
    {
        const int Cycle = 4;
        int phase = period - 1;
        double coeff = 3.0 * Math.PI;

        // Compute weights in "oldest-first" order (taps[0]=oldest, taps[flen-1]=newest)
        // then reverse into weights[] so weights[0]=newest, weights[flen-1]=oldest
        const int StackallocThreshold = 256;
        double[]? rented = flen > StackallocThreshold ? ArrayPool<double>.Shared.Rent(flen) : null;
        Span<double> taps = flen <= StackallocThreshold
            ? stackalloc double[flen]
            : rented!.AsSpan(0, flen);

        double wsum = 0.0;

        try
        {
            for (int i = 0; i < flen - 1; i++)
            {
                double t;
                if (i <= phase - 1)
                {
                    // Phase zone: t ramps from 0 to 1
                    t = phase > 1 ? (double)i / (phase - 1) : 0.0;
                }
                else
                {
                    // Cycle zone: t continues from 1 upward
                    double numer = (double)(i - phase) * (2 * Cycle - 1);
                    double denom = (double)(Cycle * period - 1);
                    t = 1.0 + (denom > 0 ? numer / denom : 0.0);
                }

                double beta = Math.Cos(Math.PI * t);
                double g = t <= 0.5 ? 1.0 : 1.0 / Math.FusedMultiplyAdd(coeff, t, 1.0);
                double w = g * beta;
                taps[i] = w;
                wsum += w;
            }

            // Last tap has weight 0 (original MQL4 loop goes to Len-2)
            taps[flen - 1] = 0.0;

            // Reverse into weights[]: weights[0]=newest=taps[flen-1], weights[flen-1]=oldest=taps[0]
            for (int i = 0; i < flen; i++)
            {
                weights[i] = taps[flen - 1 - i];
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }

        return wsum;
    }

    // ── Convolution ────────────────────────────────────────────────────

    /// <summary>
    /// Convolves when buffer is full (count == flen). Uses precomputed weights.
    /// weights[0] = newest, weights[flen-1] = oldest. Normalized by signed weight sum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ConvolveFull()
    {
        ReadOnlySpan<double> internalBuf = _buffer.InternalBuffer;
        int head = _buffer.StartIndex;
        int capacity = _buffer.Capacity;
        double sum = 0.0;

        // Iterate oldest-to-newest: oldest bar gets weights[flen-1], newest gets weights[0]
        int wi = _flen - 1;
        for (int i = head; i < capacity; i++)
        {
            sum = Math.FusedMultiplyAdd(internalBuf[i], _weights[wi], sum);
            wi--;
        }
        for (int i = 0; i < head; i++)
        {
            sum = Math.FusedMultiplyAdd(internalBuf[i], _weights[wi], sum);
            wi--;
        }

        return sum / _weightSum;
    }

    // ── Prime / Batch / Calculate ──────────────────────────────────────

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            UpdateCore(new TValue(DateTime.MinValue, value), isNew: true, publish: false);
        }
    }

    /// <summary>
    /// Calculates NLMA from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 14)
    {
        var nlma = new Nlma(period);
        return nlma.Update(source);
    }

    /// <summary>
    /// Calculates NLMA over a span of values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 14)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2", nameof(period));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    /// <summary>
    /// Creates a NLMA indicator and calculates results from source.
    /// </summary>
    public static (TSeries Results, Nlma Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Nlma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ── Static scalar core ─────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        int flen = ComputeFilterLength(period);

        const int StackallocThreshold = 256;

        // Allocate full weights
        double[]? weightsRented = flen > StackallocThreshold ? ArrayPool<double>.Shared.Rent(flen) : null;
        Span<double> weights = flen <= StackallocThreshold
            ? stackalloc double[flen]
            : weightsRented!.AsSpan(0, flen);

        // Allocate ring buffer
        double[]? ringRented = flen > StackallocThreshold ? ArrayPool<double>.Shared.Rent(flen) : null;
        Span<double> ring = flen <= StackallocThreshold
            ? stackalloc double[flen]
            : ringRented!.AsSpan(0, flen);

        // Allocate NaN-corrected array
        double[]? cleanRented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> clean = len <= StackallocThreshold
            ? stackalloc double[len]
            : cleanRented!.AsSpan(0, len);

        double fullWeightSum = ComputeIgoradWeights(weights, period, flen);

        try
        {
            // Build NaN-corrected values
            double lastValid = double.NaN;
            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                    clean[i] = val;
                }
                else if (double.IsFinite(lastValid))
                {
                    clean[i] = lastValid;
                }
                else
                {
                    clean[i] = double.NaN;
                }
            }

            // FIR convolution with growing-then-sliding window
            int ringIdx = 0;
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                double val = clean[i];

                ring[ringIdx] = val;
                ringIdx++;
                if (ringIdx >= flen)
                {
                    ringIdx = 0;
                }

                if (count < flen)
                {
                    count++;
                }

                if (count < flen)
                {
                    // Warmup: return input price
                    output[i] = val;
                    continue;
                }

                // Full window: convolve ring with weights, divide by signed sum
                double sum = 0.0;
                int wi = flen - 1;
                for (int k = 0; k < flen; k++)
                {
                    int idx = (ringIdx + k) % flen;
                    sum = Math.FusedMultiplyAdd(ring[idx], weights[wi], sum);
                    wi--;
                }

                output[i] = sum / fullWeightSum;
            }
        }
        finally
        {
            if (weightsRented != null)
            {
                ArrayPool<double>.Shared.Return(weightsRented);
            }
            if (ringRented != null)
            {
                ArrayPool<double>.Shared.Return(ringRented);
            }
            if (cleanRented != null)
            {
                ArrayPool<double>.Shared.Return(cleanRented);
            }
        }
    }

    // ── Reset / Dispose ────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void Reset()
    {
        _buffer.Clear();
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        Last = default;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null && _pubHandler != null)
            {
                _source.Pub -= _pubHandler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
