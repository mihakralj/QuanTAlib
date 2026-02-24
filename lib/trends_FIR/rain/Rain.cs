using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RAIN: Rainbow Moving Average
/// </summary>
/// <remarks>
/// Ten cascaded SMA layers with weighted composite. Layers 1–4 receive
/// weights 5, 4, 3, 2 and layers 5–10 each receive weight 1 (divisor = 20).
/// Each SMA layer uses O(1) running-sum via RingBuffer.
///
/// Calculation: <c>RAIN = (5·SMA₁ + 4·SMA₂ + 3·SMA₃ + 2·SMA₄ + SMA₅ + SMA₆ + SMA₇ + SMA₈ + SMA₉ + SMA₁₀) / 20</c>
/// </remarks>
/// <seealso href="Rain.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Rain : AbstractBase
{
    private const int LayerCount = 10;
    private const double InvTotalWeight = 1.0 / 20.0; // 5+4+3+2+1+1+1+1+1+1 = 20

    private readonly int _period;
    private readonly Sma[] _layers;
    private readonly TValuePublishedHandler _handler;
    private ITValuePublisher? _publisher;
    private bool _disposed;

    // Weights: layers 1-4 get 5,4,3,2; layers 5-10 get 1 each
    private static ReadOnlySpan<double> Weights =>
    [
        5.0, 4.0, 3.0, 2.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0
    ];

    /// <summary>
    /// Creates RAIN with specified period for each SMA layer.
    /// </summary>
    /// <param name="period">Lookback window for each SMA layer (must be &gt; 0)</param>
    public Rain(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _layers = new Sma[LayerCount];
        for (int i = 0; i < LayerCount; i++)
        {
            _layers[i] = new Sma(period);
        }

        Name = $"Rain({period})";
        // All 10 layers share the same period; the cascade needs 10*period bars
        // for full convergence, but the first layer is "hot" after 'period' bars.
        // We define WarmupPeriod as the point where ALL layers are hot.
        WarmupPeriod = period * LayerCount;
        _handler = Handle;
    }

    public Rain(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        source.Pub += _handler;
    }

    public Rain(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _publisher = source;
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override bool IsHot
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // Hot when all 10 layers have full buffers
            for (int i = 0; i < LayerCount; i++)
            {
                if (!_layers[i].IsHot)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        // Reset all layers
        for (int i = 0; i < LayerCount; i++)
        {
            _layers[i].Reset();
        }

        // Prime layer 0 directly
        _layers[0].Prime(source);

        // For each subsequent layer, compute intermediate series then prime
        double[] tempA = ArrayPool<double>.Shared.Rent(source.Length);
        double[] tempB = ArrayPool<double>.Shared.Rent(source.Length);
        try
        {
            Span<double> current = tempA.AsSpan(0, source.Length);
            Span<double> next = tempB.AsSpan(0, source.Length);

            // Compute layer 0 output
            Sma.Batch(source, current, _period);

            for (int layer = 1; layer < LayerCount; layer++)
            {
                // Prime this layer from the previous layer's output
                _layers[layer].Prime(current);

                if (layer < LayerCount - 1)
                {
                    // Compute this layer's output for the next layer
                    Sma.Batch(current, next, _period);
                    // Swap buffers
                    var tmp = current;
                    current = next;
                    next = tmp;
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(tempA);
            ArrayPool<double>.Shared.Return(tempB);
        }

        // Compute the final RAIN value from all layers' Last values
        double result = ComputeWeightedAverage();
        Last = new TValue(DateTime.MinValue, result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Cascade through all 10 SMA layers
        TValue current = input;
        for (int i = 0; i < LayerCount; i++)
        {
            current = _layers[i].Update(current, isNew);
        }

        // Weighted average of all 10 layer outputs
        double result = ComputeWeightedAverage();
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

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var rain = new Rain(period);
        return rain.Update(source);
    }

    /// <summary>
    /// Calculates RAIN in-place using 10 cascaded SMA passes with weighted average.
    /// Uses double-buffered ArrayPool for intermediate layers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // We need to store all 10 layer outputs for the weighted average.
        // Layers are computed sequentially: layer[i] = SMA(layer[i-1], period).
        // We accumulate the weighted sum as we go to minimize memory.
        //
        // Strategy: keep two temp buffers + accumulate weighted sum into output.
        double[] rentA = ArrayPool<double>.Shared.Rent(len);
        double[] rentB = ArrayPool<double>.Shared.Rent(len);
        try
        {
            Span<double> current = rentA.AsSpan(0, len);
            Span<double> next = rentB.AsSpan(0, len);
            ReadOnlySpan<double> weights = Weights;

            // Layer 0: SMA(source, period)
            Sma.Batch(source, current, period);

            // Initialize output with weight[0] * layer[0]
            double w0 = weights[0]; // 5.0
            for (int i = 0; i < len; i++)
            {
                output[i] = w0 * current[i];
            }

            // Layers 1..9
            for (int layer = 1; layer < LayerCount; layer++)
            {
                Sma.Batch(current, next, period);
                double w = weights[layer];

                // Accumulate weighted contribution
                // skipcq: CS-R1140 - FMA accumulation is correct inline; splitting fragments the pipeline
                for (int i = 0; i < len; i++)
                {
                    output[i] = Math.FusedMultiplyAdd(w, next[i], output[i]);
                }

                // Swap for next iteration
                var tmp = current;
                current = next;
                next = tmp;
            }

            // Final division by total weight (20)
            for (int i = 0; i < len; i++)
            {
                output[i] *= InvTotalWeight;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentA);
            ArrayPool<double>.Shared.Return(rentB);
        }
    }

    public static (TSeries Results, Rain Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Rain(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeWeightedAverage()
    {
        ReadOnlySpan<double> weights = Weights;

        // Unrolled weighted sum for 10 layers
        double sum = weights[0] * _layers[0].Last.Value;
        sum = Math.FusedMultiplyAdd(weights[1], _layers[1].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[2], _layers[2].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[3], _layers[3].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[4], _layers[4].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[5], _layers[5].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[6], _layers[6].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[7], _layers[7].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[8], _layers[8].Last.Value, sum);
        sum = Math.FusedMultiplyAdd(weights[9], _layers[9].Last.Value, sum);

        return sum * InvTotalWeight;
    }

    public override void Reset()
    {
        for (int i = 0; i < LayerCount; i++)
        {
            _layers[i].Reset();
        }
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _publisher != null)
            {
                _publisher.Pub -= _handler;
                _publisher = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
