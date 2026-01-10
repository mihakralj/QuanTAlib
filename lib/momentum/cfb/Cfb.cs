using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CFB: Jurik Composite Fractal Behavior (Trend Duration Index)
/// </summary>
/// <remarks>
/// CFB measures the duration of a trend by analyzing fractal efficiency across multiple time scales.
/// It calculates a composite index based on which lookback periods show "quality" trending behavior.
///
/// Key characteristics:
/// - Adaptive: Adjusts to market fractal patterns.
/// - Granular: Uses a dense array of lookback lengths for smooth transitions.
/// - Composite: Weighted average of qualifying trend lengths.
/// - Zero-lag: Designed to modulate other indicators with minimal latency.
///
/// Calculation:
/// 1. For each length L:
///    Ratio = NetMove(L) / TotalVolatility(L)
///    where NetMove = Abs(Price - Price[L ago])
///    and TotalVolatility = Sum(Abs(Price[i] - Price[i-1])) over L bars.
/// 2. Filter: Only consider lengths where Ratio > Threshold (0.25).
/// 3. Composite: Weighted average of qualifying lengths (Weight = Ratio).
/// 4. Decay: If no trend found, decay the previous CFB value.
/// </remarks>
[SkipLocalsInit]
public sealed class Cfb : ITValuePublisher
{
    private readonly int[] _lengths;
    private readonly int _maxLen;
    private readonly RingBuffer _prices;
    private readonly RingBuffer _volatility;
    private readonly double[] _runningSums;
    private readonly double[] _p_runningSums;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double PrevCfb, double LastPrice, double LastValidValue);
    private State _state;
    private State _p_state;
    private readonly TValuePublishedHandler _handler;

    public string Name { get; }
    public event TValuePublishedHandler? Pub;
    public TValue Last { get; private set; }
    public bool IsHot => _prices.IsFull;
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates a CFB indicator with specified fractal lengths.
    /// </summary>
    /// <param name="lengths">Array of lookback lengths. If null, defaults to 2, 4, ..., 192.</param>
    public Cfb(int[]? lengths = null)
    {
        if (lengths == null || lengths.Length == 0)
        {
            // Default dense array: 2, 4, 6, ..., 192
            _lengths = new int[96];
            for (int i = 0; i < 96; i++)
            {
                _lengths[i] = (i + 1) * 2;
            }
        }
        else
        {
            _lengths = (int[])lengths.Clone();
            Array.Sort(_lengths);
        }

        _maxLen = _lengths[^1];
        WarmupPeriod = _maxLen;

        // We need maxLen + 1 capacity to handle the lookback correctly
        // _prices stores raw prices
        // _volatility stores bar-to-bar changes. _volatility[i] = Abs(Price[i] - Price[i-1])
        _prices = new RingBuffer(_maxLen + 1);
        _volatility = new RingBuffer(_maxLen + 1);

        _runningSums = new double[_lengths.Length];
        _p_runningSums = new double[_lengths.Length];

        Name = "Jurik Composite Fractal Behavior";
        _handler = Handle;
        _state.PrevCfb = 1.0;
    }

    public Cfb(ITValuePublisher source, int[]? lengths = null) : this(lengths)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _prices.Clear();
        _volatility.Clear();
        Array.Clear(_runningSums);
        Array.Clear(_p_runningSums);
        _state = default;
        _state.PrevCfb = 1.0;
        _p_state = default;
        _p_state.PrevCfb = 1.0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double price = input.Value;

        if (isNew)
        {
            // Save state
            _p_state = _state;
            Array.Copy(_runningSums, _p_runningSums, _lengths.Length);
        }
        else
        {
            // Restore state
            _state = _p_state;
            Array.Copy(_p_runningSums, _runningSums, _lengths.Length);
        }

        if (!double.IsFinite(price))
        {
            price = _state.LastValidValue;
        }
        else
        {
            _state.LastValidValue = price;
        }

        // Calculate volatility for this step
        double vol = 0.0;
        if (_prices.Count > 0)
        {
            vol = Math.Abs(price - _state.LastPrice);
        }

        // Update buffers
        if (isNew)
        {
            _prices.Add(price);
            _volatility.Add(vol);
        }
        else
        {
            _prices.UpdateNewest(price);
            _volatility.UpdateNewest(vol);
        }
        _state.LastPrice = price;

        double sumWeightedLen = 0.0;
        double sumWeights = 0.0;
        int count = _prices.Count;

        // Update running sums and calculate ratios
        for (int i = 0; i < _lengths.Length; i++)
        {
            int L = _lengths[i];

            // Update running sum of volatility
            // We always add the new volatility
            // We only subtract if we have enough history

            double volToRemove = 0.0;
            if (count > L)
            {
                volToRemove = _volatility[count - 1 - L];
            }

            _runningSums[i] += vol - volToRemove;

            if (count <= L) continue;

            // Safety check for very small volatility
            if (_runningSums[i] < 1e-12) continue;

            // Net move over L bars
            // Price at Count-1 is current. Price at Count-1-L is L bars ago.
            double netMove = Math.Abs(price - _prices[count - 1 - L]);

            double ratio = netMove / _runningSums[i];

            if (ratio >= 0.25)
            {
                sumWeightedLen += L * ratio;
                sumWeights += ratio;
            }
        }

        double cfb;
        if (sumWeights > 0.25)
        {
            cfb = sumWeightedLen / sumWeights;
        }
        else
        {
            // Decay
            cfb = (_state.PrevCfb > 1.0) ? _state.PrevCfb * 0.5 : 1.0;
        }

        if (cfb < 1.0) cfb = 1.0;

        // Round to nearest integer
        cfb = Math.Round(cfb);
        if (cfb < 1.0) cfb = 1.0;

        _state.PrevCfb = cfb;

        Last = new TValue(input.Time, cfb);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _lengths);
        source.Times.CopyTo(tSpan);

        // Restore state logic would go here if needed for continuity,
        // but for batch processing we usually just return the result.
        // To properly support "Update(TValue)" after "Update(TSeries)", we would need to
        // replay the last MaxLen bars to populate the buffers.

        // Replay last MaxLen bars to restore state
        int replayStart = Math.Max(0, len - _maxLen - 1);
        _prices.Clear();
        _volatility.Clear();
        Array.Clear(_runningSums);
        _state = default;
        _state.PrevCfb = 1.0;

        // We need to re-run the update logic for the replay window to populate running sums correctly
        // This is expensive but necessary for correct state restoration.
        // For the purpose of this implementation, we will just ensure the buffers are populated.

        for (int i = replayStart; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    public static TSeries Batch(TSeries source, int[]? lengths = null)
    {
        var cfb = new Cfb(lengths);
        return cfb.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int[]? lengths = null)
    {
        int len = source.Length;
        if (len == 0)
            return;

        if (output.Length != len)
            throw new ArgumentException("Source and output must have the same length", nameof(output));

        // Setup lengths
        int[] lens;
        if (lengths == null || lengths.Length == 0)
        {
            lens = new int[96];
            for (int i = 0; i < 96; i++)
            {
                lens[i] = (i + 1) * 2;
            }
        }
        else
        {
            // We do not mutate lens, so cloning is unnecessary.
            lens = lengths;
        }

        const int StackallocThreshold = 256;

        // Pre-calculate volatility for the whole series:
        // vol[i] = Abs(source[i] - source[i-1])
        Span<double> vol = len <= StackallocThreshold
            ? stackalloc double[len]
            : new double[len];

        vol[0] = 0.0;
        for (int i = 1; i < len; i++)
        {
            vol[i] = Math.Abs(source[i] - source[i - 1]);
        }

        // Running sums for each length.
        Span<double> runningSums = lens.Length <= StackallocThreshold
            ? stackalloc double[lens.Length]
            : new double[lens.Length];

        runningSums.Clear();

        double prevCfb = 1.0;

        for (int i = 0; i < len; i++)
        {
            double price = source[i];
            double currentVol = vol[i];

            double sumWeightedLen = 0.0;
            double sumWeights = 0.0;

            // For very first bars where i < minLen, result is 1
            if (i < lens[0])
            {
                output[i] = 1.0;
                // Still need to update running sums if possible, but we can't really until we have enough data
                // Actually we can accumulate volatility.
                for (int k = 0; k < lens.Length; k++)
                {
                    runningSums[k] += currentVol;
                }
                continue;
            }

            for (int k = 0; k < lens.Length; k++)
            {
                int L = lens[k];

                // Update running sum
                runningSums[k] += currentVol;
                if (i > L)
                {
                    runningSums[k] -= vol[i - L];
                }

                if (i < L) continue;

                double totalMove = runningSums[k];
                if (totalMove < 1e-12) continue;

                double netMove = Math.Abs(price - source[i - L]);
                double ratio = netMove / totalMove;

                if (ratio >= 0.25)
                {
                    sumWeightedLen += L * ratio;
                    sumWeights += ratio;
                }
            }

            double cfb;
            if (sumWeights > 0.25)
            {
                cfb = sumWeightedLen / sumWeights;
            }
            else
            {
                cfb = (prevCfb > 1.0) ? prevCfb * 0.5 : 1.0;
            }

            if (cfb < 1.0) cfb = 1.0;
            cfb = Math.Round(cfb);
            if (cfb < 1.0) cfb = 1.0;

            output[i] = cfb;
            prevCfb = cfb;
        }
    }
}
