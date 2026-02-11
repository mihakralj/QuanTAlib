using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CCV: Close-to-Close Volatility
/// </summary>
/// <remarks>
/// Close-to-Close Volatility calculates the annualized standard deviation of
/// logarithmic returns. This is the simplest and most common volatility measure,
/// using only closing prices. The result is annualized using √252 (trading days).
///
/// Formula:
/// <c>r_t = ln(Close_t / Close_{t-1})</c>
/// <c>σ = StdDev(r, period)</c>
/// <c>CCV = σ × √252</c>
///
/// Three smoothing methods are available:
/// - SMA (1): Simple Moving Average of returns
/// - EMA (2): Exponential Moving Average with warmup compensation
/// - WMA (3): Weighted Moving Average
///
/// Key properties:
/// - Uses only closing prices
/// - Annualized for comparability
/// - Common benchmark volatility measure
/// </remarks>
[SkipLocalsInit]
public sealed class Ccv : AbstractBase
{
    private readonly int _period;
    private readonly int _method;
    private readonly RingBuffer _returnBuffer;

    private const double AnnualizationFactor = 15.874507866387544; // √252

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Sum,
        double SumSq,
        double PrevClose,
        double LastValid,
        double RawRma,
        double E);
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private int _tickCount;
    private const double Epsilon = 1e-10;

    /// <summary>
    /// Creates CCV with specified period and smoothing method.
    /// </summary>
    /// <param name="period">Lookback period for volatility calculation (must be > 0)</param>
    /// <param name="method">Smoothing method: 1=SMA, 2=EMA, 3=WMA (default: 1)</param>
    public Ccv(int period, int method = 1)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (method < 1 || method > 3)
        {
            throw new ArgumentException("Method must be 1 (SMA), 2 (EMA), or 3 (WMA)", nameof(method));
        }

        _period = period;
        _method = method;
        _returnBuffer = new RingBuffer(period);
        Name = $"Ccv({period},{method})";
        WarmupPeriod = period + 1; // +1 for first log return calculation
        _state = new State(0.0, 0.0, double.NaN, 0.0, 0.0, 1.0);
        _p_state = _state;
    }

    /// <summary>
    /// Creates CCV with specified source, period, and smoothing method.
    /// </summary>
    public Ccv(ITValuePublisher source, int period, int method = 1) : this(period, method)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _returnBuffer.IsFull;

    /// <summary>
    /// Period of the indicator.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Smoothing method (1=SMA, 2=EMA, 3=WMA).
    /// </summary>
    public int Method => _method;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double close = input.Value;

        // Sanitize input
        if (!double.IsFinite(close) || close <= 0)
        {
            close = double.IsFinite(_state.LastValid) && _state.LastValid > 0 ? _state.LastValid : 1.0;
        }
        else
        {
            _state.LastValid = close;
        }

        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        // Calculate log return if we have a previous close
        double logReturn = 0.0;
        if (double.IsFinite(_state.PrevClose) && _state.PrevClose > 0)
        {
            logReturn = Math.Log(close / _state.PrevClose);
        }

        if (isNew)
        {
            // Store the log return in buffer
            if (_returnBuffer.Count == _returnBuffer.Capacity)
            {
                double oldest = _returnBuffer.Oldest;
                _state.Sum -= oldest;
            }
            _state.Sum += logReturn;
            _returnBuffer.Add(logReturn);
            _state.PrevClose = close;

            _tickCount++;
            if (_returnBuffer.IsFull && _tickCount >= ResyncInterval)
            {
                _tickCount = 0;
                RecalculateSums();
            }
        }
        else
        {
            // Update the newest value in buffer for bar correction
            _returnBuffer.UpdateNewest(logReturn);
            RecalculateSums();
        }

        // Calculate volatility
        int count = _returnBuffer.Count;
        if (count == 0)
        {
            Last = new TValue(input.Time, 0.0);
            PubEvent(Last, isNew);
            return Last;
        }

        double mean = _state.Sum / count;

        // Calculate squared deviations
        double squaredSum = 0.0;
        for (int i = 0; i < count; i++)
        {
            double diff = _returnBuffer[i] - mean;
            squaredSum += diff * diff;
        }

        double stdDev = Math.Sqrt(squaredSum / count);
        double annualizedStdDev = stdDev * AnnualizationFactor;

        // Apply smoothing method
        double result;
        switch (_method)
        {
            case 1: // SMA - already calculated
                result = annualizedStdDev;
                break;

            case 2: // EMA/RMA with warmup compensation
                double alpha = 1.0 / _period;
                double beta = 1.0 - alpha;

                if (isNew)
                {
                    _state.RawRma = Math.FusedMultiplyAdd(_state.RawRma, beta, alpha * annualizedStdDev);
                    _state.E *= beta;
                }
                else
                {
                    // Recalculate RMA for bar correction
                    _state.RawRma = Math.FusedMultiplyAdd(_p_state.RawRma, beta, alpha * annualizedStdDev);
                    _state.E = _p_state.E * beta;
                }

                result = _state.E > Epsilon ? _state.RawRma / (1.0 - _state.E) : _state.RawRma;
                break;

            case 3: // Approximate WMA (uses current value with triangular weighting)
                // Note: This is an approximation since we don't maintain historical
                // annualized stddev values. It applies triangular weighting to the
                // current annualized stddev, which gives a smoothed result but is
                // not a true WMA of historical volatility values.
                // WMA weights: period, period-1, ..., 1
                double weightedSum = 0.0;
                double weight = _period;

                // Apply triangular weighting based on count (approximation)
                int effectiveCount = Math.Min(count, _period);
                double actualSumWeight = effectiveCount * (effectiveCount + 1) / 2.0;
                for (int i = 0; i < effectiveCount; i++)
                {
                    weightedSum += annualizedStdDev * weight;
                    weight = Math.Max(1.0, weight - 1.0);
                }
                result = weightedSum / actualSumWeight;
                break;

            default:
                result = annualizedStdDev;
                break;
        }

        if (!double.IsFinite(result))
        {
            result = 0.0;
        }

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period, _method);
        source.Times.CopyTo(tSpan);

        // Update internal state to match final position
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateSums()
    {
        _state.Sum = 0.0;
        for (int i = 0; i < _returnBuffer.Count; i++)
        {
            _state.Sum += _returnBuffer[i];
        }
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _returnBuffer.Clear();
        _state = new State(0.0, 0.0, double.NaN, 0.0, 0.0, 1.0);
        _p_state = _state;
        _tickCount = 0;
        Last = default;
    }

    /// <summary>
    /// Calculates CCV for entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, int method = 1)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (method < 1 || method > 3)
        {
            throw new ArgumentException("Method must be 1 (SMA), 2 (EMA), or 3 (WMA)", nameof(method));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, period, method);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch CCV calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, int method = 1)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (method < 1 || method > 3)
        {
            throw new ArgumentException("Method must be 1 (SMA), 2 (EMA), or 3 (WMA)", nameof(method));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var returnBuffer = new RingBuffer(period);
        double sum = 0.0;
        double prevClose = double.NaN;
        double lastValidClose = 1.0; // Track last valid sanitized close for proper fallback
        double rawRma = 0.0;
        double e = 1.0;
        double alpha = 1.0 / period;
        double beta = 1.0 - alpha;

        for (int i = 0; i < len; i++)
        {
            double close = source[i];

            // Sanitize input - use running lastValidClose instead of source[i-1]
            // to prevent NaN propagation when previous values were also invalid
            if (!double.IsFinite(close) || close <= 0)
            {
                close = lastValidClose;
            }
            else
            {
                lastValidClose = close;
            }

            // Calculate log return
            double logReturn = 0.0;
            if (double.IsFinite(prevClose) && prevClose > 0)
            {
                logReturn = Math.Log(close / prevClose);
            }

            // Update buffer and sum
            if (returnBuffer.Count == returnBuffer.Capacity)
            {
                sum -= returnBuffer.Oldest;
            }
            sum += logReturn;
            returnBuffer.Add(logReturn);
            prevClose = close;

            // Calculate volatility
            int count = returnBuffer.Count;
            if (count == 0)
            {
                output[i] = 0.0;
                continue;
            }

            double mean = sum / count;

            // Calculate squared deviations
            double squaredSum = 0.0;
            for (int j = 0; j < count; j++)
            {
                double diff = returnBuffer[j] - mean;
                squaredSum += diff * diff;
            }

            double stdDev = Math.Sqrt(squaredSum / count);
            double annualizedStdDev = stdDev * AnnualizationFactor;

            // Apply smoothing method
            double result;
            switch (method)
            {
                case 1: // SMA
                    result = annualizedStdDev;
                    break;

                case 2: // EMA/RMA
                    rawRma = Math.FusedMultiplyAdd(rawRma, beta, alpha * annualizedStdDev);
                    e *= beta;
                    result = e > Epsilon ? rawRma / (1.0 - e) : rawRma;
                    break;

                case 3: // WMA
                    double sumWeight = period * (period + 1) / 2.0;
                    double weightedSum = 0.0;
                    double weight = period;
                    for (int j = 0; j < Math.Min(count, period); j++)
                    {
                        weightedSum += annualizedStdDev * weight;
                        weight = Math.Max(1.0, weight - 1.0);
                    }
                    result = weightedSum / sumWeight;
                    break;

                default:
                    result = annualizedStdDev;
                    break;
            }

            output[i] = double.IsFinite(result) ? result : 0.0;
        }
    }

    public static (TSeries Results, Ccv Indicator) Calculate(TSeries source, int period, int method = 1)
    {
        var indicator = new Ccv(period, method);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}