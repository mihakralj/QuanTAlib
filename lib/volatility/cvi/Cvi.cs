using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CVI: Chaikin's Volatility
/// </summary>
/// <remarks>
/// Chaikin's Volatility measures the rate of change of the EMA-smoothed high-low range.
/// It identifies periods of increasing or decreasing trading range volatility by comparing
/// the current smoothed range to a prior value.
///
/// Formula:
/// <c>Range_t = High_t - Low_t</c>
/// <c>EMA_t = EMA(Range, smoothLength)</c>
/// <c>CVI = ((EMA_t - EMA_{t-rocLength}) / EMA_{t-rocLength}) × 100</c>
///
/// Key properties:
/// - Positive values indicate expanding volatility
/// - Negative values indicate contracting volatility
/// - Uses High-Low range (requires OHLC data)
/// - EMA smoothing reduces noise before ROC calculation
/// </remarks>
[SkipLocalsInit]
public sealed class Cvi : AbstractBase
{
    private readonly int _rocLength;
    private readonly int _smoothLength;
    private readonly double _alpha;
    private readonly RingBuffer _emaBuffer;

    private const double Epsilon = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Ema,
        double LastValidRange,
        int Count);
    private State _s;
    private State _ps;

    /// <summary>
    /// Creates CVI with specified parameters.
    /// </summary>
    /// <param name="rocLength">Period for Rate of Change calculation (must be > 0)</param>
    /// <param name="smoothLength">Period for EMA smoothing of high-low range (must be > 0)</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    public Cvi(int rocLength = 10, int smoothLength = 10)
    {
        if (rocLength <= 0)
        {
            throw new ArgumentException("ROC length must be greater than 0", nameof(rocLength));
        }
        if (smoothLength <= 0)
        {
            throw new ArgumentException("Smooth length must be greater than 0", nameof(smoothLength));
        }

        _rocLength = rocLength;
        _smoothLength = smoothLength;
        _alpha = 2.0 / (smoothLength + 1);
        _emaBuffer = new RingBuffer(rocLength + 1);
        Name = $"Cvi({rocLength},{smoothLength})";
        WarmupPeriod = smoothLength + rocLength;
        _s = new State(0.0, 0.0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates CVI with specified source and parameters.
    /// </summary>
    public Cvi(ITValuePublisher source, int rocLength = 10, int smoothLength = 10) : this(rocLength, smoothLength)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// ROC length for the indicator.
    /// </summary>
    public int RocLength => _rocLength;

    /// <summary>
    /// Smoothing length for EMA.
    /// </summary>
    public int SmoothLength => _smoothLength;

    /// <summary>
    /// Updates CVI with a TValue input (treats value as pre-calculated range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateWithRange(input.Time, input.Value, isNew);
    }

    /// <summary>
    /// Updates CVI with a TBar input (preferred - uses High-Low range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double range = input.High - input.Low;
        return UpdateWithRange(input.Time, range, isNew);
    }

    /// <summary>
    /// Updates CVI with a TBarSeries.
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

        // Extract high-low ranges
        Span<double> ranges = len <= 256 ? stackalloc double[len] : new double[len];
        for (int i = 0; i < len; i++)
        {
            ranges[i] = source[i].High - source[i].Low;
        }

        Batch(ranges, vSpan, _rocLength, _smoothLength);

        for (int i = 0; i < len; i++)
        {
            tSpan[i] = source[i].Time;
        }

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
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

        Batch(source.Values, vSpan, _rocLength, _smoothLength);
        source.Times.CopyTo(tSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateWithRange(long timeTicks, double range, bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            _emaBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _emaBuffer.Restore();
        }

        var s = _s;

        // Sanitize input
        if (!double.IsFinite(range) || range < 0)
        {
            range = double.IsFinite(s.LastValidRange) && s.LastValidRange >= 0 ? s.LastValidRange : 0.0;
        }
        else
        {
            s.LastValidRange = range;
        }

        // Calculate EMA of range
        double ema;
        if (s.Count == 0)
        {
            ema = range;
        }
        else
        {
            // EMA: ema = (range - prevEma) * alpha + prevEma
            ema = Math.FusedMultiplyAdd(range - s.Ema, _alpha, s.Ema);
        }

        // Always use Add() after Snapshot/Restore pattern
        // When isNew=false, Restore() reverts buffer to pre-Add state,
        // so we need Add() (not UpdateNewest) to put the value back
        _emaBuffer.Add(ema);

        if (isNew)
        {
            s.Ema = ema;
            s.Count++;
        }
        else
        {
            s.Ema = ema;
        }

        _s = s;

        // Calculate ROC
        double result = 0.0;
        if (_emaBuffer.Count > _rocLength)
        {
            // Get EMA value from rocLength bars ago
            double oldEma = _emaBuffer[_emaBuffer.Count - 1 - _rocLength];

            if (Math.Abs(oldEma) > Epsilon)
            {
                result = ((ema - oldEma) / oldEma) * 100.0;
            }
        }

        if (!double.IsFinite(result))
        {
            result = 0.0;
        }

        Last = new TValue(timeTicks, result);
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
        _emaBuffer.Clear();
        _s = new State(0.0, 0.0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates CVI for entire TBarSeries.
    /// </summary>
    public static TSeries Calculate(TBarSeries source, int rocLength = 10, int smoothLength = 10)
    {
        var cvi = new Cvi(rocLength, smoothLength);
        return cvi.Update(source);
    }

    /// <summary>
    /// Calculates CVI for entire series (assumes values are pre-calculated ranges).
    /// </summary>
    public static TSeries Calculate(TSeries source, int rocLength = 10, int smoothLength = 10)
    {
        if (rocLength <= 0)
        {
            throw new ArgumentException("ROC length must be greater than 0", nameof(rocLength));
        }
        if (smoothLength <= 0)
        {
            throw new ArgumentException("Smooth length must be greater than 0", nameof(smoothLength));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, rocLength, smoothLength);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch CVI calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int rocLength = 10, int smoothLength = 10)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (rocLength <= 0)
        {
            throw new ArgumentException("ROC length must be greater than 0", nameof(rocLength));
        }
        if (smoothLength <= 0)
        {
            throw new ArgumentException("Smooth length must be greater than 0", nameof(smoothLength));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 2.0 / (smoothLength + 1);
        var emaBuffer = new RingBuffer(rocLength + 1);
        double ema = 0.0;
        double lastValidRange = 0.0;

        for (int i = 0; i < len; i++)
        {
            double range = source[i];

            // Sanitize input
            if (!double.IsFinite(range) || range < 0)
            {
                range = lastValidRange;
            }
            else
            {
                lastValidRange = range;
            }

            // Calculate EMA
            if (i == 0)
            {
                ema = range;
            }
            else
            {
                ema = Math.FusedMultiplyAdd(range - ema, alpha, ema);
            }

            emaBuffer.Add(ema);

            // Calculate ROC
            double result = 0.0;
            if (emaBuffer.Count > rocLength)
            {
                double oldEma = emaBuffer[emaBuffer.Count - 1 - rocLength];
                if (Math.Abs(oldEma) > Epsilon)
                {
                    result = ((ema - oldEma) / oldEma) * 100.0;
                }
            }

            output[i] = double.IsFinite(result) ? result : 0.0;
        }
    }
}