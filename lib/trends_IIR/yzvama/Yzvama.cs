using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// YZVAMA: Yang-Zhang Volatility Adjusted Moving Average
/// </summary>
/// <remarks>
/// YZVAMA adjusts the SMA length based on the percentile rank of short-term
/// Yang-Zhang volatility (YZV) observed over a rolling lookback window.
///
/// Calculation (per bar):
/// 1) Compute Yang-Zhang daily variance proxy from OHLC (log returns).
/// 2) Smooth variance with bias-compensated RMA for short and long periods (sqrt -> volatility).
/// 3) Compute percentile rank of current short YZV within the lookback window.
/// 4) Map percentile to adjusted SMA length: higher volatility -> shorter length.
/// 5) Output SMA(source, adjusted_length) over a circular buffer.
/// </remarks>
[SkipLocalsInit]
public sealed class Yzvama : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct RmaState(double Ema, double E, bool IsCompensated);

    [StructLayout(LayoutKind.Auto)]
    private record struct YzvamaState(
        RmaState ShortVar,
        RmaState LongVar,
        double PrevClose,
        int SourceHead,
        double SourceSum,
        int SourceValidCount,
        int YzvHead,
        bool IsInitialized)
    {
        public static YzvamaState New() => new()
        {
            ShortVar = new RmaState(0, 1.0, false),
            LongVar = new RmaState(0, 1.0, false),
            PrevClose = double.NaN,
            SourceHead = 0,
            SourceSum = 0,
            SourceValidCount = 0,
            YzvHead = 0,
            IsInitialized = false
        };
    }

    private readonly int _percentileLookback;
    private readonly int _minLength;
    private readonly int _maxLength;

    private readonly double _shortAlpha;
    private readonly double _longAlpha;
    private readonly double _shortDecay;
    private readonly double _longDecay;
    private readonly double _kShort;
    private readonly double _kLong;

    private YzvamaState _state;
    private YzvamaState _p_state;

    private readonly double[] _sourceBuffer;
    private readonly double[] _p_sourceBuffer;
    private readonly double[] _yzvBuffer;
    private readonly double[] _p_yzvBuffer;
    private readonly double[] _yzvWork;

    private double _lastValidSource;
    private double _p_lastValidSource;

    private const double EPSILON = 1e-10;

    /// <summary>
    /// Creates YZVAMA with specified parameters.
    /// </summary>
    /// <param name="yzvShortPeriod">Short-term YZV period for current volatility (default: 3)</param>
    /// <param name="yzvLongPeriod">Long-term YZV period for baseline volatility (default: 50)</param>
    /// <param name="percentileLookback">Lookback window for percentile calculation (default: 100)</param>
    /// <param name="minLength">Minimum allowed adjusted length (default: 5)</param>
    /// <param name="maxLength">Maximum allowed adjusted length (default: 100)</param>
    public Yzvama(int yzvShortPeriod = 3, int yzvLongPeriod = 50, int percentileLookback = 100, int minLength = 5, int maxLength = 100)
    {
        if (yzvShortPeriod <= 0)
            throw new ArgumentException("Short YZV period must be greater than 0", nameof(yzvShortPeriod));
        if (yzvLongPeriod <= 0)
            throw new ArgumentException("Long YZV period must be greater than 0", nameof(yzvLongPeriod));
        if (percentileLookback <= 0)
            throw new ArgumentException("Percentile lookback must be greater than 0", nameof(percentileLookback));
        if (minLength <= 0)
            throw new ArgumentException("Min length must be greater than 0", nameof(minLength));
        if (maxLength <= 0)
            throw new ArgumentException("Max length must be greater than 0", nameof(maxLength));
        if (minLength > maxLength)
            throw new ArgumentException("Min length must be less than or equal to max length", nameof(minLength));

        _percentileLookback = percentileLookback;
        _minLength = minLength;
        _maxLength = maxLength;

        _shortAlpha = 1.0 / yzvShortPeriod;
        _longAlpha = 1.0 / yzvLongPeriod;
        _shortDecay = 1.0 - _shortAlpha;
        _longDecay = 1.0 - _longAlpha;

        _kShort = ComputeYangZhangK(yzvShortPeriod);
        _kLong = ComputeYangZhangK(yzvLongPeriod);

        _sourceBuffer = new double[maxLength];
        _p_sourceBuffer = new double[maxLength];
        Array.Fill(_sourceBuffer, double.NaN);
        Array.Fill(_p_sourceBuffer, double.NaN);

        _yzvBuffer = new double[percentileLookback];
        _p_yzvBuffer = new double[percentileLookback];
        _yzvWork = new double[percentileLookback];
        Array.Fill(_yzvBuffer, double.NaN);
        Array.Fill(_p_yzvBuffer, double.NaN);

        _state = YzvamaState.New();
        _p_state = _state;

        Name = $"Yzvama({yzvShortPeriod},{yzvLongPeriod},{percentileLookback},{minLength},{maxLength})";
        WarmupPeriod = Math.Max(Math.Max(yzvLongPeriod, maxLength), percentileLookback);
    }

    /// <summary>
    /// Creates YZVAMA with specified source and parameters.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Yzvama(ITValuePublisher source, int yzvShortPeriod = 3, int yzvLongPeriod = 50, int percentileLookback = 100, int minLength = 5, int maxLength = 100)
        : this(yzvShortPeriod, yzvLongPeriod, percentileLookback, minLength, maxLength)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// True if the YZVAMA has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _state.SourceValidCount >= _minLength && _state.IsInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeYangZhangK(int period)
    {
        if (period <= 1)
            return 0.34 / (1.34 + 1.0);

        double ratioN = (period + 1.0) / (period - 1.0);
        return 0.34 / (1.34 + ratioN);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LowerBound(ReadOnlySpan<double> sorted, int length, double value)
    {
        int lo = 0;
        int hi = length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (sorted[mid] < value)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Updates YZVAMA with a TBar input (uses OHLC for YZV, Close as source).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, bool isNew = true) => Update(input, input.Close, isNew);

    /// <summary>
    /// Updates YZVAMA with a TBar input (uses OHLC for YZV and provided source for SMA).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, double sourceValue, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_sourceBuffer, _p_sourceBuffer, _maxLength);
            Array.Copy(_yzvBuffer, _p_yzvBuffer, _percentileLookback);
            _p_lastValidSource = _lastValidSource;
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_sourceBuffer, _sourceBuffer, _maxLength);
            Array.Copy(_p_yzvBuffer, _yzvBuffer, _percentileLookback);
            _lastValidSource = _p_lastValidSource;
        }

        // Sanitize source
        if (!double.IsFinite(sourceValue))
            sourceValue = _lastValidSource;
        else
            _lastValidSource = sourceValue;

        // Compute Yang-Zhang variance components (log returns)
        double yzvShort = double.NaN;
        bool canComputeVol = double.IsFinite(input.Open) && double.IsFinite(input.High) && double.IsFinite(input.Low) && double.IsFinite(input.Close)
            && input.Open > 0 && input.High > 0 && input.Low > 0 && input.Close > 0;

        var shortVar = _state.ShortVar;
        var longVar = _state.LongVar;
        double prevClose = _state.PrevClose;

        if (canComputeVol)
        {
            double pc = (!double.IsFinite(prevClose) || prevClose <= 0) ? input.Open : prevClose;

            if (pc > 0)
            {
                double ro = Math.Log(input.Open / pc);
                double rc = Math.Log(input.Close / input.Open);
                double rh = Math.Log(input.High / input.Open);
                double rl = Math.Log(input.Low / input.Open);

                double sOSq = ro * ro;
                double sCSq = rc * rc;
                double sRsSq = rh * (rh - rc) + rl * (rl - rc);

                double sSqDailyShort = sOSq + _kShort * sCSq + (1.0 - _kShort) * sRsSq;
                double sSqDailyLong = sOSq + _kLong * sCSq + (1.0 - _kLong) * sRsSq;

                // Update short RMA variance
                shortVar.Ema = Math.FusedMultiplyAdd(shortVar.Ema, _shortDecay, _shortAlpha * sSqDailyShort);
                shortVar.E *= _shortDecay;
                if (shortVar.E <= EPSILON) shortVar.IsCompensated = true;

                double shortVarValue = shortVar.IsCompensated ? shortVar.Ema : shortVar.Ema / (1.0 - shortVar.E);
                yzvShort = shortVarValue >= 0 ? Math.Sqrt(shortVarValue) : double.NaN;

                // Update long RMA variance (kept for parity with Pine implementation)
                longVar.Ema = Math.FusedMultiplyAdd(longVar.Ema, _longDecay, _longAlpha * sSqDailyLong);
                longVar.E *= _longDecay;
                if (longVar.E <= EPSILON) longVar.IsCompensated = true;
            }
        }

        // Update YZV percentile buffer
        int yzvHead = _state.YzvHead;
        if (double.IsFinite(yzvShort))
        {
            _yzvBuffer[yzvHead] = yzvShort;
            yzvHead = (yzvHead + 1) % _percentileLookback;
        }

        // Percentile rank of current yzvShort within lookback window
        double percentileValue = 50.0;
        int nValid = 0;
        for (int i = 0; i < _percentileLookback; i++)
        {
            double val = _yzvBuffer[i];
            if (double.IsFinite(val))
                _yzvWork[nValid++] = val;
        }

        if (nValid > 1 && double.IsFinite(yzvShort))
        {
            Array.Sort(_yzvWork, 0, nValid);
            int rankPos = LowerBound(_yzvWork, nValid, yzvShort);
            percentileValue = (rankPos / (double)(nValid - 1)) * 100.0;
        }

        double lengthRange = _maxLength - _minLength;
        double adjustedLengthF = _maxLength - (percentileValue / 100.0) * lengthRange;
        int adjustedLength = (int)Math.Max(_minLength, Math.Min(_maxLength, adjustedLengthF));

        // Update source circular buffer and rolling sum
        double oldest = _sourceBuffer[_state.SourceHead];
        int validCount = _state.SourceValidCount;
        double sourceSum = _state.SourceSum;

        if (double.IsFinite(oldest))
        {
            sourceSum -= oldest;
            validCount--;
        }

        if (double.IsFinite(sourceValue))
        {
            sourceSum += sourceValue;
            validCount++;
        }

        _sourceBuffer[_state.SourceHead] = sourceValue;
        int newHead = (_state.SourceHead + 1) % _maxLength;

        // Calculate SMA over adjustedLength most recent values
        double result;
        int actualCount = Math.Min(validCount, adjustedLength);
        if (actualCount > 0)
        {
            double partialSum = 0.0;
            int partialCount = 0;
            for (int i = 0; i < actualCount; i++)
            {
                int idx = (newHead - 1 - i + _maxLength) % _maxLength;
                double val = _sourceBuffer[idx];
                if (double.IsFinite(val))
                {
                    partialSum += val;
                    partialCount++;
                }
            }

            result = partialCount > 0 ? partialSum / partialCount : sourceValue;
        }
        else
        {
            result = sourceValue;
        }

        _state = new YzvamaState(
            shortVar,
            longVar,
            input.Close,
            newHead,
            sourceSum,
            validCount,
            yzvHead,
            true);

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates YZVAMA with a TValue input.
    /// Note: YZVAMA ideally needs OHLC data to compute Yang-Zhang volatility.
    /// When only a single value is provided, a synthetic bar is created (O=H=L=C),
    /// resulting in zero volatility and a tendency toward longer adjusted lengths.
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        var syntheticBar = new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0);
        return Update(syntheticBar, input.Value, isNew);
    }

    /// <summary>
    /// Updates YZVAMA with a TBarSeries (Close as source).
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            var result = Update(bar, true);
            tSpan[i] = bar.Time;
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates YZVAMA with a TSeries (single values).
    /// </summary>
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

        var sourceTimes = source.Times;
        var sourceValues = source.Values;

        for (int i = 0; i < len; i++)
        {
            var result = Update(new TValue(sourceTimes[i], sourceValues[i]), true);
            tSpan[i] = sourceTimes[i];
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();
        foreach (double val in source)
            Update(new TValue(DateTime.MinValue, val), true);
    }

    /// <summary>
    /// Calculates YZVAMA for the entire bar series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int yzvShortPeriod = 3, int yzvLongPeriod = 50, int percentileLookback = 100, int minLength = 5, int maxLength = 100)
    {
        var yzvama = new Yzvama(yzvShortPeriod, yzvLongPeriod, percentileLookback, minLength, maxLength);
        return yzvama.Update(source);
    }

    /// <summary>
    /// Calculates YZVAMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int yzvShortPeriod = 3, int yzvLongPeriod = 50, int percentileLookback = 100, int minLength = 5, int maxLength = 100)
    {
        var yzvama = new Yzvama(yzvShortPeriod, yzvLongPeriod, percentileLookback, minLength, maxLength);
        return yzvama.Update(source);
    }

    /// <summary>
    /// Resets the YZVAMA state.
    /// </summary>
    public override void Reset()
    {
        _state = YzvamaState.New();
        _p_state = _state;
        Array.Fill(_sourceBuffer, double.NaN);
        Array.Fill(_p_sourceBuffer, double.NaN);
        Array.Fill(_yzvBuffer, double.NaN);
        Array.Fill(_p_yzvBuffer, double.NaN);
        _lastValidSource = 0;
        _p_lastValidSource = 0;
        Last = default;
    }
}
