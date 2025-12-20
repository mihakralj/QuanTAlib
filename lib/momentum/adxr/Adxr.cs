using System.Runtime.CompilerServices;
using QuanTAlib;

namespace QuanTAlib;

/// <summary>
/// ADXR: Average Directional Movement Rating
/// </summary>
/// <remarks>
/// ADXR quantifies the change in momentum of the ADX. It is calculated by averaging
/// the current ADX value and the ADX value from 'Period' bars ago.
///
/// Calculation:
/// ADXR = (ADX + ADX[Period]) / 2
///
/// Sources:
/// https://www.investopedia.com/terms/a/adxr.asp
/// "New Concepts in Technical Trading Systems" by J. Welles Wilder
/// </remarks>
[SkipLocalsInit]
public sealed class Adxr : ITValuePublisher
{
    private readonly Adx _adx;
    private readonly RingBuffer _adxHistory;
    private readonly RingBuffer _p_adxHistory;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Current ADXR value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the ADXR has warmed up and is providing valid results.
    /// </summary>
    public bool IsHot => _adx.IsHot && _adxHistory.IsFull;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates ADXR with specified period.
    /// </summary>
    /// <param name="period">Period for ADXR calculation (must be > 0)</param>
    public Adxr(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        Name = $"Adxr({period})";
        _adx = new Adx(period);

        // We need the ADX value from 'period' bars ago.
        // TA-Lib uses (Period-1) lag for ADXR.
        _adxHistory = new RingBuffer(period - 1);
        _p_adxHistory = new RingBuffer(period - 1);
        
        // ADXR needs valid ADX from 'period' bars ago.
        // ADX takes 2*period to warm up.
        // So ADXR takes 2*period + period - 1 to warm up.
        WarmupPeriod = _adx.WarmupPeriod + period - 1; 
    }

    /// <summary>
    /// Resets the ADXR state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _adx.Reset();
        _adxHistory.Clear();
        _p_adxHistory.Clear();
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        // Update ADX first
        TValue adxResult = _adx.Update(input, isNew);
        double currentAdx = adxResult.Value;

        if (isNew)
        {
            _p_adxHistory.CopyFrom(_adxHistory);
        }
        else
        {
            _adxHistory.CopyFrom(_p_adxHistory);
        }

        double prevAdx = double.NaN;
        if (_adxHistory.IsFull)
        {
            prevAdx = _adxHistory.Oldest;
        }

        _adxHistory.Add(currentAdx);

        double adxr = 0;
        // We calculate ADXR even if not fully hot, as long as we have history
        if (!double.IsNaN(prevAdx))
        {
            adxr = (currentAdx + prevAdx) / 2.0;
        }
        else
        {
            // Fallback if we don't have enough history yet?
            // Usually ADXR is just ADX or 0 until we have history.
            // TA-Lib returns 0 until valid.
            adxr = (currentAdx + (double.IsNaN(prevAdx) ? currentAdx : prevAdx)) / 2.0;
            // Actually if prevAdx is NaN, we can't really calculate ADXR properly.
            // But to avoid returning 0 when ADX is valid but history isn't full (which is rare given ADX warmup is longer),
            // we might just return 0 or currentAdx.
            // Given ADX warmup is 2*Period, and buffer fills in Period,
            // _adxHistory will be full long before ADX is valid.
            // So prevAdx will be 0 (from cold ADX) rather than NaN, once we pass Period bars.
            // So this branch is only for the very first 'Period' bars.
            // In that case ADX is 0, so ADXR is 0.
        }

        Last = new TValue(input.Time, adxr);
        Pub?.Invoke(Last);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    public static TSeries Batch(TBarSeries source, int period)
    {
        var adxr = new Adxr(period);
        return adxr.Update(source);
    }
}
