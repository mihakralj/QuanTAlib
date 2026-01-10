using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Hann Filter: A Finite Impulse Response (FIR) filter using Hann window coefficients.
/// The Hann window is defined as w(n) = 0.5 * (1 - cos(2*pi*n/(N-1))).
/// This filter provides strong smoothing properties but introduces lag, as the weights
/// typically start and end at zero.
/// </summary>
public sealed class Hann : AbstractBase
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private struct State
    {
        public double LastValue;
        public bool IsHot;
    }

    /// <summary>
    /// Gets the length of the window (period).
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="Hann"/> class.
    /// </summary>
    /// <param name="length">The lookback period (window length). Must be greater than 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is less than or equal to 1.</exception>
    public Hann(int length)
    {
        if (length <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 1.");
        }

        _length = length;
        WarmupPeriod = length;
        Name = $"Hann({length})";
        _buffer = new RingBuffer(length);
        _weights = new double[length];
        GenerateWeights();
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Hann"/> class with a publisher source.
    /// </summary>
    /// <param name="source">The source publisher.</param>
    /// <param name="length">The lookback period.</param>
    public Hann(ITValuePublisher source, int length) : this(length)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = new State { LastValue = double.NaN };
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GenerateWeights()
    {
        // Formula: 0.5 * (1.0 - cos(2*pi*i / (len - 1)))
        // i goes from 0 to len-1
        // Note: Pine script implements this exactly.
        // w[0] and w[len-1] will be 0.

        double coefSum = 0;
        double denom = _length - 1;

        for (int i = 0; i < _length; i++)
        {
            double w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / denom));
            _weights[i] = w;
            coefSum += w;
        }

        // Ideally we shouldn't normalize here if we match Pine script logic
        // which accumulates 'currentWeightSum' dynamically.
        // However, for optimization, since we handle NaNs, dynamic normalization is safer.
        // But if 'coefSum' is constant (no NaNs), we could pre-normalize.
        // The Pine script does: nz(currentWeightSum == 0.0 ? src : acc / currentWeightSum, src)
        // This implies dynamic normalization. We will follow that for correctness with NaNs.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        Init();
        _buffer.Clear();
        Last = default;
    }

    public override bool IsHot => _buffer.IsFull;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _buffer.Add(input.Value);
        }
        else
        {
            _state = _p_state;
            _buffer.UpdateNewest(input.Value);
        }

        double result = 0;
        double wSum = 0;
        int count = _buffer.Count;

        // Convolution:
        // We apply weights to the buffer.
        // In Pine:
        //   p = min(bar_index + 1, len) -> this is our 'count'
        //   loop i from 0 to p-1
        //   price = src[p - 1 - i] -> this is accessing history. 'i=0' is newest.
        //   weight = w[i]
        //   acc += price * weight
        //
        // Mapping to RingBuffer:
        //   _buffer[count - 1] is newest (corresponds to Pine's src[p - 1 - 0])
        //   _buffer[count - 1 - i] corresponds to Pine's src[p - 1 - i]
        //   So we iterate i from 0 to count-1.
        //   weight index is 'i'.
        //   buffer index is 'count - 1 - i'.

        for (int i = 0; i < count; i++)
        {
            double val = _buffer[count - 1 - i];
            if (!double.IsNaN(val))
            {
                // Align weights to match Pine Script logic (Lag 0 uses weight[count-1])
                double w = _weights[count - 1 - i];
                result = Math.FusedMultiplyAdd(val, w, result);
                wSum += w;
            }
        }

        if (wSum > double.Epsilon)
        {
            result /= wSum;
        }
        else
        {
            result = !double.IsNaN(input.Value) ? input.Value : _state.LastValue;
        }

        _state.IsHot = IsHot;
        _state.LastValue = result;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);

        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        // Calculate using static method for performance
        var resultValues = new double[source.Count];
        Calculate(source.Values, resultValues, _length);

        // Convert to TSeries
        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        // Restore state based on last values
        // We need to feed at least the last _length values to the buffer
        int startup = Math.Max(0, source.Count - _length);
        Reset();
        for (int i = startup; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }

        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value), isNew: true);
        }
    }

    /// <summary>
    /// Static calculation of Hann Filter on a span.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="output">Output buffer (must be same length as source)</param>
    /// <param name="length">Lookback length</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int length)
    {
        if (length <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 1.");
        }
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }

        // Precompute weights
        // Use stackalloc if small enough, otherwise array pool or heap
        // 256 doubles is 2KB, safe for stack
        Span<double> weights = length <= 256 ? stackalloc double[length] : new double[length];

        double denom = length - 1;
        for (int i = 0; i < length; i++)
        {
            weights[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / denom));
        }

        double lastValue = double.NaN;
        for (int i = 0; i < source.Length; i++)
        {
            double result = 0;
            double wSum = 0;
            int count = Math.Min(i + 1, length);

            // Same loop logic as Update:
            // Iterate k from 0 to count-1 representing lag.
            // Source index: i - k
            // Weight index: k

            for (int k = 0; k < count; k++)
            {
                double val = source[i - k];
                if (!double.IsNaN(val))
                {
                    // Align weights to match Pine Script logic (Lag 0 uses weight[count-1])
                    double w = weights[count - 1 - k];
                    result = Math.FusedMultiplyAdd(val, w, result);
                    wSum += w;
                }
            }

            if (wSum > double.Epsilon)
            {
                output[i] = result / wSum;
            }
            else
            {
                output[i] = !double.IsNaN(source[i]) ? source[i] : lastValue;
            }
            lastValue = output[i];
        }
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
