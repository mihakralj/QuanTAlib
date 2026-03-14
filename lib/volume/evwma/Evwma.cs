using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Elastic Volume Weighted Moving Average (EVWMA) over a fixed lookback period.
/// </summary>
/// <remarks>
/// EVWMA weights each bar elastically by its volume relative to the rolling volume sum:
/// <c>EVWMA = ((sumVol - curVol) * prevResult + curVol * curPrice) / sumVol</c>.
///
/// High-volume bars shift the average more aggressively; low-volume bars barely nudge it.
/// The rolling volume sum uses a circular buffer for O(1) streaming updates.
///
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed
/// for price and volume independently.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Evwma.md">Detailed documentation</seealso>
/// <seealso href="evwma.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Evwma : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double SumVol, double SumVolComp, double Result, int Index, int Head, int Count)
    {
        public static State New() => new() { SumVol = 0, SumVolComp = 0, Result = double.NaN, Index = 0, Head = 0, Count = 0 };
    }

    private readonly int _period;
    private readonly double[] _volBuffer;
    private State _state;
    private State _p_state;
    private double _lastValidClose;
    private double _lastValidVolume;
    private double _p_lastValidClose;
    private double _p_lastValidVolume;
    private double _p_bufferVol;    // Previous volume at current head position

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current EVWMA value.
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
    /// Creates a new EVWMA indicator.
    /// </summary>
    /// <param name="period">Lookback period for rolling volume sum. Must be >= 1.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Evwma(int period = 20)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be >= 1", nameof(period));
        }

        _period = period;
        _volBuffer = new double[period];
        _state = State.New();
        _p_state = State.New();
        Name = $"EVWMA({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = State.New();
        _p_state = State.New();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, bool isNew = true)
    {
        return UpdateInternal(input.Time, input.Close, input.Volume, isNew);
    }

    /// <summary>
    /// Updates EVWMA with a TValue input (uses value as price, assumes volume=1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return UpdateInternal(input.Time, input.Value, 1.0, isNew);
    }

    /// <summary>
    /// Calculates EVWMA for an entire bar series.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <returns>TSeries containing EVWMA values</returns>
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
            // Save current buffer value at head position for rollback
            _p_bufferVol = _volBuffer[s.Head];
        }
        else
        {
            // Restore previous state
            s = _p_state;
            _state = _p_state;
            _lastValidClose = _p_lastValidClose;
            _lastValidVolume = _p_lastValidVolume;
            // Restore buffer value at head position
            _volBuffer[s.Head] = _p_bufferVol;
            // Reset Kahan compensation on re-entry
            s.SumVolComp = 0;
        }

        // Get valid values
        double currentPrice = GetValidValue(price, ref _lastValidClose);
        double currentVol = GetValidValue(volume, ref _lastValidVolume);
        currentVol = Math.Max(0.0, currentVol);

        // Kahan-compensated delta update for SumVol
        double oldVol = _volBuffer[s.Head];
        double delta = currentVol - (s.Count >= _period ? oldVol : 0);
        double y = delta - s.SumVolComp;
        double t = s.SumVol + y;
        s.SumVolComp = (t - s.SumVol) - y;
        s.SumVol = t;

        // Store in circular buffer
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
        }

        // EVWMA calculation
        double result;
        if (double.IsNaN(s.Result))
        {
            // First bar: initialize to current price
            result = currentPrice;
        }
        else if (s.SumVol > double.Epsilon)
        {
            // EVWMA = ((sumVol - curVol) * prevResult + curVol * curPrice) / sumVol
            double remainVol = s.SumVol - currentVol;
            result = Math.FusedMultiplyAdd(remainVol, s.Result, currentVol * currentPrice) / s.SumVol;
        }
        else
        {
            result = s.Result;
        }

        s.Result = result;
        _state = s;

        Last = new TValue(time, result);
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
    /// Static calculation returning TSeries from bar series.
    /// </summary>
    /// <param name="source">Source bar series</param>
    /// <param name="period">Lookback period for rolling volume sum</param>
    /// <returns>TSeries containing EVWMA values</returns>
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
    /// <param name="period">Lookback period for rolling volume sum</param>
    /// <returns>TSeries containing EVWMA values</returns>
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
    /// <param name="source">Source price values</param>
    /// <param name="volume">Volume values</param>
    /// <param name="output">Output span for EVWMA values</param>
    /// <param name="period">Lookback period for rolling volume sum</param>
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
        double[]? rentedVol = null;
        scoped Span<double> volBuffer;

        if (period <= StackallocThreshold)
        {
            volBuffer = stackalloc double[period];
        }
        else
        {
            rentedVol = System.Buffers.ArrayPool<double>.Shared.Rent(period);
            volBuffer = rentedVol.AsSpan(0, period);
        }

        try
        {
            volBuffer.Clear();

            double sumVol = 0;
            double sumVolComp = 0;
            double result = double.NaN;
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

            for (int i = 0; i < len; i++)
            {
                // Get valid values with NaN substitution
                double currentPrice = double.IsFinite(source[i]) ? source[i] : lastValidPrice;
                double currentVol = double.IsFinite(volume[i]) ? volume[i] : lastValidVolume;
                currentVol = Math.Max(0.0, currentVol);

                if (double.IsFinite(source[i]))
                {
                    lastValidPrice = source[i];
                }
                if (double.IsFinite(volume[i]))
                {
                    lastValidVolume = volume[i];
                }

                // Kahan-compensated delta update for SumVol
                double oldVol = volBuffer[head];
                double delta = currentVol - (count >= period ? oldVol : 0);
                double y = delta - sumVolComp;
                double t = sumVol + y;
                sumVolComp = (t - sumVol) - y;
                sumVol = t;

                // Store in circular buffer
                volBuffer[head] = currentVol;

                // Advance head pointer
                head = (head + 1) % period;

                if (count < period)
                {
                    count++;
                }

                // EVWMA calculation
                if (double.IsNaN(result))
                {
                    result = currentPrice;
                }
                else if (sumVol > double.Epsilon)
                {
                    double remainVol = sumVol - currentVol;
                    result = Math.FusedMultiplyAdd(remainVol, result, currentVol * currentPrice) / sumVol;
                }

                output[i] = result;
            }
        }
        finally
        {
            if (rentedVol != null)
            {
                System.Buffers.ArrayPool<double>.Shared.Return(rentedVol);
            }
        }
    }

    public static (TSeries Results, Evwma Indicator) Calculate(TBarSeries source, int period = 20)
    {
        var indicator = new Evwma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
