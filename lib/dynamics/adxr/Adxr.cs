using System.Buffers;
using System.Runtime.CompilerServices;

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
    private readonly int _period;
    private readonly Adx _adx;
    private readonly RingBuffer _adxHistory;
    private readonly RingBuffer _p_adxHistory;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

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

        _period = period;
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

        // Calculate ADXR: average of current ADX and ADX from 'period' bars ago
        // When prevAdx is NaN (insufficient history), use currentAdx as fallback
        double adxr = double.IsNaN(prevAdx)
            ? currentAdx
            : (currentAdx + prevAdx) * 0.5;

        Last = new TValue(input.Time, adxr);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        var v = new double[len];

        Calculate(source.High.Values, source.Low.Values, source.Close.Values, _period, v);

        var tList = new List<long>(len);
        var vList = new List<double>(v);

        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(tList, vList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close, int period, Span<double> destination)
    {
        int len = high.Length;
        if (len == 0 || len != low.Length || len != close.Length || len != destination.Length)
        {
            if (destination.Length > 0)
            {
                destination.Clear();
            }
            return;
        }

        const int StackallocThreshold = 256;
        double[]? rentedAdx = null;
        scoped Span<double> adxSpan;
        if (len <= StackallocThreshold)
        {
            adxSpan = stackalloc double[len];
        }
        else
        {
            rentedAdx = ArrayPool<double>.Shared.Rent(len);
            adxSpan = rentedAdx.AsSpan(0, len);
        }

        try
        {
            Adx.Calculate(high, low, close, period, adxSpan);

            destination.Clear();

            int lag = period - 1;
            if (lag <= 0)
            {
                adxSpan.CopyTo(destination);
                return;
            }

            if (lag >= len)
            {
                return;
            }

            ReadOnlySpan<double> current = adxSpan[lag..];
            ReadOnlySpan<double> previous = adxSpan[..(len - lag)];
            Span<double> destTail = destination[lag..];

            SimdExtensions.Add(current, previous, destTail);
            SimdExtensions.Scale(destTail, 0.5, destTail);
        }
        finally
        {
            if (rentedAdx != null)
                ArrayPool<double>.Shared.Return(rentedAdx);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSeries Batch(TBarSeries source, int period)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        var v = new double[len];

        Calculate(source.High.Values, source.Low.Values, source.Close.Values, period, v);

        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        return new TSeries(tList, [.. v]);
    }
}