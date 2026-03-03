using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VHF: Vertical Horizontal Filter
/// Measures trend strength by computing the ratio of max-min range (vertical)
/// to the sum of absolute bar-to-bar changes (horizontal path).
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Numerator = Highest(close, N+1) - Lowest(close, N+1)</item>
/// <item>Denominator = Sum(|close[i] - close[i-1]|, i=1..N)</item>
/// <item>VHF = Numerator / Denominator</item>
/// </list>
///
/// <b>Sources:</b>
/// Adam White, "Vertical Horizontal Filter", Futures magazine, August 1991
/// </remarks>
/// <seealso href="Vhf.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Vhf : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _closeBuffer;  // period+1 close values for max/min
    private readonly RingBuffer _diffBuffer;   // period absolute differences for running sum

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double DiffSum,
        double PrevClose,
        double LastValidValue,
        int TickCount,
        bool HasPrevClose
    );

    private State _s;
    private State _ps;

    private const int ResyncInterval = 1000;

    /// <summary>
    /// Creates VHF with specified lookback period.
    /// </summary>
    /// <param name="period">Lookback period (must be &gt; 1, default 28)</param>
    public Vhf(int period = 28)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _period = period;
        _closeBuffer = new RingBuffer(period + 1);  // need period+1 closes for range
        _diffBuffer = new RingBuffer(period);        // period absolute differences
        Name = $"Vhf({period})";
        WarmupPeriod = period + 1;
        _s = new State(0, 0, 0, 0, false);
        _ps = _s;
    }

    /// <summary>
    /// Creates VHF with specified source and period.
    /// </summary>
    public Vhf(ITValuePublisher source, int period = 28) : this(period)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True when close buffer has period+1 values (enough for full VHF calculation).
    /// </summary>
    public override bool IsHot => _closeBuffer.IsFull;

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
            _closeBuffer.UpdateNewest(_closeBuffer.Newest);
            _diffBuffer.UpdateNewest(_diffBuffer.Newest);
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
            // Compute absolute change from previous close
            double absDiff = 0;
            if (s.HasPrevClose)
            {
                absDiff = Math.Abs(val - s.PrevClose);
            }

            // Update diff buffer running sum
            if (s.HasPrevClose)
            {
                double diffRemoved = _diffBuffer.Count == _diffBuffer.Capacity ? _diffBuffer.Oldest : 0.0;
                s.DiffSum = s.DiffSum - diffRemoved + absDiff;
                _diffBuffer.Add(absDiff);
            }

            // Add close to buffer
            _closeBuffer.Add(val);

            s.PrevClose = val;
            s.HasPrevClose = true;

            // Resync to prevent floating-point drift
            s.TickCount++;
            if (_diffBuffer.IsFull && s.TickCount >= ResyncInterval)
            {
                s.TickCount = 0;
                s.DiffSum = _diffBuffer.RecalculateSum();
            }
        }
        else
        {
            // Bar correction: update newest close value
            _closeBuffer.UpdateNewest(val);

            // Recompute the newest absolute difference
            if (s.HasPrevClose && _diffBuffer.Count > 0)
            {
                // PrevClose in _ps is the close before the current bar
                double prevCloseForDiff = _ps.PrevClose;
                double newAbsDiff = Math.Abs(val - prevCloseForDiff);
                _diffBuffer.UpdateNewest(newAbsDiff);
                s.DiffSum = _diffBuffer.Sum;
            }
        }

        // Calculate VHF
        double result;
        if (_closeBuffer.IsFull && _diffBuffer.IsFull)
        {
            double highest = _closeBuffer.Max();
            double lowest = _closeBuffer.Min();
            double numerator = highest - lowest;
            double denominator = s.DiffSum;

            // Division-by-zero guard (flat price = all changes zero)
            if (denominator > 1e-10)
            {
                result = numerator / denominator;
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

        // Prime internal state by replaying last WarmupPeriod bars
        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _closeBuffer.Clear();
        _diffBuffer.Clear();
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
    /// Calculates VHF for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 28)
    {
        var vhf = new Vhf(period);
        return vhf.Update(source);
    }

    /// <summary>
    /// Span-based batch calculation for close price arrays.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    /// <param name="source">Close prices.</param>
    /// <param name="output">Output VHF values.</param>
    /// <param name="period">Lookback period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 28)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    /// <summary>
    /// Calculates VHF and returns both results and the indicator instance.
    /// </summary>
    public static (TSeries Results, Vhf Indicator) Calculate(TSeries source, int period = 28)
    {
        var indicator = new Vhf(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ---- Private implementation ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        int closeBufSize = period + 1;

        const int StackAllocThreshold = 256;

        // Close buffer (period+1)
        double[]? rentedClose = closeBufSize > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(closeBufSize) : null;
        Span<double> closeBuf = rentedClose != null
            ? rentedClose.AsSpan(0, closeBufSize)
            : stackalloc double[closeBufSize];

        // Diff buffer (period)
        double[]? rentedDiff = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
        Span<double> diffBuf = rentedDiff != null
            ? rentedDiff.AsSpan(0, period)
            : stackalloc double[period];

        try
        {
            double diffSum = 0;
            double lastValid = 0;
            double prevClose = 0;
            bool hasPrevClose = false;
            int closeIdx = 0;
            int closeFilled = 0;
            int diffIdx = 0;
            int diffFilled = 0;
            int tickCount = 0;

            // Find first valid value to seed lastValid
            for (int k = 0; k < len; k++)
            {
                if (double.IsFinite(source[k]))
                {
                    lastValid = source[k];
                    break;
                }
            }

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

                // Compute absolute change
                if (hasPrevClose)
                {
                    double absDiff = Math.Abs(val - prevClose);

                    // Update diff buffer
                    if (diffFilled >= period)
                    {
                        diffSum -= diffBuf[diffIdx];
                    }
                    diffSum += absDiff;
                    diffBuf[diffIdx] = absDiff;
                    if (diffFilled < period)
                    {
                        diffFilled++;
                    }
                    diffIdx++;
                    if (diffIdx >= period)
                    {
                        diffIdx = 0;
                    }
                }

                // Update close buffer
                closeBuf[closeIdx] = val;
                if (closeFilled < closeBufSize)
                {
                    closeFilled++;
                }
                closeIdx++;
                if (closeIdx >= closeBufSize)
                {
                    closeIdx = 0;
                }

                prevClose = val;
                hasPrevClose = true;

                // Resync diff sum
                tickCount++;
                if (diffFilled >= period && tickCount >= ResyncInterval)
                {
                    tickCount = 0;
                    double recalc = 0;
                    for (int k = 0; k < period; k++)
                    {
                        recalc += diffBuf[k];
                    }
                    diffSum = recalc;
                }

                // Calculate VHF
                if (closeFilled >= closeBufSize && diffFilled >= period)
                {
                    // Scan for max/min over close buffer
                    double hi = double.MinValue;
                    double lo = double.MaxValue;
                    for (int k = 0; k < closeBufSize; k++)
                    {
                        double cv = closeBuf[k];
                        if (cv > hi)
                        {
                            hi = cv;
                        }
                        if (cv < lo)
                        {
                            lo = cv;
                        }
                    }

                    double numerator = hi - lo;

                    if (diffSum > 1e-10)
                    {
                        output[i] = numerator / diffSum;
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
            if (rentedClose != null)
            {
                ArrayPool<double>.Shared.Return(rentedClose);
            }
            if (rentedDiff != null)
            {
                ArrayPool<double>.Shared.Return(rentedDiff);
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _closeBuffer.Clear();
        _diffBuffer.Clear();
        _s = new State(0, 0, 0, 0, false);
        _ps = _s;
        Last = default;
    }
}
