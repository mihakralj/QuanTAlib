using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TBF: Ehlers Truncated Bandpass Filter
/// A modified bandpass filter that limits the IIR filter's infinite memory to a fixed
/// number of bars, eliminating initialization errors and dampening transient responses
/// for more reliable cycle indication.
/// </summary>
/// <remarks>
/// The TBF calculation process:
/// 1. Compute standard Ehlers bandpass coefficients from Period and Bandwidth
/// 2. Each bar, recompute the entire filter forward over the truncation window
/// 3. Initialize the tail of the window to zero (no memory beyond Length bars)
/// 4. Run the 2-pole IIR recursion from oldest to newest within the window
/// 5. Output the value at the current bar position
///
/// Key characteristics:
/// - Eliminates initialization errors inherent in IIR filters
/// - Dampened transient response reduces false cycle triggers from price shocks
/// - O(Length) computation per bar (brute-force recomputation)
/// - Also computes standard (non-truncated) bandpass for comparison
///
/// Sources:
///     John F. Ehlers - "Truncated Indicators" TASC July 2020
///     https://www.mesasoftware.com/papers/TRUNCATED%20INDICATORS.pdf
/// </remarks>
[SkipLocalsInit]
public sealed class Tbf : AbstractBase
{
    private readonly int _length;

    // Precomputed bandpass coefficients
    private readonly double _a0; // 0.5 * (1 - S1) — input difference coefficient
    private readonly double _a1; // L1 * (1 + S1) — first feedback coefficient
    private readonly double _a2; // -S1 — second feedback coefficient

    // Price history buffer: stores last (length + 2) prices
    private readonly RingBuffer _priceBuffer;

    private const int DefaultPeriod = 20;
    private const double DefaultBandwidth = 0.1;
    private const int DefaultLength = 10;
    private const int MinPeriod = 2;
    private const double MinBandwidth = 0.001;
    private const int MinLength = 1;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Bp1,
        double Bp2,
        double Src1,
        double Src2,
        double LastValid,
        int Bars);

    private State _state;
    private State _p_state;

    public override bool IsHot => _state.Bars >= WarmupPeriod;

    /// <summary>
    /// Standard (non-truncated) bandpass filter value for comparison.
    /// </summary>
    public TValue Bp { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tbf(int period = DefaultPeriod, double bandwidth = DefaultBandwidth, int length = DefaultLength)
    {
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (bandwidth < MinBandwidth)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth),
                $"Bandwidth must be at least {MinBandwidth}.");
        }
        if (length < MinLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length),
                $"Length must be at least {MinLength}.");
        }

        _length = length;

        // Ehlers bandpass coefficients
        double twoPi = 2.0 * Math.PI;
        double l1 = Math.Cos(twoPi / period);
        double g1 = Math.Cos(bandwidth * twoPi / period);
        double s1 = (1.0 / g1) - Math.Sqrt((1.0 / (g1 * g1)) - 1.0);

        _a0 = 0.5 * (1.0 - s1);
        _a1 = l1 * (1.0 + s1);
        _a2 = -s1;

        // Buffer needs length + 2 prices (indices 0..length+1)
        _priceBuffer = new RingBuffer(length + 2);

        WarmupPeriod = length + 2;
        Name = $"TBF({period},{bandwidth:F2},{length})";
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = default;
        _p_state = default;
        _priceBuffer.Clear();
        Bp = new TValue(DateTime.UtcNow, double.NaN);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // State management for bar correction
        if (isNew)
        {
            _p_state = _state;
            _priceBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _priceBuffer.Restore();
        }

        // Handle NaN/Infinity input
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(_state.LastValid) ? _state.LastValid : 0.0;
        }
        else
        {
            _state.LastValid = val;
        }

        _state.Bars++;

        // Add price to buffer
        _priceBuffer.Add(val);

        // === Standard Bandpass (IIR) ===
        double bpStd;
        if (_state.Bars <= 3)
        {
            bpStd = 0.0;
        }
        else
        {
            bpStd = Math.FusedMultiplyAdd(_a0, val - _state.Src2,
                Math.FusedMultiplyAdd(_a1, _state.Bp1,
                    _a2 * _state.Bp2));
        }

        // Update standard BP state
        _state.Bp2 = _state.Bp1;
        _state.Bp1 = bpStd;
        _state.Src2 = _state.Src1;
        _state.Src1 = val;

        Bp = new TValue(input.Time, bpStd);

        // === Truncated Bandpass ===
        double tbfValue;
        if (_state.Bars < _length + 2)
        {
            // Not enough history for full truncation window
            tbfValue = 0.0;
        }
        else
        {
            tbfValue = ComputeTruncated();
        }

        Last = new TValue(input.Time, tbfValue);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        int len = source.Count;
        TSeries result = new(capacity: len);

        for (int i = 0; i < len; i++)
        {
            var item = source[i];
            Update(item, isNew: true);
            result.Add(Last.Time, Last.Value, isNew: true);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeTruncated()
    {
        int len = _length;
        int bufCount = _priceBuffer.Count; // should be length + 2 when full

        // Scratch array for the truncated filter computation
        // Need indices 1..length+2, so allocate length+3 elements
        Span<double> trunc = len + 3 <= 256
            ? stackalloc double[len + 3]
            : new double[len + 3];

        // Initialize tail to zero (no memory beyond truncation window)
        trunc[len + 2] = 0.0;
        trunc[len + 1] = 0.0;

        // Run the IIR recursion forward from oldest to newest within window
        // Ehlers: for count = Length downto 1
        //   Trunc[count] = a0*(Close[count-1] - Close[count+1]) + a1*Trunc[count+1] + a2*Trunc[count+2]
        // Where Close[k] = price k bars ago (Close[0] = current)
        // RingBuffer uses [0]=oldest, [Count-1]=newest, so Close[k bars ago] = [Count-1-k]

        for (int count = len; count >= 1; count--)
        {
            double pricePrev = _priceBuffer[bufCount - count];     // Close[count-1] = [Count-1-(count-1)]
            double priceNext = _priceBuffer[bufCount - count - 2]; // Close[count+1] = [Count-1-(count+1)]
            trunc[count] = Math.FusedMultiplyAdd(_a0, pricePrev - priceNext,
                Math.FusedMultiplyAdd(_a1, trunc[count + 1],
                    _a2 * trunc[count + 2]));
        }

        return trunc[1];
    }

    public override void Reset()
    {
        _priceBuffer.Clear();
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        step ??= TimeSpan.FromSeconds(1);
        DateTime startTime = DateTime.UtcNow;

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(startTime + i * step.Value, source[i]), isNew: true);
        }
    }

    /// <summary>
    /// Static batch computation for TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int period = DefaultPeriod,
        double bandwidth = DefaultBandwidth, int length = DefaultLength)
    {
        Tbf tbf = new(period, bandwidth, length);
        return tbf.Update(source);
    }

    /// <summary>
    /// Static span-based batch computation.
    /// Outputs both truncated bandpass and standard bandpass.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> tbfOut,
        Span<double> bpOut,
        int period = DefaultPeriod,
        double bandwidth = DefaultBandwidth,
        int length = DefaultLength)
    {
        int n = source.Length;
        if (n != tbfOut.Length || n != bpOut.Length)
        {
            throw new ArgumentException("All spans must have the same length.", nameof(source));
        }
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (bandwidth < MinBandwidth)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth),
                $"Bandwidth must be at least {MinBandwidth}.");
        }
        if (length < MinLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length),
                $"Length must be at least {MinLength}.");
        }

        if (n == 0)
        {
            return;
        }

        // Compute coefficients
        double twoPi = 2.0 * Math.PI;
        double l1 = Math.Cos(twoPi / period);
        double g1 = Math.Cos(bandwidth * twoPi / period);
        double s1 = (1.0 / g1) - Math.Sqrt((1.0 / (g1 * g1)) - 1.0);

        double a0 = 0.5 * (1.0 - s1);
        double a1 = l1 * (1.0 + s1);
        double a2 = -s1;

        int bufSize = length + 2;

        // Price buffer (circular)
        Span<double> priceBuf = bufSize <= 256
            ? stackalloc double[bufSize]
            : new double[bufSize];
        int head = 0;
        int count = 0;

        // Standard BP state
        double bp1 = 0, bp2 = 0;
        double src1 = 0, src2 = 0;
        double lastValid = 0;
        int bars = 0;

        // Scratch for truncated computation
        Span<double> trunc = length + 3 <= 256
            ? stackalloc double[length + 3]
            : new double[length + 3];

        for (int i = 0; i < n; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
            {
                val = double.IsFinite(lastValid) ? lastValid : 0.0;
            }
            else
            {
                lastValid = val;
            }

            bars++;

            // Add to circular buffer
            int writeIdx = head;
            priceBuf[writeIdx] = val;
            head = (head + 1) % bufSize;
            if (count < bufSize)
            {
                count++;
            }

            // Standard BP
            double bpStd;
            if (bars <= 3)
            {
                bpStd = 0.0;
            }
            else
            {
                bpStd = Math.FusedMultiplyAdd(a0, val - src2,
                    Math.FusedMultiplyAdd(a1, bp1, a2 * bp2));
            }

            bp2 = bp1;
            bp1 = bpStd;
            src2 = src1;
            src1 = val;
            bpOut[i] = bpStd;

            // Truncated BP
            if (count < bufSize)
            {
                tbfOut[i] = 0.0;
                continue;
            }

            // Recompute truncated filter
            trunc[length + 2] = 0.0;
            trunc[length + 1] = 0.0;

            for (int c = length; c >= 1; c--)
            {
                // price k bars ago: most recent is at (head-1) mod bufSize,
                // k bars ago is at (head-1-k) mod bufSize
                int idxPrev = ((writeIdx - (c - 1)) % bufSize + bufSize) % bufSize;
                int idxNext = ((writeIdx - (c + 1)) % bufSize + bufSize) % bufSize;
                double pPrev = priceBuf[idxPrev];
                double pNext = priceBuf[idxNext];
                trunc[c] = Math.FusedMultiplyAdd(a0, pPrev - pNext,
                    Math.FusedMultiplyAdd(a1, trunc[c + 1], a2 * trunc[c + 2]));
            }

            tbfOut[i] = trunc[1];
        }
    }

    public static (TSeries Results, Tbf Indicator) Calculate(TSeries source,
        int period = DefaultPeriod, double bandwidth = DefaultBandwidth, int length = DefaultLength)
    {
        var indicator = new Tbf(period, bandwidth, length);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
