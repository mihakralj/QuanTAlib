// Relative Volatility Index (RVI) Indicator — Revised (1995) version
// Averages original RVI computed on High and Low series separately

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RVI: Relative Volatility Index (Revised)
/// Computes original RVI on the High series and on the Low series, then averages.
/// Each channel classifies stddev direction based on its own price change.
/// </summary>
/// <remarks>
/// <b>Calculation steps (per channel — High and Low independently):</b>
/// <list type="number">
/// <item>Calculate population standard deviation over stdevLength</item>
/// <item>Classify by price change: if up, upStd = stddev; if down, downStd = stddev</item>
/// <item>Smooth upStd and downStd with RMA (Wilder's smoothing with bias correction)</item>
/// <item>channelRVI = 100 × avgUpStd / (avgUpStd + avgDownStd)</item>
/// </list>
/// <b>Final:</b> RVI = (RVI_high + RVI_low) / 2
///
/// <b>Sources:</b>
/// Donald Dorsey (1993, original; 1995, revised). Technical Analysis of Stocks &amp; Commodities.
/// FM Labs: https://www.fmlabs.com/reference/RVI.htm
/// </remarks>
[SkipLocalsInit]
public sealed class Rvi : AbstractBase
{
    private const double Epsilon = 1e-10;

    private readonly int _stdevLength;
    private readonly int _rmaLength;
    private readonly double _alpha;
    private readonly RingBuffer _hiBuf;
    private readonly RingBuffer _loBuf;

    [StructLayout(LayoutKind.Auto)]
    private record struct ChState(
        double PrevPrice,
        double Sum,
        double SumSq,
        double RawRmaUp,
        double EUp,
        double RawRmaDown,
        double EDown,
        int FillCount
    );

    private ChState _hi, _phi;
    private ChState _lo, _plo;
    private double _lastValue, _pLastValue;

    public Rvi(int stdevLength = 10, int rmaLength = 14)
    {
        if (stdevLength < 2)
        {
            throw new ArgumentException("Standard deviation length must be at least 2", nameof(stdevLength));
        }
        if (rmaLength < 1)
        {
            throw new ArgumentException("RMA length must be at least 1", nameof(rmaLength));
        }

        _stdevLength = stdevLength;
        _rmaLength = rmaLength;
        _alpha = 1.0 / rmaLength;
        _hiBuf = new RingBuffer(stdevLength);
        _loBuf = new RingBuffer(stdevLength);
        WarmupPeriod = stdevLength + rmaLength;
        Name = $"Rvi({stdevLength},{rmaLength})";

        var init = new ChState(double.NaN, 0, 0, 0, 1.0, 0, 1.0, 0);
        _hi = _phi = init;
        _lo = _plo = init;
        _lastValue = _pLastValue = 50.0;
    }

    public Rvi(ITValuePublisher source, int stdevLength = 10, int rmaLength = 14)
        : this(stdevLength, rmaLength)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    public override bool IsHot => _hi.FillCount >= _stdevLength;

    public int StdevLength => _stdevLength;
    public int RmaLength => _rmaLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, input.Value, isNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.High, bar.Low, isNew);
    }

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

        Span<double> highs = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> lows = len <= 128 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            highs[i] = source[i].High;
            lows[i] = source[i].Low;
            tSpan[i] = source[i].Time;
        }

        BatchDual(highs, lows, vSpan, _stdevLength, _rmaLength);

        // Sync internal state
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
    }

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

        // Single-price series: same value to both channels
        Batch(source.Values, vSpan, _stdevLength, _rmaLength);
        source.Times.CopyTo(tSpan);

        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double hiPrice, double loPrice, bool isNew)
    {
        if (isNew)
        {
            _phi = _hi;
            _plo = _lo;
            _pLastValue = _lastValue;
            _hiBuf.Snapshot();
            _loBuf.Snapshot();
        }
        else
        {
            _hi = _phi;
            _lo = _plo;
            _lastValue = _pLastValue;
            _hiBuf.Restore();
            _loBuf.Restore();
        }

        // Handle non-finite
        if (!double.IsFinite(hiPrice) || !double.IsFinite(loPrice))
        {
            Last = new TValue(timeTicks, _lastValue);
            PubEvent(Last, isNew);
            return Last;
        }

        double rviHi = UpdateChannel(ref _hi, _hiBuf, hiPrice);
        double rviLo = UpdateChannel(ref _lo, _loBuf, loPrice);
        double rviValue = (rviHi + rviLo) * 0.5;

        if (!double.IsFinite(rviValue))
        {
            rviValue = _lastValue;
        }
        else
        {
            _lastValue = rviValue;
        }

        Last = new TValue(timeTicks, rviValue);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double UpdateChannel(ref ChState s, RingBuffer buf, double price)
    {
        if (double.IsNaN(s.PrevPrice))
        {
            buf.Add(price);
            s = s with
            {
                PrevPrice = price,
                Sum = price,
                SumSq = price * price,
                FillCount = 1
            };
            return 50.0;
        }

        double priceChange = price - s.PrevPrice;

        double oldSum = s.Sum;
        double oldSumSq = s.SumSq;
        int oldCount = s.FillCount;

        if (buf.Count == _stdevLength)
        {
            double oldest = buf[0];
            oldSum -= oldest;
            oldSumSq -= oldest * oldest;
            oldCount--;
        }

        buf.Add(price);
        double newSum = oldSum + price;
        double newSumSq = oldSumSq + (price * price);
        int newCount = oldCount + 1;

        double currentStdDev = 0.0;
        if (newCount > 1)
        {
            double mean = newSum / newCount;
            double variance = (newSumSq / newCount) - (mean * mean);
            variance = Math.Max(0.0, variance);
            currentStdDev = Math.Sqrt(variance);
        }

        double upStdVal = 0.0;
        double downStdVal = 0.0;
        if (priceChange > 0)
        {
            upStdVal = currentStdDev;
        }
        else if (priceChange < 0)
        {
            downStdVal = currentStdDev;
        }

        double rawRmaUp = Math.FusedMultiplyAdd(s.RawRmaUp, _rmaLength - 1, upStdVal) / _rmaLength;
        double eUp = (1 - _alpha) * s.EUp;
        double avgUpStd = eUp > Epsilon ? rawRmaUp / (1.0 - eUp) : rawRmaUp;

        double rawRmaDown = Math.FusedMultiplyAdd(s.RawRmaDown, _rmaLength - 1, downStdVal) / _rmaLength;
        double eDown = (1 - _alpha) * s.EDown;
        double avgDownStd = eDown > Epsilon ? rawRmaDown / (1.0 - eDown) : rawRmaDown;

        double sumAvgStd = avgUpStd + avgDownStd;
        double rvi = sumAvgStd > Epsilon ? (100.0 * avgUpStd / sumAvgStd) : 50.0;

        s = new ChState(price, newSum, newSumSq, rawRmaUp, eUp, rawRmaDown, eDown, newCount);
        return rvi;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    public override void Reset()
    {
        var init = new ChState(double.NaN, 0, 0, 0, 1.0, 0, 1.0, 0);
        _hi = _phi = init;
        _lo = _plo = init;
        _lastValue = _pLastValue = 50.0;
        _hiBuf.Clear();
        _loBuf.Clear();
        Last = default;
    }

    // --- Static Batch methods ---

    /// <summary>
    /// Batch RVI for a single-price series (same value to both channels → original behavior).
    /// </summary>
    public static TSeries Batch(TSeries source, int stdevLength = 10, int rmaLength = 14)
    {
        if (stdevLength < 2)
        {
            throw new ArgumentException("Standard deviation length must be at least 2", nameof(stdevLength));
        }
        if (rmaLength < 1)
        {
            throw new ArgumentException("RMA length must be at least 1", nameof(rmaLength));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.Values, CollectionsMarshal.AsSpan(v), stdevLength, rmaLength);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch RVI for a bar series (revised: high+low average).
    /// </summary>
    public static TSeries Batch(TBarSeries source, int stdevLength = 10, int rmaLength = 14)
    {
        var rvi = new Rvi(stdevLength, rmaLength);
        return rvi.Update(source);
    }

    /// <summary>
    /// Span-based batch for single-price series. Same price to both channels → original behavior.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> prices,
        Span<double> output,
        int stdevLength = 10,
        int rmaLength = 14)
    {
        if (stdevLength < 2)
        {
            throw new ArgumentException("Standard deviation length must be at least 2", nameof(stdevLength));
        }
        if (rmaLength < 1)
        {
            throw new ArgumentException("RMA length must be at least 1", nameof(rmaLength));
        }
        if (output.Length < prices.Length)
        {
            throw new ArgumentException("Output span must be at least as long as prices span", nameof(output));
        }

        // Single-price: feed same data to both channels, average = original
        BatchDual(prices, prices, output, stdevLength, rmaLength);
    }

    /// <summary>
    /// Span-based batch for dual-channel (high + low) revised RVI.
    /// </summary>
    public static void BatchDual(
        ReadOnlySpan<double> highs,
        ReadOnlySpan<double> lows,
        Span<double> output,
        int stdevLength = 10,
        int rmaLength = 14)
    {
        if (stdevLength < 2)
        {
            throw new ArgumentException("Standard deviation length must be at least 2", nameof(stdevLength));
        }
        if (rmaLength < 1)
        {
            throw new ArgumentException("RMA length must be at least 1", nameof(rmaLength));
        }

        int len = highs.Length;
        if (len == 0)
        {
            return;
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input span", nameof(output));
        }

        // Allocate temp buffers for each channel's RVI output
        Span<double> rviHi = len <= 256 ? stackalloc double[len] : new double[len];
        Span<double> rviLo = len <= 256 ? stackalloc double[len] : new double[len];

        BatchSingleChannel(highs, rviHi, stdevLength, rmaLength);
        BatchSingleChannel(lows, rviLo, stdevLength, rmaLength);

        // Average
        for (int i = 0; i < len; i++)
        {
            output[i] = (rviHi[i] + rviLo[i]) * 0.5;
        }
    }

    /// <summary>
    /// Computes original (single-channel) RVI for one price series.
    /// </summary>
    private static void BatchSingleChannel(
        ReadOnlySpan<double> prices,
        Span<double> output,
        int stdevLength,
        int rmaLength)
    {
        int len = prices.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 1.0 / rmaLength;

        Span<double> priceBuffer = stdevLength <= 256 ? stackalloc double[stdevLength] : new double[stdevLength];
        int head = 0;
        int count = 0;
        double sum = 0;
        double sumSq = 0;
        double prevPrice = double.NaN;
        double lastValue = 50.0;

        double rawRmaUp = 0;
        double eUp = 1.0;
        double rawRmaDown = 0;
        double eDown = 1.0;

        for (int i = 0; i < len; i++)
        {
            double price = prices[i];

            if (double.IsNaN(prevPrice))
            {
                if (!double.IsFinite(price))
                {
                    output[i] = lastValue;
                    continue;
                }

                if (count < stdevLength)
                {
                    count++;
                }
                else
                {
                    double oldest = priceBuffer[head];
                    sum -= oldest;
                    sumSq -= oldest * oldest;
                }
                priceBuffer[head] = price;
                head = (head + 1) % stdevLength;
                sum += price;
                sumSq += price * price;

                prevPrice = price;
                output[i] = 50.0;
                continue;
            }

            if (!double.IsFinite(price))
            {
                output[i] = lastValue;
                continue;
            }

            double priceChange = price - prevPrice;
            prevPrice = price;

            if (count < stdevLength)
            {
                count++;
            }
            else
            {
                double oldest = priceBuffer[head];
                sum -= oldest;
                sumSq -= oldest * oldest;
            }
            priceBuffer[head] = price;
            head = (head + 1) % stdevLength;
            sum += price;
            sumSq += price * price;

            double currentStdDev = 0.0;
            if (count > 1)
            {
                double mean = sum / count;
                double variance = (sumSq / count) - (mean * mean);
                variance = Math.Max(0.0, variance);
                currentStdDev = Math.Sqrt(variance);
            }

            double upStdVal = 0.0;
            double downStdVal = 0.0;
            if (priceChange > 0)
            {
                upStdVal = currentStdDev;
            }
            else if (priceChange < 0)
            {
                downStdVal = currentStdDev;
            }

            rawRmaUp = Math.FusedMultiplyAdd(rawRmaUp, rmaLength - 1, upStdVal) / rmaLength;
            eUp = (1 - alpha) * eUp;
            double avgUpStd = eUp > Epsilon ? rawRmaUp / (1.0 - eUp) : rawRmaUp;

            rawRmaDown = Math.FusedMultiplyAdd(rawRmaDown, rmaLength - 1, downStdVal) / rmaLength;
            eDown = (1 - alpha) * eDown;
            double avgDownStd = eDown > Epsilon ? rawRmaDown / (1.0 - eDown) : rawRmaDown;

            double sumAvgStd = avgUpStd + avgDownStd;
            double rviValue = sumAvgStd > Epsilon ? (100.0 * avgUpStd / sumAvgStd) : 50.0;

            if (!double.IsFinite(rviValue))
            {
                rviValue = lastValue;
            }
            else
            {
                lastValue = rviValue;
            }

            output[i] = rviValue;
        }
    }

    public static (TSeries Results, Rvi Indicator) Calculate(TSeries source, int stdevLength = 10, int rmaLength = 14)
    {
        var indicator = new Rvi(stdevLength, rmaLength);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
