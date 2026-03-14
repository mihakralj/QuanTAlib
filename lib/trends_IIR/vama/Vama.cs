using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VAMA: Volatility Adjusted Moving Average
/// </summary>
/// <remarks>
/// Adaptive MA that adjusts period based on long/short ATR volatility ratio.
/// Higher volatility → shorter period (faster); lower volatility → longer period (smoother).
///
/// Calculation: <c>length = baseLength × (LongATR/ShortATR)</c>, clamped to [min, max].
/// </remarks>
/// <seealso href="Vama.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Vama : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct RmaState(double Ema, double E, bool IsCompensated);

    [StructLayout(LayoutKind.Auto)]
    private record struct VamaState(
        RmaState ShortAtr,
        RmaState LongAtr,
        double PrevClose,
        int BufferHead,
        double BufferSum,
        int ValidCount,
        bool IsInitialized)
    {
        public static VamaState New() => new()
        {
            ShortAtr = new RmaState(Ema: 0, E: 1.0, IsCompensated: false),
            LongAtr = new RmaState(Ema: 0, E: 1.0, IsCompensated: false),
            PrevClose = double.NaN,
            BufferHead = 0,
            BufferSum = 0,
            ValidCount = 0,
            IsInitialized = false
        };
    }

    private readonly int _baseLength;
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly double _shortAlpha;
    private readonly double _longAlpha;
    private readonly double _shortDecay;
    private readonly double _longDecay;

    private VamaState _state;
    private VamaState _p_state;
    private readonly double[] _buffer;
    private readonly double[] _p_buffer;
    private double _lastValidValue;
    private double _p_lastValidValue;

    private const double EPSILON = 1e-10;

    /// <summary>
    /// Creates VAMA with specified parameters.
    /// </summary>
    /// <param name="baseLength">Base period for the moving average (default: 20)</param>
    /// <param name="shortAtrPeriod">Short-term ATR period for current volatility (default: 10)</param>
    /// <param name="longAtrPeriod">Long-term ATR period for reference volatility (default: 50)</param>
    /// <param name="minLength">Minimum allowed adjusted length (default: 5)</param>
    /// <param name="maxLength">Maximum allowed adjusted length (default: 100)</param>
    public Vama(int baseLength = 20, int shortAtrPeriod = 10, int longAtrPeriod = 50, int minLength = 5, int maxLength = 100)
    {
        if (baseLength <= 0)
        {
            throw new ArgumentException("Base length must be greater than 0", nameof(baseLength));
        }

        if (shortAtrPeriod <= 0)
        {
            throw new ArgumentException("Short ATR period must be greater than 0", nameof(shortAtrPeriod));
        }

        if (longAtrPeriod <= 0)
        {
            throw new ArgumentException("Long ATR period must be greater than 0", nameof(longAtrPeriod));
        }

        if (minLength <= 0)
        {
            throw new ArgumentException("Min length must be greater than 0", nameof(minLength));
        }

        if (maxLength <= 0)
        {
            throw new ArgumentException("Max length must be greater than 0", nameof(maxLength));
        }

        if (minLength > maxLength)
        {
            throw new ArgumentException("Min length must be less than or equal to max length", nameof(minLength));
        }

        _baseLength = baseLength;
        _minLength = minLength;
        _maxLength = maxLength;

        _shortAlpha = 1.0 / shortAtrPeriod;
        _longAlpha = 1.0 / longAtrPeriod;
        _shortDecay = 1.0 - _shortAlpha;
        _longDecay = 1.0 - _longAlpha;

        _buffer = new double[maxLength];
        _p_buffer = new double[maxLength];
        Array.Fill(_buffer, double.NaN);
        Array.Fill(_p_buffer, double.NaN);

        _state = VamaState.New();
        _p_state = _state;

        Name = $"Vama({baseLength},{shortAtrPeriod},{longAtrPeriod})";
        WarmupPeriod = Math.Max(longAtrPeriod, maxLength);
    }

    /// <summary>
    /// Creates VAMA with specified source and parameters.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Vama(ITValuePublisher source, int baseLength = 20, int shortAtrPeriod = 10, int longAtrPeriod = 50, int minLength = 5, int maxLength = 100)
        : this(baseLength, shortAtrPeriod, longAtrPeriod, minLength, maxLength)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// True if the VAMA has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _state.ValidCount >= _minLength && _state.IsInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Updates VAMA with a TBar input (uses Close for smoothing, OHLC for True Range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_buffer, _p_buffer, _maxLength);
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_buffer, _buffer, _maxLength);
            _lastValidValue = _p_lastValidValue;
        }

        // Calculate True Range
        double trueRange;
        if (!_state.IsInitialized || double.IsNaN(_state.PrevClose))
        {
            trueRange = input.High - input.Low;
        }
        else
        {
            double hl = input.High - input.Low;
            double hpc = Math.Abs(input.High - _state.PrevClose);
            double lpc = Math.Abs(input.Low - _state.PrevClose);
            trueRange = Math.Max(hl, Math.Max(hpc, lpc));
        }

        // Update ATRs with bias compensation (RMA style)
        var shortAtr = _state.ShortAtr;
        var longAtr = _state.LongAtr;

        shortAtr.Ema = Math.FusedMultiplyAdd(shortAtr.Ema, _shortDecay, _shortAlpha * trueRange);
        shortAtr.E *= _shortDecay;
        if (shortAtr.E <= EPSILON)
        {
            shortAtr.IsCompensated = true;
        }

        longAtr.Ema = Math.FusedMultiplyAdd(longAtr.Ema, _longDecay, _longAlpha * trueRange);
        longAtr.E *= _longDecay;
        if (longAtr.E <= EPSILON)
        {
            longAtr.IsCompensated = true;
        }

        // Compensated ATR values
        double shortAtrValue = shortAtr.IsCompensated ? shortAtr.Ema : shortAtr.Ema / (1.0 - shortAtr.E);
        double longAtrValue = longAtr.IsCompensated ? longAtr.Ema : longAtr.Ema / (1.0 - longAtr.E);

        // Calculate volatility ratio
        double volatilityRatio = shortAtrValue > EPSILON ? longAtrValue / shortAtrValue : 1.0;

        // Calculate adjusted length
        double calcLength = _baseLength * volatilityRatio;
        int adjustedLength = (int)Math.Max(_minLength, Math.Min(_maxLength, calcLength));

        // Update circular buffer with source value
        double sourceValue = input.Close;
        if (!double.IsFinite(sourceValue))
        {
            sourceValue = _lastValidValue;
        }
        else
        {
            _lastValidValue = sourceValue;
        }

        // Remove oldest value from sum if it was valid
        double oldest = _buffer[_state.BufferHead];
        int validCount = _state.ValidCount;
        double bufferSum = _state.BufferSum;

        if (double.IsFinite(oldest))
        {
            bufferSum -= oldest;
            validCount--;
        }

        // Add new value
        if (double.IsFinite(sourceValue))
        {
            bufferSum += sourceValue;
            validCount++;
        }

        _buffer[_state.BufferHead] = sourceValue;
        int newHead = (_state.BufferHead + 1) % _maxLength;

        // Calculate SMA over adjusted_length most recent values
        double result = 0;
        int actualCount = Math.Min(validCount, adjustedLength);
        if (actualCount > 0)
        {
            double partialSum = 0.0;
            int partialCount = 0;
            for (int i = 0; i < actualCount; i++)
            {
                int idx = (newHead - 1 - i + _maxLength) % _maxLength;
                double val = _buffer[idx];
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

        // Update state
        _state = new VamaState(
            shortAtr,
            longAtr,
            input.Close,
            newHead,
            bufferSum,
            validCount,
            IsInitialized: true);

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates VAMA with a TValue input.
    /// Note: VAMA ideally needs OHLC data for True Range calculation.
    /// When only a single value is provided, TR is approximated as 0 (no volatility),
    /// which means the adjusted length stays at base_length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Create a synthetic bar with O=H=L=C for single-value input
        // This results in TR = 0, so volatility ratio stays at 1
        var syntheticBar = new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0);
        return Update(syntheticBar, isNew);
    }

    /// <summary>
    /// Updates VAMA with a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

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
            var result = Update(bar, isNew: true);
            tSpan[i] = bar.Time;
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates VAMA with a TSeries (single values).
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

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
            var result = Update(new TValue(sourceTimes[i], sourceValues[i]), isNew: true);
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
        {
            Update(new TValue(DateTime.MinValue, val), isNew: true);
        }
    }

    /// <summary>
    /// Calculates VAMA for the entire bar series using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int baseLength = 20, int shortAtrPeriod = 10, int longAtrPeriod = 50, int minLength = 5, int maxLength = 100)
    {
        var vama = new Vama(baseLength, shortAtrPeriod, longAtrPeriod, minLength, maxLength);
        return vama.Update(source);
    }

    /// <summary>
    /// Calculates VAMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int baseLength = 20, int shortAtrPeriod = 10, int longAtrPeriod = 50, int minLength = 5, int maxLength = 100)
    {
        var vama = new Vama(baseLength, shortAtrPeriod, longAtrPeriod, minLength, maxLength);
        return vama.Update(source);
    }

    public static (TSeries Results, Vama Indicator) Calculate(TBarSeries source, int baseLength = 20, int shortAtrPeriod = 10, int longAtrPeriod = 50, int minLength = 5, int maxLength = 100)
    {
        var indicator = new Vama(baseLength, shortAtrPeriod, longAtrPeriod, minLength, maxLength);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Resets the VAMA state.
    /// </summary>
    public override void Reset()
    {
        _state = VamaState.New();
        _p_state = _state;
        Array.Fill(_buffer, double.NaN);
        Array.Fill(_p_buffer, double.NaN);
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}
