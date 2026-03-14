using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SGMA: Savitzky-Golay Moving Average
/// </summary>
/// <remarks>
/// Polynomial-fitting FIR filter preserving peaks and inflection points.
/// Superior shape preservation vs standard MAs; odd period required.
///
/// Calculation: <c>W_i = 1 - |norm_x|^d</c> with degree 0-4 controlling smoothing.
/// </remarks>
/// <seealso href="Sgma.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Sgma : AbstractBase
{
    private readonly int _period;
    private readonly int _degree;
    private readonly double[] _weights;
    private readonly double _invWeightSum;
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
    /// Creates SGMA with specified period and polynomial degree.
    /// </summary>
    /// <param name="period">Lookback period (must be >= 3, adjusted to odd if even)</param>
    /// <param name="degree">Polynomial degree (0-4, default 2)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sgma(int period = 9, int degree = 2)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3", nameof(period));
        }

        if (degree < 0 || degree > 4)
        {
            throw new ArgumentException("Degree must be between 0 and 4", nameof(degree));
        }

        // Ensure period is odd
        _period = period % 2 == 0 ? period + 1 : period;
        _degree = degree >= _period ? 2 : degree;
        Name = $"Sgma({_period},{_degree})";
        WarmupPeriod = _period;

        _buffer = new RingBuffer(_period);
        _weights = new double[_period];

        ComputeWeights(_weights, _period, _degree, out _invWeightSum);
    }

    /// <summary>
    /// Creates SGMA with specified period and polynomial degree, connected to a data source.
    /// </summary>
    /// <param name="source">Data source for event-based updates</param>
    /// <param name="period">Lookback period (default: 9)</param>
    /// <param name="degree">Polynomial degree (default: 2)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sgma(ITValuePublisher source, int period = 9, int degree = 2) : this(period, degree)
    {
        _source = source;
        _pubHandler = Handle;
        _source.Pub += _pubHandler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeWeights(Span<double> weights, int period, int degree, out double invWeightSum)
    {
        if (degree == 2)
        {
            if (period == 5)
            {
                weights[0] = -0.0857;
                weights[1] = 0.3429;
                weights[2] = 0.4857;
                weights[3] = 0.3429;
                weights[4] = -0.0857;
                double sum5 = weights[0] + weights[1] + weights[2] + weights[3] + weights[4];
                invWeightSum = Math.Abs(sum5) > double.Epsilon ? 1.0 / sum5 : 0.0;
                return;
            }

            if (period == 7)
            {
                weights[0] = -0.0476;
                weights[1] = 0.0952;
                weights[2] = 0.2857;
                weights[3] = 0.3333;
                weights[4] = 0.2857;
                weights[5] = 0.0952;
                weights[6] = -0.0476;
                double sum7 = 0.0;
                for (int i = 0; i < 7; i++)
                {
                    sum7 += weights[i];
                }

                invWeightSum = Math.Abs(sum7) > double.Epsilon ? 1.0 / sum7 : 0.0;
                return;
            }

            if (period == 9)
            {
                weights[0] = -0.0281;
                weights[1] = 0.0337;
                weights[2] = 0.1236;
                weights[3] = 0.2247;
                weights[4] = 0.2921;
                weights[5] = 0.2247;
                weights[6] = 0.1236;
                weights[7] = 0.0337;
                weights[8] = -0.0281;
                double sum9 = 0.0;
                for (int i = 0; i < 9; i++)
                {
                    sum9 += weights[i];
                }

                invWeightSum = Math.Abs(sum9) > double.Epsilon ? 1.0 / sum9 : 0.0;
                return;
            }
        }

        double halfWindow = (period - 1) * 0.5;
        double sum = 0.0;

        for (int i = 0; i < period; i++)
        {
            double x = i - halfWindow;
            double normX = halfWindow > 0.0 ? x / halfWindow : 0.0;

            double w = degree switch
            {
                0 => 1.0,
                1 => 1.0 - Math.Abs(normX),
                2 => 1.0 - (normX * normX),
                3 => 1.0 - Math.Abs(normX * normX * normX),
                4 => 1.0 - (normX * normX * normX * normX),
                _ => 1.0 - (normX * normX)
            };

            weights[i] = w;
            sum += w;
        }

        invWeightSum = sum > 0.0 ? 1.0 / sum : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            return input;
        }
        return double.IsFinite(_lastValidValue) ? _lastValidValue : double.NaN;
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

            double result = count < _period
                ? CalculateWeightedSumWarmup(_buffer.GetSpan(), count, _degree, fallbackValue: val)
                : CalculateWeightedSumFull(_buffer, _weights, _invWeightSum, fallbackValue: val);

            Last = new TValue(input.Time, result);
            if (publish)
            {
                PubEvent(Last, isNew);
            }
            return Last;
        }
        else
        {
            // For isNew==false: snapshot buffer, compute, restore
            _buffer.Snapshot();
            double prevLast = _lastValidValue;
            double prevPLast = _p_lastValidValue;

            _lastValidValue = val;
            _buffer.UpdateNewest(val);

            int count = _buffer.Count;

            double result = count < _period
                ? CalculateWeightedSumWarmup(_buffer.GetSpan(), count, _degree, fallbackValue: val)
                : CalculateWeightedSumFull(_buffer, _weights, _invWeightSum, fallbackValue: val);

            Last = new TValue(input.Time, result);

            // Restore buffer and state for non-new updates
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

        Batch(source.Values, vSpan, _period, _degree);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying last period bars
        Reset();
        int startIndex = Math.Max(0, len - _period);
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

    /// <summary>
    /// Calculates SGMA from a TSeries using streaming updates.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 9, int degree = 2)
    {
        var sgma = new Sgma(period, degree);
        return sgma.Update(source);
    }

    /// <summary>
    /// Calculates SGMA over a span of values.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output buffer (must be same length as source)</param>
    /// <param name="period">Period for weight calculation (default: 9)</param>
    /// <param name="degree">Polynomial degree (default: 2)</param>
    /// <exception cref="ArgumentException">Thrown when output length doesn't match source length.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 9, int degree = 2)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be at least 3", nameof(period));
        }

        if (degree < 0 || degree > 4)
        {
            throw new ArgumentException("Degree must be between 0 and 4", nameof(degree));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        int usePeriod = period % 2 == 0 ? period + 1 : period;
        int useDegree = degree >= usePeriod ? 2 : degree;

        int len = source.Length;
        double[]? weightsArray = usePeriod > 256 ? ArrayPool<double>.Shared.Rent(usePeriod) : null;
        Span<double> weights = usePeriod <= 256
            ? stackalloc double[usePeriod]
            : weightsArray!.AsSpan(0, usePeriod);

        double[]? ringArray = usePeriod > 256 ? ArrayPool<double>.Shared.Rent(usePeriod) : null;
        Span<double> ring = usePeriod <= 256
            ? stackalloc double[usePeriod]
            : ringArray!.AsSpan(0, usePeriod);

        ComputeWeights(weights, usePeriod, useDegree, out double invWeightSum);

        int ringIdx = 0;
        int count = 0;
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
                else
                {
                    val = double.NaN;
                }

                ring[ringIdx] = val;
                ringIdx++;
                if (ringIdx >= usePeriod)
                {
                    ringIdx = 0;
                }

                if (count < usePeriod)
                {
                    count++;
                }

                if (count < usePeriod)
                {
                    output[i] = CalculateWeightedSumWarmup(ring, count, useDegree, fallbackValue: val);
                    continue;
                }

                if (Math.Abs(invWeightSum) < double.Epsilon)
                {
                    output[i] = val;
                    continue;
                }

                int part1Len = usePeriod - ringIdx;
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

    public static (TSeries Results, Sgma Indicator) Calculate(TSeries source, int period = 9, int degree = 2)
    {
        var indicator = new Sgma(period, degree);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateWeightedSumFull(RingBuffer buffer, double[] weights, double invWeightSum, double fallbackValue)
    {
        if (Math.Abs(invWeightSum) < double.Epsilon)
        {
            return fallbackValue;
        }

        ReadOnlySpan<double> internalBuf = buffer.InternalBuffer;
        int head = buffer.StartIndex;
        int period = buffer.Capacity;

        int part1Len = period - head;
        double sum1 = internalBuf.Slice(head, part1Len).DotProduct(weights.AsSpan(0, part1Len));
        double sum2 = internalBuf[..head].DotProduct(weights.AsSpan(part1Len));

        return (sum1 + sum2) * invWeightSum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateWeightedSumWarmup(ReadOnlySpan<double> window, int p, int degree, double fallbackValue)
    {
        if (p <= 0)
        {
            return 0.0;
        }

        if (p == 1)
        {
            return fallbackValue;
        }

        if (degree == 2)
        {
            if (p == 5)
            {
                const double w0 = -0.0857;
                const double w1 = 0.3429;
                const double w2 = 0.4857;
                const double w3 = 0.3429;
                const double w4 = -0.0857;

                double sum = Math.FusedMultiplyAdd(window[0], w0,
                    Math.FusedMultiplyAdd(window[1], w1,
                    Math.FusedMultiplyAdd(window[2], w2,
                    Math.FusedMultiplyAdd(window[3], w3, window[4] * w4))));

                double weightSum = w0 + w1 + w2 + w3 + w4;
                return Math.Abs(weightSum) > double.Epsilon ? sum / weightSum : fallbackValue;
            }

            if (p == 7)
            {
                const double w0 = -0.0476;
                const double w1 = 0.0952;
                const double w2 = 0.2857;
                const double w3 = 0.3333;
                const double w4 = 0.2857;
                const double w5 = 0.0952;
                const double w6 = -0.0476;

                double sum = 0.0;
                sum = Math.FusedMultiplyAdd(window[0], w0, sum);
                sum = Math.FusedMultiplyAdd(window[1], w1, sum);
                sum = Math.FusedMultiplyAdd(window[2], w2, sum);
                sum = Math.FusedMultiplyAdd(window[3], w3, sum);
                sum = Math.FusedMultiplyAdd(window[4], w4, sum);
                sum = Math.FusedMultiplyAdd(window[5], w5, sum);
                sum = Math.FusedMultiplyAdd(window[6], w6, sum);

                double weightSum = w0 + w1 + w2 + w3 + w4 + w5 + w6;
                return Math.Abs(weightSum) > double.Epsilon ? sum / weightSum : fallbackValue;
            }

            if (p == 9)
            {
                const double w0 = -0.0281;
                const double w1 = 0.0337;
                const double w2 = 0.1236;
                const double w3 = 0.2247;
                const double w4 = 0.2921;
                const double w5 = 0.2247;
                const double w6 = 0.1236;
                const double w7 = 0.0337;
                const double w8 = -0.0281;

                double sum = 0.0;
                sum = Math.FusedMultiplyAdd(window[0], w0, sum);
                sum = Math.FusedMultiplyAdd(window[1], w1, sum);
                sum = Math.FusedMultiplyAdd(window[2], w2, sum);
                sum = Math.FusedMultiplyAdd(window[3], w3, sum);
                sum = Math.FusedMultiplyAdd(window[4], w4, sum);
                sum = Math.FusedMultiplyAdd(window[5], w5, sum);
                sum = Math.FusedMultiplyAdd(window[6], w6, sum);
                sum = Math.FusedMultiplyAdd(window[7], w7, sum);
                sum = Math.FusedMultiplyAdd(window[8], w8, sum);

                double weightSum = w0 + w1 + w2 + w3 + w4 + w5 + w6 + w7 + w8;
                return Math.Abs(weightSum) > double.Epsilon ? sum / weightSum : fallbackValue;
            }
        }

        double halfWindow = (p - 1) * 0.5;
        double sum2 = 0.0;
        double wSum = 0.0;

        for (int i = 0; i < p; i++)
        {
            double x = i - halfWindow;
            double normX = halfWindow > 0.0 ? x / halfWindow : 0.0;

            double w = degree switch
            {
                0 => 1.0,
                1 => 1.0 - Math.Abs(normX),
                2 => 1.0 - (normX * normX),
                3 => 1.0 - Math.Abs(normX * normX * normX),
                4 => 1.0 - (normX * normX * normX * normX),
                _ => 1.0 - (normX * normX)
            };

            sum2 = Math.FusedMultiplyAdd(window[i], w, sum2);
            wSum += w;
        }

        return Math.Abs(wSum) > 1e-15 ? sum2 / wSum : fallbackValue;
    }

    public override void Reset()
    {
        _buffer.Clear();
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        Last = default;
    }
}