using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RAVI: Chande Range Action Verification Index
/// Measures trend strength by computing the absolute percentage divergence
/// between a short-period SMA and a long-period SMA.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>SMA_short = running sum of last shortPeriod closes / shortPeriod</item>
/// <item>SMA_long = running sum of last longPeriod closes / longPeriod</item>
/// <item>RAVI = |SMA_short - SMA_long| / |SMA_long| * 100</item>
/// </list>
///
/// <b>Sources:</b>
/// Tushar Chande, "Beyond Technical Analysis", Wiley, 2nd ed. (2001), pp. 66-70
/// </remarks>
/// <seealso href="Ravi.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Ravi : AbstractBase
{
    private readonly int _shortPeriod;
    private readonly int _longPeriod;
    private readonly RingBuffer _shortBuffer;
    private readonly RingBuffer _longBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double ShortSum,
        double LongSum,
        double LastValidValue,
        int ShortTickCount,
        int LongTickCount
    );

    private State _s;
    private State _ps;

    private const int ResyncInterval = 1000;

    /// <summary>
    /// Creates RAVI with specified short and long SMA periods.
    /// </summary>
    /// <param name="shortPeriod">Short SMA period (must be &gt; 0, default 7)</param>
    /// <param name="longPeriod">Long SMA period (must be &gt; shortPeriod, default 65)</param>
    public Ravi(int shortPeriod = 7, int longPeriod = 65)
    {
        if (shortPeriod <= 0)
        {
            throw new ArgumentException("Short period must be greater than 0", nameof(shortPeriod));
        }
        if (longPeriod <= 0)
        {
            throw new ArgumentException("Long period must be greater than 0", nameof(longPeriod));
        }
        if (shortPeriod >= longPeriod)
        {
            throw new ArgumentException("Short period must be less than long period", nameof(shortPeriod));
        }

        _shortPeriod = shortPeriod;
        _longPeriod = longPeriod;
        _shortBuffer = new RingBuffer(shortPeriod);
        _longBuffer = new RingBuffer(longPeriod);
        Name = $"Ravi({shortPeriod},{longPeriod})";
        WarmupPeriod = longPeriod;
        _s = new State(0, 0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Creates RAVI with specified source and parameters.
    /// </summary>
    public Ravi(ITValuePublisher source, int shortPeriod = 7, int longPeriod = 65) : this(shortPeriod, longPeriod)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True when both SMA buffers are full (long buffer determines warmup).
    /// </summary>
    public override bool IsHot => _longBuffer.IsFull;

    /// <summary>
    /// Updates the indicator with a single TValue input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
            // Restore buffer state for bar correction
            _shortBuffer.UpdateNewest(_shortBuffer.Newest);
            _longBuffer.UpdateNewest(_longBuffer.Newest);
        }

        var s = _s;

        // NaN/Infinity handling: last-valid substitution
        double val = input.Value;
        if (double.IsFinite(val))
        {
            s.LastValidValue = val;
        }
        else
        {
            val = s.LastValidValue;
        }

        if (isNew)
        {
            // Short buffer: remove oldest, add new
            double shortRemoved = _shortBuffer.Count == _shortBuffer.Capacity ? _shortBuffer.Oldest : 0.0;
            s.ShortSum = s.ShortSum - shortRemoved + val;
            _shortBuffer.Add(val);

            // Long buffer: remove oldest, add new
            double longRemoved = _longBuffer.Count == _longBuffer.Capacity ? _longBuffer.Oldest : 0.0;
            s.LongSum = s.LongSum - longRemoved + val;
            _longBuffer.Add(val);

            // Resync to prevent floating-point drift
            s.ShortTickCount++;
            if (_shortBuffer.IsFull && s.ShortTickCount >= ResyncInterval)
            {
                s.ShortTickCount = 0;
                s.ShortSum = _shortBuffer.RecalculateSum();
            }
            s.LongTickCount++;
            if (_longBuffer.IsFull && s.LongTickCount >= ResyncInterval)
            {
                s.LongTickCount = 0;
                s.LongSum = _longBuffer.RecalculateSum();
            }
        }
        else
        {
            // Bar correction: update newest value in both buffers
            _shortBuffer.UpdateNewest(val);
            s.ShortSum = _shortBuffer.Sum;

            _longBuffer.UpdateNewest(val);
            s.LongSum = _longBuffer.Sum;
        }

        // Calculate RAVI
        double result;
        if (_longBuffer.IsFull && _shortBuffer.IsFull)
        {
            double smaShort = s.ShortSum / _shortPeriod;
            double smaLong = s.LongSum / _longPeriod;
            double absSmaLong = Math.Abs(smaLong);

            // Division-by-zero guard
            if (absSmaLong > 1e-10)
            {
                result = Math.Abs(smaShort - smaLong) / absSmaLong * 100.0;
            }
            else
            {
                result = 0.0;
            }
        }
        else
        {
            result = 0.0;
        }

        _s = s;

        Last = new TValue(input.Time, result);
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

        Batch(source.Values, vSpan, _shortPeriod, _longPeriod);
        source.Times.CopyTo(tSpan);

        // Prime internal state by replaying last longPeriod bars
        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _shortBuffer.Clear();
        _longBuffer.Clear();
        _s = default;
        _ps = default;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        // Seed LastValidValue
        _s.LastValidValue = 0;
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source[i]))
            {
                _s.LastValidValue = source[i];
                break;
            }
        }

        if (_s.LastValidValue == 0)
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (double.IsFinite(source[i]))
                {
                    _s.LastValidValue = source[i];
                    break;
                }
            }
        }

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]), isNew: true);
        }

        _ps = _s;
    }

    /// <summary>
    /// Calculates RAVI for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int shortPeriod = 7, int longPeriod = 65)
    {
        var ravi = new Ravi(shortPeriod, longPeriod);
        return ravi.Update(source);
    }

    /// <summary>
    /// Span-based batch calculation for close price arrays.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    /// <param name="source">Close prices.</param>
    /// <param name="output">Output RAVI values.</param>
    /// <param name="shortPeriod">Short SMA period.</param>
    /// <param name="longPeriod">Long SMA period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int shortPeriod = 7, int longPeriod = 65)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (shortPeriod <= 0)
        {
            throw new ArgumentException("Short period must be greater than 0", nameof(shortPeriod));
        }
        if (longPeriod <= 0)
        {
            throw new ArgumentException("Long period must be greater than 0", nameof(longPeriod));
        }
        if (shortPeriod >= longPeriod)
        {
            throw new ArgumentException("Short period must be less than long period", nameof(shortPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, shortPeriod, longPeriod);
    }

    /// <summary>
    /// Calculates RAVI and returns both results and the indicator instance.
    /// </summary>
    public static (TSeries Results, Ravi Indicator) Calculate(TSeries source, int shortPeriod = 7, int longPeriod = 65)
    {
        var indicator = new Ravi(shortPeriod, longPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ---- Private implementation ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int shortPeriod, int longPeriod)
    {
        int len = source.Length;

        const int StackAllocThreshold = 256;

        // Short buffer
        double[]? rentedShort = shortPeriod > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(shortPeriod) : null;
        Span<double> shortBuf = rentedShort != null
            ? rentedShort.AsSpan(0, shortPeriod)
            : stackalloc double[shortPeriod];

        // Long buffer
        double[]? rentedLong = longPeriod > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(longPeriod) : null;
        Span<double> longBuf = rentedLong != null
            ? rentedLong.AsSpan(0, longPeriod)
            : stackalloc double[longPeriod];

        try
        {
            double shortSum = 0;
            double longSum = 0;
            double lastValid = 0;
            int shortIdx = 0;
            int longIdx = 0;
            int shortFilled = 0;
            int longFilled = 0;

            // Find first valid value to seed lastValid
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }

            int shortTickCount = 0;
            int longTickCount = 0;

            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (double.IsFinite(val))
                {
                    lastValid = val;
                }
                else
                {
                    val = lastValid;
                }

                // Update short buffer
                if (shortFilled >= shortPeriod)
                {
                    shortSum -= shortBuf[shortIdx];
                }
                shortSum += val;
                shortBuf[shortIdx] = val;
                if (shortFilled < shortPeriod)
                {
                    shortFilled++;
                }
                shortIdx++;
                if (shortIdx >= shortPeriod)
                {
                    shortIdx = 0;
                }

                // Update long buffer
                if (longFilled >= longPeriod)
                {
                    longSum -= longBuf[longIdx];
                }
                longSum += val;
                longBuf[longIdx] = val;
                if (longFilled < longPeriod)
                {
                    longFilled++;
                }
                longIdx++;
                if (longIdx >= longPeriod)
                {
                    longIdx = 0;
                }

                // Resync short
                shortTickCount++;
                if (shortFilled >= shortPeriod && shortTickCount >= ResyncInterval)
                {
                    shortTickCount = 0;
                    double recalc = 0;
                    for (int k = 0; k < shortPeriod; k++)
                    {
                        recalc += shortBuf[k];
                    }
                    shortSum = recalc;
                }

                // Resync long
                longTickCount++;
                if (longFilled >= longPeriod && longTickCount >= ResyncInterval)
                {
                    longTickCount = 0;
                    double recalc = 0;
                    for (int k = 0; k < longPeriod; k++)
                    {
                        recalc += longBuf[k];
                    }
                    longSum = recalc;
                }

                // Calculate RAVI
                if (shortFilled >= shortPeriod && longFilled >= longPeriod)
                {
                    double smaShort = shortSum / shortPeriod;
                    double smaLong = longSum / longPeriod;
                    double absSmaLong = Math.Abs(smaLong);

                    if (absSmaLong > 1e-10)
                    {
                        output[i] = Math.Abs(smaShort - smaLong) / absSmaLong * 100.0;
                    }
                    else
                    {
                        output[i] = 0.0;
                    }
                }
                else
                {
                    output[i] = 0.0;
                }
            }
        }
        finally
        {
            if (rentedShort != null)
            {
                ArrayPool<double>.Shared.Return(rentedShort);
            }
            if (rentedLong != null)
            {
                ArrayPool<double>.Shared.Return(rentedLong);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _shortBuffer.Clear();
        _longBuffer.Clear();
        _s = new State(0, 0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }
}
