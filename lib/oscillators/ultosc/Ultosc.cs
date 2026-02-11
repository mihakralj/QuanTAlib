using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ULTOSC: Ultimate Oscillator
/// </summary>
/// <remarks>
/// The Ultimate Oscillator, developed by Larry Williams in 1976, is a momentum oscillator
/// that uses weighted averages of three different time periods to reduce volatility and
/// false signals inherent in single-period oscillators.
///
/// Calculation:
/// 1. Buying Pressure (BP) = Close - True Low
///    True Low = Min(Low, Previous Close)
/// 2. True Range (TR) = True High - True Low
///    True High = Max(High, Previous Close)
/// 3. Average for each period = Sum(BP) / Sum(TR)
/// 4. Ultimate Oscillator = 100 * (4*Avg7 + 2*Avg14 + Avg28) / (4 + 2 + 1)
///
/// Key Features:
/// - Three time frames reduce false signals
/// - Buying pressure concept measures demand
/// - Weighted average gives priority to shorter-term movements
///
/// Sources:
/// - Larry Williams, "The Ultimate Oscillator" (1985 Stocks & Commodities)
/// - https://www.investopedia.com/terms/u/ultimateoscillator.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Ultosc : AbstractBase
{
    private readonly int _period1;
    private readonly int _period2;
    private readonly int _period3;
    private readonly RingBuffer _bp1;
    private readonly RingBuffer _bp2;
    private readonly RingBuffer _bp3;
    private readonly RingBuffer _tr1;
    private readonly RingBuffer _tr2;
    private readonly RingBuffer _tr3;
    private double _prevClose;
    private double _p_prevClose;
    private int _index;
    private int _p_index;
    private readonly TBarSeries? _source;
    private readonly TBarPublishedHandler? _handler;
    private bool _disposed;

    // Weights: 4:2:1
    private const double Weight1 = 4.0;
    private const double Weight2 = 2.0;
    private const double Weight3 = 1.0;
    private const double WeightSum = Weight1 + Weight2 + Weight3; // 7.0

    public override bool IsHot => _index >= _period3;

    /// <summary>
    /// Creates Ultimate Oscillator with specified periods.
    /// </summary>
    /// <param name="period1">Short period (default: 7)</param>
    /// <param name="period2">Intermediate period (default: 14)</param>
    /// <param name="period3">Long period (default: 28)</param>
    public Ultosc(int period1 = 7, int period2 = 14, int period3 = 28)
    {
        if (period1 <= 0)
        {
            throw new ArgumentException("Period1 must be greater than 0", nameof(period1));
        }

        if (period2 <= 0)
        {
            throw new ArgumentException("Period2 must be greater than 0", nameof(period2));
        }

        if (period3 <= 0)
        {
            throw new ArgumentException("Period3 must be greater than 0", nameof(period3));
        }

        if (period1 >= period2)
        {
            throw new ArgumentException("Period1 must be less than Period2", nameof(period1));
        }

        if (period2 >= period3)
        {
            throw new ArgumentException("Period2 must be less than Period3", nameof(period2));
        }

        _period1 = period1;
        _period2 = period2;
        _period3 = period3;
        _bp1 = new RingBuffer(period1);
        _bp2 = new RingBuffer(period2);
        _bp3 = new RingBuffer(period3);
        _tr1 = new RingBuffer(period1);
        _tr2 = new RingBuffer(period2);
        _tr3 = new RingBuffer(period3);
        _prevClose = double.NaN;
        _p_prevClose = double.NaN;
        _index = 0;
        _p_index = 0;

        Name = $"Ultosc({period1},{period2},{period3})";
        WarmupPeriod = period3;
    }

    /// <summary>
    /// Creates Ultimate Oscillator with source subscription and specified periods.
    /// </summary>
    public Ultosc(TBarSeries source, int period1 = 7, int period2 = 14, int period3 = 28) : this(period1, period2, period3)
    {
        _source = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null && _handler != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    private void Handle(object? sender, in TBarEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_prevClose = _prevClose;
            _p_index = _index;
        }
        else
        {
            _prevClose = _p_prevClose;
            _index = _p_index;
        }

        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        // Handle invalid inputs
        if (!double.IsFinite(high) || !double.IsFinite(low) || !double.IsFinite(close))
        {
            Last = new TValue(input.Time, Last.Value);
            PubEvent(Last, isNew);
            return Last;
        }

        double bp, tr;
        if (double.IsNaN(_prevClose))
        {
            // First bar: True Range = High - Low, BP = Close - Low
            bp = close - low;
            tr = high - low;
        }
        else
        {
            // True Low = Min(Low, Previous Close)
            double trueLow = Math.Min(low, _prevClose);
            // True High = Max(High, Previous Close)
            double trueHigh = Math.Max(high, _prevClose);
            // Buying Pressure = Close - True Low
            bp = close - trueLow;
            // True Range = True High - True Low
            tr = trueHigh - trueLow;
        }

        // Add to all three period buffers
        _bp1.Add(bp, isNew);
        _bp2.Add(bp, isNew);
        _bp3.Add(bp, isNew);
        _tr1.Add(tr, isNew);
        _tr2.Add(tr, isNew);
        _tr3.Add(tr, isNew);

        if (isNew)
        {
            _prevClose = close;
            _index++;
        }

        // Calculate sums
        double bpSum1 = _bp1.Sum();
        double bpSum2 = _bp2.Sum();
        double bpSum3 = _bp3.Sum();
        double trSum1 = _tr1.Sum();
        double trSum2 = _tr2.Sum();
        double trSum3 = _tr3.Sum();

        // Calculate averages (handle division by zero)
        const double epsilon = 1e-10;
        double avg1 = trSum1 > epsilon ? bpSum1 / trSum1 : 0.5;
        double avg2 = trSum2 > epsilon ? bpSum2 / trSum2 : 0.5;
        double avg3 = trSum3 > epsilon ? bpSum3 / trSum3 : 0.5;

        // Ultimate Oscillator = 100 * (4*Avg1 + 2*Avg2 + Avg3) / 7
        double ultosc = 100.0 * Math.FusedMultiplyAdd(Weight1, avg1, Math.FusedMultiplyAdd(Weight2, avg2, Weight3 * avg3)) / WeightSum;

        Last = new TValue(input.Time, ultosc);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Update for TValue input - not recommended for Ultimate Oscillator as it needs OHLC.
    /// This method will return 50 (neutral) since proper calculation requires OHLC data.
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Ultimate Oscillator requires OHLC data
        // Return neutral value if called with TValue
        Last = new TValue(input.Time, 50.0);
        PubEvent(Last, isNew);
        return Last;
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

        // Calculate using span method
        Batch(source.High.Values, source.Low.Values, source.Close.Values,
                  vSpan, _period1, _period2, _period3);
        source.Times.CopyTo(tSpan);

        // Restore state for streaming
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i]);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override TSeries Update(TSeries source)
    {
        // Cannot properly calculate Ultimate Oscillator from single-value series
        // Return series of neutral values
        if (source.Count == 0)
        {
            return [];
        }

        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            t.Add(source.Times[i]);
            v.Add(50.0);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        // Cannot properly prime Ultimate Oscillator from single-value array
        // This method is a no-op for OHLC indicators
    }

    public static TSeries Batch(TBarSeries source, int period1 = 7, int period2 = 14, int period3 = 28)
    {
        var ultosc = new Ultosc(period1, period2, period3);
        return ultosc.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period1 = 7,
        int period2 = 14,
        int period3 = 28)
    {
        int len = high.Length;
        if (len != low.Length || len != close.Length || len != output.Length)
        {
            throw new ArgumentException("All arrays must have the same length", nameof(output));
        }

        if (period1 <= 0)
        {
            throw new ArgumentException("Period1 must be greater than 0", nameof(period1));
        }

        if (period2 <= 0)
        {
            throw new ArgumentException("Period2 must be greater than 0", nameof(period2));
        }

        if (period3 <= 0)
        {
            throw new ArgumentException("Period3 must be greater than 0", nameof(period3));
        }

        if (period1 >= period2)
        {
            throw new ArgumentException("Period1 must be less than Period2", nameof(period1));
        }

        if (period2 >= period3)
        {
            throw new ArgumentException("Period2 must be less than Period3", nameof(period2));
        }

        if (len == 0)
        {
            return;
        }

        // Allocate buffers for BP and TR
        double[] bpArray = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        double[] trArray = System.Buffers.ArrayPool<double>.Shared.Rent(len);

        try
        {
            Span<double> bp = bpArray.AsSpan(0, len);
            Span<double> tr = trArray.AsSpan(0, len);

            // First bar
            bp[0] = close[0] - low[0];
            tr[0] = high[0] - low[0];

            // Calculate BP and TR for remaining bars
            for (int i = 1; i < len; i++)
            {
                double h = high[i];
                double l = low[i];
                double c = close[i];
                double prevC = close[i - 1];

                double trueLow = Math.Min(l, prevC);
                double trueHigh = Math.Max(h, prevC);

                bp[i] = c - trueLow;
                tr[i] = trueHigh - trueLow;
            }

            // Calculate running sums and output
            double bpSum1 = 0, bpSum2 = 0, bpSum3 = 0;
            double trSum1 = 0, trSum2 = 0, trSum3 = 0;

            const double epsilon = 1e-10;

            for (int i = 0; i < len; i++)
            {
                // Add current values
                bpSum1 += bp[i];
                bpSum2 += bp[i];
                bpSum3 += bp[i];
                trSum1 += tr[i];
                trSum2 += tr[i];
                trSum3 += tr[i];

                // Remove old values for each period window
                if (i >= period1)
                {
                    bpSum1 -= bp[i - period1];
                    trSum1 -= tr[i - period1];
                }
                if (i >= period2)
                {
                    bpSum2 -= bp[i - period2];
                    trSum2 -= tr[i - period2];
                }
                if (i >= period3)
                {
                    bpSum3 -= bp[i - period3];
                    trSum3 -= tr[i - period3];
                }

                // Calculate averages
                double avg1 = trSum1 > epsilon ? bpSum1 / trSum1 : 0.5;
                double avg2 = trSum2 > epsilon ? bpSum2 / trSum2 : 0.5;
                double avg3 = trSum3 > epsilon ? bpSum3 / trSum3 : 0.5;

                // Ultimate Oscillator
                output[i] = 100.0 * Math.FusedMultiplyAdd(Weight1, avg1, Math.FusedMultiplyAdd(Weight2, avg2, Weight3 * avg3)) / WeightSum;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<double>.Shared.Return(bpArray);
            System.Buffers.ArrayPool<double>.Shared.Return(trArray);
        }
    }

    public static (TSeries Results, Ultosc Indicator) Calculate(TBarSeries source, int period1 = 7, int period2 = 14, int period3 = 28)
    {
        var indicator = new Ultosc(period1, period2, period3);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }


    public override void Reset()
    {
        _bp1.Clear();
        _bp2.Clear();
        _bp3.Clear();
        _tr1.Clear();
        _tr2.Clear();
        _tr3.Clear();
        _prevClose = double.NaN;
        _p_prevClose = double.NaN;
        _index = 0;
        _p_index = 0;
        Last = default;
    }
}
