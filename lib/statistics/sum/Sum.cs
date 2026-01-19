using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Sum: Summation over a rolling window using Kahan-Babuška algorithm
/// </summary>
/// <remarks>
/// Sum calculates the sum of the last n values using the Kahan-Babuška summation
/// algorithm (also known as "improved Kahan") for maximum numerical precision.
///
/// Kahan-Babuška fixes second-order rounding errors that classic Kahan misses:
/// - Tracks two compensation layers: primary (c) and secondary (cc)
/// - Captures rounding losses that Kahan itself introduces
/// - Error bounded closer to machine epsilon, even for pathological sequences
///
/// Algorithm:
/// For each value x:
///   y  = x - c
///   t  = sum + y
///   c  = (t - sum) - y
///   sum = t
///   // compensate the compensation
///   z  = c - cc
///   tt = sum + z
///   cc = (tt - sum) - z
///   sum = tt
///
/// Key Features:
/// - Near machine-epsilon accuracy for streaming summation
/// - Handles adversarial inputs (wildly different magnitudes)
/// - O(1) time complexity per update with RingBuffer
/// - Branch-free core algorithm
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Sum : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;

    [StructLayout(LayoutKind.Auto)]
    private struct State
    {
        public double Sum;           // Accumulated sum
        public double C;             // First-order compensation
        public double Cc;            // Second-order compensation
        public double LastInput;
        public double LastValidValue;
        public int TickCount;
    }

    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    /// <summary>
    /// Creates Sum with specified period.
    /// </summary>
    /// <param name="period">Number of values to sum (must be > 0)</param>
    public Sum(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Sum({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Sum(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    public Sum(TSeries source, int period) : this(period)
    {
        source.Pub += _handler;
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _p_state = _state;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode B: Streaming (Stateful)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// True if the Sum has enough data to produce valid results.
    /// Sum is "hot" when the buffer is full (has received at least 'period' values).
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Kahan-Babuška Core Operations
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Adds a value using Kahan-Babuška summation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void KahanBabuskaAdd(double x)
    {
        // Primary Kahan step
        double y = x - _state.C;
        double t = _state.Sum + y;
        _state.C = t - _state.Sum - y;
        _state.Sum = t;

        // Secondary compensation (Babuška improvement)
        double z = _state.C - _state.Cc;
        double tt = _state.Sum + z;
        _state.Cc = tt - _state.Sum - z;
        _state.Sum = tt;
    }

    /// <summary>
    /// Subtracts a value using Kahan-Babuška summation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void KahanBabuskaSubtract(double x)
    {
        KahanBabuskaAdd(-x);
    }

    /// <summary>
    /// Recalculates the sum from scratch using Kahan-Babuška.
    /// Used for periodic resync to prevent drift.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateSum()
    {
        _state.Sum = 0;
        _state.C = 0;
        _state.Cc = 0;

        var bufferSpan = _buffer.GetSpan();
        for (int i = 0; i < bufferSpan.Length; i++)
        {
            KahanBabuskaAdd(bufferSpan[i]);
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode C: Priming (The Bridge)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        // Reset state
        _buffer.Clear();
        _state = default;
        _p_state = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // Seed LastValidValue
        _state.LastValidValue = double.NaN;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _state.LastValidValue = source[i];
                break;
            }
        }

        if (double.IsNaN(_state.LastValidValue))
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _state.LastValidValue = source[i];
                    break;
                }
            }
        }

        // Feed the buffer and calculate sum
        for (int i = startIndex; i < source.Length; i++)
        {
            double val = GetValidValue(source[i]);
            _buffer.Add(val);
            KahanBabuskaAdd(val);
            _state.LastInput = val;
        }

        Last = new TValue(DateTime.MinValue, _state.Sum);
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue = input;
            return input;
        }
        return _state.LastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateState(double val)
    {
        if (_buffer.Count == _buffer.Capacity)
        {
            KahanBabuskaSubtract(_buffer.Oldest);
        }

        _buffer.Add(val);
        KahanBabuskaAdd(val);

        _state.TickCount++;
        if (_buffer.IsFull && _state.TickCount >= ResyncInterval)
        {
            _state.TickCount = 0;
            RecalculateSum();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;

            double val = GetValidValue(input.Value);
            UpdateState(val);
            _state.LastInput = val;
        }
        else
        {
            _state = _p_state;

            double val = GetValidValue(input.Value);

            // Recalculate: remove old bar value, add new correction value
            if (_buffer.Count == _buffer.Capacity)
            {
                KahanBabuskaSubtract(_buffer.Oldest);
            }

            // Replace the newest value in buffer
            if (_buffer.Count > 0)
            {
                // We need to subtract the value that was added and add the new one
                // Since we restored state, we add directly
                _buffer.UpdateNewest(val);
                RecalculateSum(); // Ensure accuracy after correction
            }
            else
            {
                _buffer.Add(val);
                KahanBabuskaAdd(val);
            }
        }

        Last = new TValue(input.Time, _state.Sum);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////
    // Mode A: Batch (Stateless)
    /////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Calculates Sum for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var sum = new Sum(period);
        return sum.Update(source);
    }

    /// <summary>
    /// Calculates Sum in-place using Kahan-Babuška summation.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        CalculateScalarCore(source, output, period);
    }

    /// <summary>
    /// Runs a batch calculation and returns a "Hot" Sum instance.
    /// </summary>
    public static (TSeries Results, Sum Indicator) Calculate(TSeries source, int period)
    {
        var sum = new Sum(period);
        TSeries results = sum.Update(source);
        return (results, sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;
        double[]? bufferArray = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> buffer = period <= StackAllocThreshold
            ? stackalloc double[period]
            : bufferArray!.AsSpan(0, period);

        // Kahan-Babuška state
        double sum = 0;
        double c = 0;   // First-order compensation
        double cc = 0;  // Second-order compensation
        double lastValid = double.NaN;

        // Find first valid value
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                lastValid = source[k];
                break;
            }
        }

        try
        {
            int bufferIndex = 0;
            int tickCount = 0;

            // Warmup phase
        int warmupEnd = Math.Min(period, len);
        for (int i = 0; i < warmupEnd; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // Kahan-Babuška add
            double y = val - c;
            double t = sum + y;
            c = t - sum - y;
            sum = t;

            double z = c - cc;
            double tt = sum + z;
            cc = tt - sum - z;
            sum = tt;

            buffer[i] = val;
            output[i] = sum;
        }

        // Main phase with sliding window
        for (int i = period; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double oldVal = buffer[bufferIndex];

            // Kahan-Babuška subtract old value
            double yS = -oldVal - c;
            double tS = sum + yS;
            c = tS - sum - yS;
            sum = tS;

            double zS = c - cc;
            double ttS = sum + zS;
            cc = ttS - sum - zS;
            sum = ttS;

            // Kahan-Babuška add new value
            double yA = val - c;
            double tA = sum + yA;
            c = tA - sum - yA;
            sum = tA;

            double zA = c - cc;
            double ttA = sum + zA;
            cc = ttA - sum - zA;
            sum = ttA;

            buffer[bufferIndex] = val;
            bufferIndex++;
            if (bufferIndex >= period)
                bufferIndex = 0;

            output[i] = sum;

            // Periodic resync for long sequences
            tickCount++;
                if (tickCount >= ResyncInterval)
                {
                    tickCount = 0;
                    sum = 0;
                    c = 0;
                    cc = 0;
                    for (int k = 0; k < period; k++)
                    {
                        double bVal = buffer[k];
                        double yR = bVal - c;
                        double tR = sum + yR;
                        c = tR - sum - yR;
                        sum = tR;

                        double zR = c - cc;
                        double ttR = sum + zR;
                        cc = ttR - sum - zR;
                        sum = ttR;
                    }
                }
            }
        }
        finally
        {
            if (bufferArray != null) ArrayPool<double>.Shared.Return(bufferArray);
        }
    }

    /// <summary>
    /// Resets the Sum state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}