using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Volume Weighted Moving Average (VWMA) over a fixed lookback period.
/// </summary>
/// <remarks>
/// VWMA weights each price by volume over the last <c>period</c> samples:
/// <c>VWMA = Σ(price × volume) / Σ(volume)</c>.
///
/// This implementation is optimized for streaming updates with O(1) per bar using circular buffers.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed
/// for price and volume independently.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Vwma.md">Detailed documentation</seealso>
/// <seealso href="vwma.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Vwma : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SumPV, double SumVol, int Index, int Head, int Count, int SyncCounter)
    {
        public static State New() => new() { SumPV = 0, SumVol = 0, Index = 0, Head = 0, Count = 0, SyncCounter = 0 };
    }

    /// <summary>
    /// Resync interval to limit floating-point drift in running sums.
    /// Full recalculation every N bars.
    /// </summary>
    private const int ResyncInterval = 1000;

    private readonly int _period;
    private readonly double[] _priceBuffer;
    private readonly double[] _volBuffer;
    private State _state;
    private State _p_state;
    private double _lastValidClose;
    private double _lastValidVolume;
    private double _p_lastValidClose;
    private double _p_lastValidVolume;
    private double _p_bufferPrice;  // Previous price at current head position
    private double _p_bufferVol;    // Previous volume at current head position

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current VWMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed at least Period bars.
    /// </summary>
    public bool IsHot => _state.Count >= _period;

    /// <summary>
    /// Warmup period equals the specified period.
    /// </summary>
    // S2325 suppressed: Instance property required for interface consistency across all indicators,
    // even when value is constant. All QuanTAlib indicators expose WarmupPeriod as instance property.
#pragma warning disable S2325
    public int WarmupPeriod => _period;
#pragma warning restore S2325

    /// <summary>
    /// Creates a new VWMA indicator.
    /// </summary>
    /// <param name="period">Lookback period for VWMA calculation. Must be >= 1.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Vwma(int period = 20)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _priceBuffer = new double[period];
        _volBuffer = new double[period];
        _state = State.New();
        _p_state = State.New();
        Name = $"VWMA({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = State.New();
        _p_state = State.New();
        Array.Clear(_priceBuffer);
        Array.Clear(_volBuffer);
        _lastValidClose = 0;
        _lastValidVolume = 0;
        _p_lastValidClose = 0;
        _p_lastValidVolume = 0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(double input, ref double lastValid)
    {
        if (double.IsFinite(input))
        {
            lastValid = input;
            return input;
        }
        return lastValid;
    }

    /// <summary>
    /// Recalculates running sums from buffer to eliminate accumulated floating-point drift.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResyncRunningTotals(ref State s)
    {
        double sumPV = 0;
        double sumVol = 0;

        for (int i = 0; i < _period; i++)
        {
            double p = _priceBuffer[i];
            double v = _volBuffer[i];
            if (v > 0)
            {
                sumPV = Math.FusedMultiplyAdd(p, v, sumPV);
                sumVol += v;
            }
        }

        s.SumPV = sumPV;
        s.SumVol = sumVol;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, bool isNew = true)
    {
        // Use close price for VWMA calculation
        return UpdateInternal(input.Time, input.Close, input.Volume, isNew);
    }

    /// <summary>
    /// Updates VWMA with a TValue input (uses value as price, assumes volume=1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return UpdateInternal(input.Time, input.Value, 1.0, isNew);
    }

    /// <summary>
    /// Calculates VWMA for an entire bar series.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <returns>TSeries containing VWMA values</returns>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private TValue UpdateInternal(long time, double price, double volume, bool isNew)
    {
        // Local copy for struct promotion
        var s = _state;

        if (isNew)
        {
            _p_state = _state;
            _p_lastValidClose = _lastValidClose;
            _p_lastValidVolume = _lastValidVolume;
            // Save current buffer values at head position for rollback
            _p_bufferPrice = _priceBuffer[s.Head];
            _p_bufferVol = _volBuffer[s.Head];
        }
        else
        {
            // Restore previous state
            s = _p_state;
            _state = _p_state;
            _lastValidClose = _p_lastValidClose;
            _lastValidVolume = _p_lastValidVolume;
            // Restore buffer values at head position
            _priceBuffer[s.Head] = _p_bufferPrice;
            _volBuffer[s.Head] = _p_bufferVol;
        }

        // Get valid values
        double currentPrice = GetValidValue(price, ref _lastValidClose);
        double currentVol = GetValidValue(volume, ref _lastValidVolume);

        // Remove old values from circular buffer
        double oldPrice = _priceBuffer[s.Head];
        double oldVol = _volBuffer[s.Head];

        if (s.Count >= _period && oldVol > 0)
        {
            s.SumPV = Math.FusedMultiplyAdd(-oldPrice, oldVol, s.SumPV);
            s.SumVol -= oldVol;
        }

        // Add new values
        if (currentVol > 0)
        {
            s.SumPV = Math.FusedMultiplyAdd(currentPrice, currentVol, s.SumPV);
            s.SumVol += currentVol;
        }

        // Store in circular buffer
        _priceBuffer[s.Head] = currentPrice;
        _volBuffer[s.Head] = currentVol;

        // Advance head pointer
        s.Head = (s.Head + 1) % _period;

        if (isNew)
        {
            s.Index++;
            if (s.Count < _period)
            {
                s.Count++;
            }

            // Periodic resync to limit floating-point drift
            s.SyncCounter++;
            if (s.SyncCounter >= ResyncInterval && s.Count >= _period)
            {
                s.SyncCounter = 0;
                ResyncRunningTotals(ref s);
            }
        }

        // Calculate VWMA
        double vwma = s.SumVol > double.Epsilon ? s.SumPV / s.SumVol : currentPrice;

        _state = s;

        Last = new TValue(time, vwma);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }


    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    /// <param name="source">Historical bar data.</param>
    public void Prime(TBarSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Static calculation returning TSeries.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <param name="period">Lookback period for VWMA</param>
    /// <returns>TSeries containing VWMA values</returns>
    public static TSeries Batch(TBarSeries source, int period = 20)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Batch(source.Close.Values, source.Volume.Values, v, period);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Static calculation for TSeries (price with assumed volume=1).
    /// </summary>
    /// <param name="source">Source value series</param>
    /// <param name="period">Lookback period for VWMA</param>
    /// <returns>TSeries containing VWMA values</returns>
    public static TSeries Batch(TSeries source, int period = 20)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var t = source.Times.ToArray();
        var v = new double[source.Count];

        // Use span overload with uniform volume = 1
        Span<double> unitVolume = stackalloc double[source.Count];
        unitVolume.Fill(1.0);

        Batch(source.Values, unitVolume, v, period);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Zero-allocation span-based calculation.
    /// </summary>
    /// <param name="source">Source values</param>
    /// <param name="volume">Volume values</param>
    /// <param name="output">Output span for VWMA values</param>
    /// <param name="period">Lookback period for VWMA</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, ReadOnlySpan<double> volume, Span<double> output, int period = 20)
    {
        if (source.Length != volume.Length)
        {
            throw new ArgumentException("Source and Volume spans must be of the same length", nameof(volume));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;
        double[]? rentedPrice = null;
        double[]? rentedVol = null;
        scoped Span<double> priceBuffer;
        scoped Span<double> volBuffer;

        if (period <= StackallocThreshold)
        {
            priceBuffer = stackalloc double[period];
            volBuffer = stackalloc double[period];
        }
        else
        {
            rentedPrice = System.Buffers.ArrayPool<double>.Shared.Rent(period);
            rentedVol = System.Buffers.ArrayPool<double>.Shared.Rent(period);
            priceBuffer = rentedPrice.AsSpan(0, period);
            volBuffer = rentedVol.AsSpan(0, period);
        }

        try
        {
            priceBuffer.Clear();
            volBuffer.Clear();

            double sumPV = 0;
            double sumVol = 0;
            double lastValidPrice = 0;
            double lastValidVolume = 0;
            int head = 0;
            int count = 0;

            // Find first valid values
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValidPrice = source[k];
                    break;
                }
            }
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(volume[k]))
                {
                    lastValidVolume = volume[k];
                    break;
                }
            }

            int syncCounter = 0;

            for (int i = 0; i < len; i++)
            {
                // Get valid values with NaN substitution
                double currentPrice = double.IsFinite(source[i]) ? source[i] : lastValidPrice;
                double currentVol = double.IsFinite(volume[i]) ? volume[i] : lastValidVolume;

                if (double.IsFinite(source[i]))
                {
                    lastValidPrice = source[i];
                }
                if (double.IsFinite(volume[i]))
                {
                    lastValidVolume = volume[i];
                }

                // Remove old values from circular buffer
                double oldPrice = priceBuffer[head];
                double oldVol = volBuffer[head];

                if (count >= period && oldVol > 0)
                {
                    sumPV = Math.FusedMultiplyAdd(-oldPrice, oldVol, sumPV);
                    sumVol -= oldVol;
                }

                // Add new values
                if (currentVol > 0)
                {
                    sumPV = Math.FusedMultiplyAdd(currentPrice, currentVol, sumPV);
                    sumVol += currentVol;
                }

                // Store in circular buffer
                priceBuffer[head] = currentPrice;
                volBuffer[head] = currentVol;

                // Advance head pointer
                head = (head + 1) % period;

                if (count < period)
                {
                    count++;
                }

                // Periodic resync to limit floating-point drift
                syncCounter++;
                if (syncCounter >= ResyncInterval && count >= period)
                {
                    syncCounter = 0;
                    // Recalculate sums from buffer
                    sumPV = 0;
                    sumVol = 0;
                    for (int j = 0; j < period; j++)
                    {
                        double pj = priceBuffer[j];
                        double vj = volBuffer[j];
                        if (vj > 0)
                        {
                            sumPV = Math.FusedMultiplyAdd(pj, vj, sumPV);
                            sumVol += vj;
                        }
                    }
                }

                // Calculate VWMA
                output[i] = sumVol > double.Epsilon ? sumPV / sumVol : currentPrice;
            }
        }
        finally
        {
            if (rentedPrice != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedPrice);
            }
            if (rentedVol != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedVol);
            }
        }
    }

    public static (TSeries Results, Vwma Indicator) Calculate(TBarSeries source, int period = 20)
    {
        var indicator = new Vwma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}