// Relative Volatility Index (RVI) Indicator
// Measures the direction of volatility using standard deviation and RMA smoothing

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RVI: Relative Volatility Index
/// Measures the direction of volatility by comparing upward and downward price movements
/// weighted by their standard deviations, smoothed with Wilder's RMA.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate population standard deviation of prices over stdevLength</item>
/// <item>Classify by price change: if up, upStd = stddev; if down, downStd = stddev</item>
/// <item>Smooth upStd and downStd with RMA (Wilder's smoothing with bias correction)</item>
/// <item>RVI = 100 × avgUpStd / (avgUpStd + avgDownStd)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Oscillator ranging from 0 to 100</item>
/// <item>Values above 50 indicate upward volatility momentum</item>
/// <item>Values below 50 indicate downward volatility momentum</item>
/// <item>Often used to confirm RSI signals or as a standalone indicator</item>
/// </list>
///
/// <b>Sources:</b>
/// Donald Dorsey (1993). "The Relative Volatility Index". Technical Analysis of Stocks &amp; Commodities.
/// </remarks>
[SkipLocalsInit]
public sealed class Rvi : AbstractBase
{
    private const double Epsilon = 1e-10;

    private readonly int _stdevLength;
    private readonly int _rmaLength;
    private readonly double _alpha;
    private readonly RingBuffer _priceBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevPrice,
        double Sum,
        double SumSq,
        double RawRmaUp,
        double EUp,
        double RawRmaDown,
        double EDown,
        double LastValue,
        int FillCount
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Rvi class.
    /// </summary>
    /// <param name="stdevLength">The lookback period for standard deviation calculation (default 10).</param>
    /// <param name="rmaLength">The lookback period for RMA smoothing (default 14).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when stdevLength is less than 2, or rmaLength is less than 1.
    /// </exception>
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
        _priceBuffer = new RingBuffer(stdevLength);
        WarmupPeriod = stdevLength + rmaLength;
        Name = $"Rvi({stdevLength},{rmaLength})";
        _s = new State(double.NaN, 0, 0, 0, 1.0, 0, 1.0, 50.0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Rvi class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="stdevLength">The lookback period for standard deviation calculation (default 10).</param>
    /// <param name="rmaLength">The lookback period for RMA smoothing (default 14).</param>
    public Rvi(ITValuePublisher source, int stdevLength = 10, int rmaLength = 14)
        : this(stdevLength, rmaLength)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.FillCount >= _stdevLength;

    /// <summary>
    /// The lookback period for standard deviation calculation.
    /// </summary>
    public int StdevLength => _stdevLength;

    /// <summary>
    /// The lookback period for RMA smoothing.
    /// </summary>
    public int RmaLength => _rmaLength;

    /// <summary>
    /// Updates the indicator with a new price value.
    /// </summary>
    /// <param name="input">The input price value.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated RVI value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a new bar (uses Close price).
    /// </summary>
    /// <param name="bar">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated RVI value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return UpdateCore(bar.Time, bar.Close, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the RVI values.</returns>
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

        // Extract close prices
        Span<double> closes = len <= 128 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            closes[i] = source[i].Close;
            tSpan[i] = source[i].Time;
        }

        Batch(closes, vSpan, _stdevLength, _rmaLength);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source[i].Time, source[i].Close), isNew: true);
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
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

        Batch(source.Values, vSpan, _stdevLength, _rmaLength);
        source.Times.CopyTo(tSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double price, bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            _priceBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _priceBuffer.Restore();
        }

        var s = _s;

        // Handle non-finite price
        if (!double.IsFinite(price))
        {
            Last = new TValue(timeTicks, s.LastValue);
            PubEvent(Last, isNew);
            return Last;
        }

        double rviValue;

        // Need previous price for direction
        if (double.IsNaN(s.PrevPrice))
        {
            // First price - add to buffer but no RVI yet
            _priceBuffer.Add(price);
            s = s with
            {
                PrevPrice = price,
                Sum = price,
                SumSq = price * price,
                FillCount = 1
            };
            rviValue = 50.0; // Neutral
        }
        else
        {
            // Calculate price change direction
            double priceChange = price - s.PrevPrice;

            // Update price buffer for stddev calculation
            double oldSum = s.Sum;
            double oldSumSq = s.SumSq;
            int oldCount = s.FillCount;

            // Remove oldest if buffer full
            if (_priceBuffer.Count == _stdevLength)
            {
                double oldest = _priceBuffer[0];
                oldSum -= oldest;
                oldSumSq -= oldest * oldest;
                oldCount--;
            }

            // Add new price
            _priceBuffer.Add(price);
            double newSum = oldSum + price;
            double newSumSq = oldSumSq + (price * price);
            int newCount = oldCount + 1;

            // Calculate population stddev
            double currentStdDev = 0.0;
            if (newCount > 1)
            {
                double mean = newSum / newCount;
                double variance = (newSumSq / newCount) - (mean * mean);
                variance = Math.Max(0.0, variance);
                currentStdDev = Math.Sqrt(variance);
            }

            // Classify stddev by direction
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
            // If priceChange == 0, both stay 0

            // RMA with bias correction for upward stddev
            double rawRmaUp = s.RawRmaUp;
            double eUp = s.EUp;

            rawRmaUp = Math.FusedMultiplyAdd(rawRmaUp, _rmaLength - 1, upStdVal) / _rmaLength;
            eUp = (1 - _alpha) * eUp;
            double avgUpStd = eUp > Epsilon ? rawRmaUp / (1.0 - eUp) : rawRmaUp;

            // RMA with bias correction for downward stddev
            double rawRmaDown = s.RawRmaDown;
            double eDown = s.EDown;

            rawRmaDown = Math.FusedMultiplyAdd(rawRmaDown, _rmaLength - 1, downStdVal) / _rmaLength;
            eDown = (1 - _alpha) * eDown;
            double avgDownStd = eDown > Epsilon ? rawRmaDown / (1.0 - eDown) : rawRmaDown;

            // Calculate RVI
            double sumAvgStd = avgUpStd + avgDownStd;
            rviValue = sumAvgStd > Epsilon ? (100.0 * avgUpStd / sumAvgStd) : 50.0;

            s = s with
            {
                PrevPrice = price,
                Sum = newSum,
                SumSq = newSumSq,
                RawRmaUp = rawRmaUp,
                EUp = eUp,
                RawRmaDown = rawRmaDown,
                EDown = eDown,
                FillCount = newCount
            };
        }

        if (!double.IsFinite(rviValue))
        {
            rviValue = s.LastValue;
        }
        else
        {
            s = s with { LastValue = rviValue };
        }

        _s = s;

        Last = new TValue(timeTicks, rviValue);
        PubEvent(Last, isNew);
        return Last;
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
        _s = new State(double.NaN, 0, 0, 0, 1.0, 0, 1.0, 50.0, 0);
        _ps = _s;
        _priceBuffer.Clear();
        Last = default;
    }

    /// <summary>
    /// Calculates Relative Volatility Index for a price series (static).
    /// </summary>
    /// <param name="source">The source price series.</param>
    /// <param name="stdevLength">The lookback period for standard deviation.</param>
    /// <param name="rmaLength">The lookback period for RMA smoothing.</param>
    /// <returns>A TSeries containing the RVI values.</returns>
    public static TSeries Calculate(TSeries source, int stdevLength = 10, int rmaLength = 14)
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

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, stdevLength, rmaLength);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates RVI for a bar series (static).
    /// </summary>
    public static TSeries Calculate(TBarSeries source, int stdevLength = 10, int rmaLength = 14)
    {
        var rvi = new Rvi(stdevLength, rmaLength);
        return rvi.Update(source);
    }

    /// <summary>
    /// Batch calculation using spans.
    /// </summary>
    /// <param name="prices">Price values.</param>
    /// <param name="output">Output RVI values.</param>
    /// <param name="stdevLength">The lookback period for standard deviation.</param>
    /// <param name="rmaLength">The lookback period for RMA smoothing.</param>
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

        int len = prices.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 1.0 / rmaLength;

        // Price buffer for stddev
        Span<double> priceBuffer = stdevLength <= 256 ? stackalloc double[stdevLength] : new double[stdevLength];
        int head = 0;
        int count = 0;
        double sum = 0;
        double sumSq = 0;
        double prevPrice = double.NaN;
        double lastValue = 50.0;

        // RMA state
        double rawRmaUp = 0;
        double eUp = 1.0;
        double rawRmaDown = 0;
        double eDown = 1.0;

        for (int i = 0; i < len; i++)
        {
            double price = prices[i];

            // First price
            if (double.IsNaN(prevPrice))
            {
                // Handle invalid first price - output neutral and continue
                if (!double.IsFinite(price))
                {
                    output[i] = lastValue;
                    continue;
                }

                // Add to buffer
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

            // Handle invalid price
            if (!double.IsFinite(price))
            {
                output[i] = lastValue;
                continue;
            }

            // Price change direction
            double priceChange = price - prevPrice;
            prevPrice = price;

            // Update buffer
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

            // Population stddev
            double currentStdDev = 0.0;
            if (count > 1)
            {
                double mean = sum / count;
                double variance = (sumSq / count) - (mean * mean);
                variance = Math.Max(0.0, variance);
                currentStdDev = Math.Sqrt(variance);
            }

            // Classify by direction
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

            // RMA with bias correction
            rawRmaUp = Math.FusedMultiplyAdd(rawRmaUp, rmaLength - 1, upStdVal) / rmaLength;
            eUp = (1 - alpha) * eUp;
            double avgUpStd = eUp > Epsilon ? rawRmaUp / (1.0 - eUp) : rawRmaUp;

            rawRmaDown = Math.FusedMultiplyAdd(rawRmaDown, rmaLength - 1, downStdVal) / rmaLength;
            eDown = (1 - alpha) * eDown;
            double avgDownStd = eDown > Epsilon ? rawRmaDown / (1.0 - eDown) : rawRmaDown;

            // RVI
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
}