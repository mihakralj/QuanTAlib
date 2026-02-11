// CMO: Chande Momentum Oscillator
// Developed by Tushar Chande, CMO measures momentum using both up and down changes.
// Unlike RSI which is bounded [0,100], CMO is bounded [-100,+100].

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CMO: Chande Momentum Oscillator
/// </summary>
/// <remarks>
/// Momentum oscillator measuring overbought/oversold conditions [-100,+100].
/// Uses sum of gains vs sum of losses over the lookback period.
///
/// Calculation: <c>CMO = 100 × (SumUp - SumDown) / (SumUp + SumDown)</c>
///
/// Key differences from RSI:
/// - RSI uses smoothed averages (RMA), CMO uses simple sums
/// - RSI range is [0,100], CMO range is [-100,+100]
/// - CMO is more sensitive to price changes, RSI is smoother
///
/// Values above +50 indicate overbought, below -50 indicate oversold.
/// Zero crossings can signal momentum shifts.
/// </remarks>
/// <seealso href="Cmo.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Cmo : AbstractBase
{
    private const int DefaultPeriod = 14;
    private readonly int _period;
    private readonly RingBuffer _upBuffer;
    private readonly RingBuffer _downBuffer;
    private readonly TValuePublishedHandler _handler;
    private double _prevValue;
    private double _p_prevValue;

    /// <inheritdoc/>
    public override bool IsHot => _upBuffer.IsFull;

    /// <summary>
    /// Initializes a new CMO indicator with the specified period.
    /// </summary>
    /// <param name="period">Lookback period (default: 14)</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Cmo(int period = DefaultPeriod)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        _period = period;
        _upBuffer = new RingBuffer(period);
        _downBuffer = new RingBuffer(period);
        _handler = Handle;
        _prevValue = double.NaN;
        _p_prevValue = double.NaN;

        Name = $"Cmo({period})";
        WarmupPeriod = period + 1;
    }

    /// <summary>
    /// Initializes a CMO indicator with a source publisher.
    /// </summary>
    /// <param name="source">Source indicator providing values.</param>
    /// <param name="period">Lookback period (default: 14)</param>
    public Cmo(ITValuePublisher source, int period = DefaultPeriod) : this(period)
    {
        source.Pub += _handler;
    }


    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_prevValue = _prevValue;
        }
        else
        {
            _prevValue = _p_prevValue;
        }

        double val = input.Value;
        double up = 0;
        double down = 0;

        if (!double.IsNaN(_prevValue))
        {
            double change = val - _prevValue;
            if (change > 0)
            {
                up = change;
            }
            else if (change < 0)
            {
                down = -change;
            }
        }

        if (isNew)
        {
            _prevValue = val;
        }

        // Update circular buffers - RingBuffer maintains running Sum internally
        _upBuffer.Add(up, isNew);
        _downBuffer.Add(down, isNew);

        // Calculate CMO using RingBuffer's built-in Sum property
        double sumUp = _upBuffer.Sum;
        double sumDown = _downBuffer.Sum;
        double denom = sumUp + sumDown;
        double cmo;
        if (denom < 1e-10)
        {
            cmo = 0; // No movement = neutral
        }
        else
        {
            cmo = 100.0 * (sumUp - sumDown) / denom;
        }

        Last = new TValue(input.Time, cmo);
        PubEvent(Last, isNew);
        return Last;
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

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state for streaming
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]));
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    /// <summary>
    /// Calculates CMO for a batch of data.
    /// </summary>
    /// <param name="source">Input price series.</param>
    /// <param name="period">Lookback period (default: 14)</param>
    /// <returns>TSeries containing CMO values.</returns>
    public static TSeries Batch(TSeries source, int period = DefaultPeriod)
    {
        var cmo = new Cmo(period);
        return cmo.Update(source);
    }

    /// <summary>
    /// SIMD-optimized batch calculation for CMO.
    /// </summary>
    /// <param name="source">Input price data.</param>
    /// <param name="output">Output CMO values.</param>
    /// <param name="period">Lookback period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double[] ups = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        double[] downs = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        Span<double> upSpan = ups.AsSpan(0, len);
        Span<double> downSpan = downs.AsSpan(0, len);

        // Calculate ups and downs
        upSpan[0] = 0;
        downSpan[0] = 0;
        int i = 1;

        if (Vector.IsHardwareAccelerated && len > Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var vZero = Vector<double>.Zero;

            for (; i <= len - vectorSize; i += vectorSize)
            {
                var vCurrent = new Vector<double>(source.Slice(i, vectorSize));
                var vPrev = new Vector<double>(source.Slice(i - 1, vectorSize));
                var vChange = vCurrent - vPrev;

                var vUp = Vector.Max(vChange, vZero);
                var vDown = Vector.Max(-vChange, vZero);

                vUp.CopyTo(upSpan.Slice(i, vectorSize));
                vDown.CopyTo(downSpan.Slice(i, vectorSize));
            }
        }

        for (; i < len; i++)
        {
            double change = source[i] - source[i - 1];
            if (change > 0)
            {
                upSpan[i] = change;
                downSpan[i] = 0;
            }
            else
            {
                upSpan[i] = 0;
                downSpan[i] = -change;
            }
        }

        // Calculate rolling sums and CMO
        double sumUp = 0;
        double sumDown = 0;

        // Warmup phase
        for (i = 0; i < Math.Min(period, len); i++)
        {
            sumUp += upSpan[i];
            sumDown += downSpan[i];
            double denom = sumUp + sumDown;
            output[i] = denom > 1e-10 ? 100.0 * (sumUp - sumDown) / denom : 0;
        }

        // Sliding window phase
        for (; i < len; i++)
        {
            sumUp += upSpan[i] - upSpan[i - period];
            sumDown += downSpan[i] - downSpan[i - period];
            double denom = sumUp + sumDown;
            output[i] = denom > 1e-10 ? 100.0 * (sumUp - sumDown) / denom : 0;
        }

        System.Buffers.ArrayPool<double>.Shared.Return(ups);
        System.Buffers.ArrayPool<double>.Shared.Return(downs);
    }

    public static (TSeries Results, Cmo Indicator) Calculate(TSeries source, int period = DefaultPeriod)
    {
        var indicator = new Cmo(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _upBuffer.Clear();
        _downBuffer.Clear();
        _prevValue = double.NaN;
        _p_prevValue = double.NaN;
        Last = default;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // No external resources to dispose
        }
        base.Dispose(disposing);
    }
}