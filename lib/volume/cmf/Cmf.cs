using System.Runtime.CompilerServices;
using System.Numerics;

namespace QuanTAlib;

/// <summary>
/// CMF: Chaikin Money Flow
/// </summary>
/// <remarks>
/// Chaikin Money Flow measures buying and selling pressure over a specified period.
/// It uses the Money Flow Multiplier and Volume to determine if a security is being
/// accumulated (bought) or distributed (sold).
///
/// Calculation:
/// 1. Money Flow Multiplier = [(Close - Low) - (High - Close)] / (High - Low)
/// 2. Money Flow Volume = Money Flow Multiplier × Volume
/// 3. CMF = Sum(Money Flow Volume, period) / Sum(Volume, period)
///
/// CMF oscillates between -1 and +1:
/// - Positive values indicate buying pressure (accumulation)
/// - Negative values indicate selling pressure (distribution)
///
/// Sources:
/// https://www.investopedia.com/terms/c/chaikinoscillator.asp
/// https://school.stockcharts.com/doku.php?id=technical_indicators:chaikin_money_flow_cmf
/// </remarks>
[SkipLocalsInit]
public sealed class Cmf : ITValuePublisher
{
    private readonly int _period;
    private readonly RingBuffer _mfvBuffer;
    private readonly RingBuffer _volBuffer;
    private double _sumMfv;
    private double _sumVol;
    private double _p_sumMfv;
    private double _p_sumVol;
    private int _index;
    private int _p_index;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current CMF value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed enough bars (period).
    /// </summary>
    public bool IsHot => _index >= _period;

    /// <summary>
    /// Warmup period required before the indicator is considered hot.
    /// </summary>
    public int WarmupPeriod => _period;

    /// <summary>
    /// Creates a new CMF indicator.
    /// </summary>
    /// <param name="period">Lookback period (default: 20)</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Cmf(int period = 20)
    {
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        _period = period;
        _mfvBuffer = new RingBuffer(period);
        _volBuffer = new RingBuffer(period);
        Name = $"CMF({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _mfvBuffer.Clear();
        _volBuffer.Clear();
        _sumMfv = 0;
        _sumVol = 0;
        _p_sumMfv = 0;
        _p_sumVol = 0;
        _index = 0;
        _p_index = 0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_sumMfv = _sumMfv;
            _p_sumVol = _sumVol;
            _p_index = _index;
            _mfvBuffer.Snapshot();
            _volBuffer.Snapshot();
        }
        else
        {
            _sumMfv = _p_sumMfv;
            _sumVol = _p_sumVol;
            _index = _p_index;
            _mfvBuffer.Restore();
            _volBuffer.Restore();
        }

        double highLowRange = input.High - input.Low;
        double mfm = 0;

        if (highLowRange > double.Epsilon)
        {
            mfm = (input.Close - input.Low - (input.High - input.Close)) / highLowRange;
        }

        double mfv = mfm * input.Volume;
        double vol = input.Volume;

        // Update rolling sums
        if (_mfvBuffer.IsFull)
        {
            _sumMfv -= _mfvBuffer.Oldest;
            _sumVol -= _volBuffer.Oldest;
        }

        _mfvBuffer.Add(mfv);
        _volBuffer.Add(vol);
        _sumMfv += mfv;
        _sumVol += vol;

        if (isNew)
        {
            _index++;
        }

        // Calculate CMF
        double cmfValue = _sumVol > double.Epsilon ? _sumMfv / _sumVol : 0;

        Last = new TValue(input.Time, cmfValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates CMF with a TValue input.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// CMF requires OHLCV bar data to calculate the Money Flow Multiplier and Volume.
    /// Use Update(TBar) instead.
    /// </exception>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue input, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException(
            "CMF requires OHLCV bar data to calculate the Money Flow Multiplier and Volume. " +
            "Use Update(TBar) instead.");
    }

    public TSeries Update(TBarSeries source)
    {
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

    public static TSeries Calculate(TBarSeries source, int period = 20)
    {
        if (source.Count == 0) return [];

        var t = source.Open.Times.ToArray();
        var v = new double[source.Count];

        Calculate(source.High.Values, source.Low.Values, source.Close.Values, source.Volume.Values, v, period);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output, int period = 20)
    {
        if (high.Length != low.Length)
            throw new ArgumentException("High and Low spans must be of the same length", nameof(low));
        if (high.Length != close.Length)
            throw new ArgumentException("High and Close spans must be of the same length", nameof(close));
        if (high.Length != volume.Length)
            throw new ArgumentException("High and Volume spans must be of the same length", nameof(volume));
        if (high.Length != output.Length)
            throw new ArgumentException("Output span must be of the same length as input", nameof(output));
        if (period < 1)
            throw new ArgumentException("Period must be >= 1", nameof(period));

        int len = high.Length;

        // First, compute MFV for each bar
        Span<double> mfv = len <= 512 ? stackalloc double[len] : new double[len];

        int i = 0;
        if (Vector.IsHardwareAccelerated && len >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var epsilon = new Vector<double>(double.Epsilon);

            for (; i <= len - vectorSize; i += vectorSize)
            {
                var h = new Vector<double>(high.Slice(i, vectorSize));
                var l = new Vector<double>(low.Slice(i, vectorSize));
                var c = new Vector<double>(close.Slice(i, vectorSize));
                var vol = new Vector<double>(volume.Slice(i, vectorSize));

                var hl = h - l;
                var num = c - l - (h - c);

                var mask = Vector.GreaterThan(hl, epsilon);
                var safeHl = Vector.ConditionalSelect(mask, hl, Vector<double>.One);
                var mfm = num / safeHl;
                mfm = Vector.ConditionalSelect(mask, mfm, Vector<double>.Zero);

                var result = mfm * vol;
                result.CopyTo(mfv.Slice(i, vectorSize));
            }
        }

        for (; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];
            double vol = volume[i];

            double hl = h - l;
            double mfm = 0;
            if (hl > double.Epsilon)
            {
                mfm = (c - l - (h - c)) / hl;
            }
            mfv[i] = mfm * vol;
        }

        // Now compute CMF using rolling sums
        double sumMfv = 0;
        double sumVol = 0;

        for (i = 0; i < len; i++)
        {
            sumMfv += mfv[i];
            sumVol += volume[i];

            if (i >= period)
            {
                sumMfv -= mfv[i - period];
                sumVol -= volume[i - period];
            }

            output[i] = sumVol > double.Epsilon ? sumMfv / sumVol : 0;
        }
    }
}
