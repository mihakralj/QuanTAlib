using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BWMA: Bessel-Weighted Moving Average
/// </summary>
/// <remarks>
/// FIR MA using Bessel window coefficients with adjustable order.
/// Higher order produces sharper window; order 0 = parabolic.
///
/// Calculation: <c>W_i = (1 - x²)^(order/2 + 0.5)</c> where <c>x = 2i/(n-1) - 1</c>.
/// </remarks>
/// <seealso href="Bwma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Bwma : AbstractBase
{
    private readonly int _period;
    private readonly int _order;
    private readonly double _power;
    private readonly double[] _weights;
    private readonly double _invWeightSum;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _source;
    private readonly TValuePublishedHandler? _pubHandler;
    private bool _isNew = true;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double LastValidValue;
        public bool IsInitialized;
    }
    private State _state;
    private State _p_state;

    public bool IsNew => _isNew;
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates BWMA with specified parameters.
    /// </summary>
    /// <param name="period">Window size (must be > 0)</param>
    /// <param name="order">Bessel function order (0-3, default 0). Higher orders produce sharper windows.</param>
    public Bwma(int period, int order = 0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Order must be non-negative");
        }

        _period = period;
        _order = order;
        _power = order * 0.5 + 0.5;
        _buffer = new RingBuffer(period);
        _weights = new double[period];
        Name = $"Bwma({period}, {order})";
        WarmupPeriod = period;

        ComputeWeights(_weights, period, order, out _invWeightSum);
        _state = new State { LastValidValue = double.NaN, IsInitialized = false };
    }

    public Bwma(ITValuePublisher source, int period, int order = 0)
        : this(period, order)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    protected override void Dispose(bool disposing)
    {
        if (disposing && _source != null && _pubHandler != null)
        {
            _source.Pub -= _pubHandler;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Computes Bessel window weights.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeWeights(Span<double> weights, int period, int order, out double invWeightSum)
    {
        double sum = 0;
        double scale = period > 1 ? 2.0 / (period - 1) : 0.0;
        double power = order * 0.5 + 0.5;

        for (int i = 0; i < period; i++)
        {
            double x = period > 1 ? i * scale - 1.0 : 0.0;
            double arg = 1.0 - x * x;

            double w;
            if (arg > 0.0)
            {
                // Match PineScript behavior exactly:
                // order=0: w = arg (parabolic, power=1)
                // order=1: w = arg * sqrt(arg) (power=1.5)
                // order>=2: w = pow(arg, order/2 + 0.5)
                if (order == 0)
                {
                    w = arg;  // (1 - x²)^1.0 - parabolic window
                }
                else if (order == 1)
                {
                    w = arg * Math.Sqrt(arg);  // (1 - x²)^1.5
                }
                else
                {
                    w = Math.Pow(arg, power);  // (1 - x²)^power
                }
            }
            else
            {
                w = 0.0;
            }

            weights[i] = w;
            sum += w;
        }

        invWeightSum = sum > 0 ? 1.0 / sum : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            return input;
        }
        return _state.IsInitialized ? _state.LastValidValue : double.NaN;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        return Update(input, isNew, publish: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue Update(TValue input, bool isNew, bool publish)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        if (double.IsFinite(input.Value))
        {
            _state.LastValidValue = input.Value;
            _state.IsInitialized = true;
        }

        double val = GetValidValue(input.Value);
        _buffer.Add(val, isNew);

        double result = _buffer.Count > 0 ? CalculateWeightedSum(fallbackValue: val) : 0.0;

        Last = new TValue(input.Time, result);
        if (publish)
        {
            PubEvent(Last);
        }
        return Last;
    }

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

        Calculate(source.Values, vSpan, _period, _order);
        source.Times.CopyTo(tSpan);

        _buffer.Clear();

        int windowSize = Math.Min(len, _period);
        int startIndex = len - windowSize;

        _state = default;
        _state.LastValidValue = double.NaN;
        _state.IsInitialized = false;
        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                double v0 = source.Values[i];
                if (double.IsFinite(v0))
                {
                    _state.LastValidValue = v0;
                    _state.IsInitialized = true;
                    break;
                }
            }
        }
        else
        {
            _state.LastValidValue = double.NaN;
            _state.IsInitialized = false;
        }

        for (int i = startIndex; i < len; i++)
        {
            Update(source[i], isNew: true, publish: false);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateWeightedSum(double fallbackValue)
    {
        int count = _buffer.Count;
        if (count == 0)
        {
            return 0;
        }

        if (count < _period)
        {
            return CalculateWeightedSumWarmup(_buffer.GetSpan(), count, _order, _power, fallbackValue);
        }

        if (_invWeightSum == 0.0)
        {
            return fallbackValue;
        }

        ReadOnlySpan<double> internalBuf = _buffer.InternalBuffer;
        int head = _buffer.StartIndex;

        int part1Len = _period - head;
        double sum1 = internalBuf.Slice(head, part1Len).DotProduct(_weights.AsSpan(0, part1Len));
        double sum2 = internalBuf[..head].DotProduct(_weights.AsSpan(part1Len));

        return (sum1 + sum2) * _invWeightSum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateWeightedSumWarmup(ReadOnlySpan<double> window, int p, int order, double power, double fallbackValue)
    {
        if (p <= 0)
        {
            return 0.0;
        }

        if (p == 1)
        {
            return fallbackValue;
        }

        if (p == 2)
        {
            return fallbackValue;
        }

        double scale = 2.0 / (p - 1);
        double sum = 0.0;
        double wSum = 0.0;

        for (int i = 0; i < p; i++)
        {
            double x = Math.FusedMultiplyAdd(i, scale, -1.0);
            double arg = Math.FusedMultiplyAdd(-x, x, 1.0);
            if (arg <= 0.0)
            {
                continue;
            }

            double w;
            if (order == 0)
            {
                w = arg;
            }
            else if (order == 1)
            {
                w = arg * Math.Sqrt(arg);
            }
            else
            {
                w = Math.Pow(arg, power);
            }

            if (w == 0.0)
            {
                continue;
            }

            sum = Math.FusedMultiplyAdd(window[i], w, sum);
            wSum += w;
        }

        return wSum > 0.0 ? sum / wSum : fallbackValue;
    }

    public static TSeries Batch(TSeries source, int period, int order = 0)
    {
        var bwma = new Bwma(period, order);
        return bwma.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period, int order = 0)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Order must be non-negative");
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double power = order * 0.5 + 0.5;

        if (period > len)
        {
            double[]? bufferArray = len > 256 ? ArrayPool<double>.Shared.Rent(len) : null;
            Span<double> buffer = len <= 256
                ? stackalloc double[len]
                : bufferArray!.AsSpan(0, len);

            double lastValid = double.NaN;

            try
            {
                for (int i = 0; i < len; i++)
                {
                    double val = source[i];
                    if (double.IsFinite(val))
                    {
                        lastValid = val;
                    }
                    else if (double.IsFinite(lastValid))
                    {
                        val = lastValid;
                    }

                    buffer[i] = val;
                    int p = i + 1;
                    output[i] = CalculateWeightedSumWarmup(buffer, p, order, power, fallbackValue: val);
                }
            }
            finally
            {
                if (bufferArray != null)
                {
                    ArrayPool<double>.Shared.Return(bufferArray);
                }
            }

            return;
        }

        double[]? weightsArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> weights = period <= 256
            ? stackalloc double[period]
            : weightsArray!.AsSpan(0, period);

        double[]? ringArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> ring = period <= 256
            ? stackalloc double[period]
            : ringArray!.AsSpan(0, period);

        ComputeWeights(weights, period, order, out double invWeightSum);

        int ringIdx = 0;
        int count = 0;
        double lastValid2 = double.NaN;

        try
        {
            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid2 = val;
                }
                else if (double.IsFinite(lastValid2))
                {
                    val = lastValid2;
                }

                ring[ringIdx] = val;
                ringIdx++;
                if (ringIdx >= period)
                {
                    ringIdx = 0;
                }

                if (count < period)
                {
                    count++;
                }

                if (count < period)
                {
                    output[i] = CalculateWeightedSumWarmup(ring, count, order, power, fallbackValue: val);
                    continue;
                }

                if (invWeightSum == 0.0)
                {
                    output[i] = val;
                    continue;
                }

                int part1Len = period - ringIdx;
                double sum = ring.Slice(ringIdx, part1Len).DotProduct(weights.Slice(0, part1Len))
                           + ring.Slice(0, ringIdx).DotProduct(weights.Slice(part1Len));

                output[i] = sum * invWeightSum;
            }
        }
        finally
        {
            if (weightsArray != null)
            {
                ArrayPool<double>.Shared.Return(weightsArray);
            }

            if (ringArray != null)
            {
                ArrayPool<double>.Shared.Return(ringArray);
            }
        }
    }

    public override void Reset()
    {
        _buffer.Clear();
        _state = new State { LastValidValue = double.NaN, IsInitialized = false };
        _p_state = _state;
        Last = default;
    }
}
