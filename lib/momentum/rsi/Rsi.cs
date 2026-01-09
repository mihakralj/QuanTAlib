using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RSI: Relative Strength Index
/// </summary>
/// <remarks>
/// RSI measures the speed and change of price movements.
///
/// Calculation:
/// RS = Average Gain / Average Loss
/// RSI = 100 - 100 / (1 + RS)
///
/// Average Gain/Loss are smoothed using RMA (Wilder's Smoothing).
///
/// Sources:
/// https://www.investopedia.com/terms/r/rsi.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Rsi : AbstractBase
{
    private readonly int _period;
    private readonly Rma _avgGain;
    private readonly Rma _avgLoss;
    private readonly TValuePublishedHandler _handler;
    private double _prevValue;
    private double _p_prevValue;

    public override bool IsHot => _avgGain.IsHot && _avgLoss.IsHot;

    public Rsi(int period = 14)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _avgGain = new Rma(period);
        _avgLoss = new Rma(period);
        _handler = Handle;
        _prevValue = double.NaN;
        _p_prevValue = double.NaN;

        Name = $"Rsi({period})";
        WarmupPeriod = period + 1;
    }

    public Rsi(ITValuePublisher source, int period = 14) : this(period)
    {
        source.Pub += _handler;
    }

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
        double gain = 0;
        double loss = 0;

        if (!double.IsNaN(_prevValue))
        {
            double change = val - _prevValue;
            if (change > 0)
            {
                gain = change;
            }
            else
            {
                loss = -change;
            }
        }

        if (isNew)
        {
            _prevValue = val;
        }

        // Update RMAs
        // Note: We pass isNew to RMAs.
        // If isNew=true, RMAs advance state.
        // If isNew=false, RMAs update current state.
        // However, gain/loss depend on _prevValue which we just managed.
        // If isNew=false, _prevValue was restored to _p_prevValue.
        // So change is calculated from the same previous bar.
        // This is correct.

        double avgGain = _avgGain.Update(new TValue(input.Time, gain), isNew).Value;
        double avgLoss = _avgLoss.Update(new TValue(input.Time, loss), isNew).Value;

        double rsi;
        const double epsilon = 1e-10;
        if (avgLoss < epsilon)
        {
            rsi = (avgGain < epsilon) ? 50 : 100;
        }
        else
        {
            double rs = avgGain / avgLoss;
            rsi = 100.0 - (100.0 / (1.0 + rs));
        }

        Last = new TValue(input.Time, rsi);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state for streaming
        // We need to replay at least period + 1 values
        // But since RMA is recursive, we ideally replay more.
        // Or we can just Reset and replay all if len is small, or last N if len is large.
        // For correctness with recursive indicators, replaying all is safest unless we have state export/import.
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

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period = 14)
    {
        var rsi = new Rsi(period);
        return rsi.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        double[] gains = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        double[] losses = System.Buffers.ArrayPool<double>.Shared.Rent(len);
        Span<double> gainSpan = gains.AsSpan(0, len);
        Span<double> lossSpan = losses.AsSpan(0, len);

        // Calculate gains and losses
        gainSpan[0] = 0;
        lossSpan[0] = 0;
        int i = 1;

        if (Vector.IsHardwareAccelerated && len > Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var vZero = Vector<double>.Zero;

            // Start from 1, but align to vector size if possible or just process chunks
            // Since we need i-1, we can load vectors at i and i-1
            for (; i <= len - vectorSize; i += vectorSize)
            {
                var vCurrent = new Vector<double>(source.Slice(i, vectorSize));
                var vPrev = new Vector<double>(source.Slice(i - 1, vectorSize));
                var vChange = vCurrent - vPrev;

                var vGain = Vector.Max(vChange, vZero);
                var vLoss = Vector.Max(-vChange, vZero);

                vGain.CopyTo(gainSpan.Slice(i, vectorSize));
                vLoss.CopyTo(lossSpan.Slice(i, vectorSize));
            }
        }

        for (; i < len; i++)
        {
            double change = source[i] - source[i - 1];
            if (change > 0)
            {
                gainSpan[i] = change;
                lossSpan[i] = 0;
            }
            else
            {
                gainSpan[i] = 0;
                lossSpan[i] = -change;
            }
        }

        // Smooth gains and losses using RMA (in-place is safe for sequential processing)
        Rma.Batch(gainSpan, gainSpan, period);
        Rma.Batch(lossSpan, lossSpan, period);

        // Calculate RSI
        i = 0;
        if (Vector.IsHardwareAccelerated && len >= Vector<double>.Count)
        {
            int vectorSize = Vector<double>.Count;
            var v100 = new Vector<double>(100.0);
            var v1 = Vector<double>.One;
            var v50 = new Vector<double>(50.0);
            var vEpsilon = new Vector<double>(1e-10);

            for (; i <= len - vectorSize; i += vectorSize)
            {
                var vGain = new Vector<double>(gainSpan.Slice(i, vectorSize));
                var vLoss = new Vector<double>(lossSpan.Slice(i, vectorSize));

                // Standard RSI calculation
                var vRs = vGain / vLoss;
                var vRsi = v100 - (v100 / (v1 + vRs));

                // Handle edge cases where loss is zero
                var vLossIsZero = Vector.LessThan(vLoss, vEpsilon);
                var vGainIsZero = Vector.LessThan(vGain, vEpsilon);

                // If loss is zero:
                // If gain is also zero -> 50
                // Else -> 100
                var vFlat = Vector.BitwiseAnd(vLossIsZero, vGainIsZero);

                // First set to 100 if loss is zero
                var vResult = Vector.ConditionalSelect(vLossIsZero, v100, vRsi);

                // Then set to 50 if both are zero
                vResult = Vector.ConditionalSelect(vFlat, v50, vResult);

                vResult.CopyTo(output.Slice(i, vectorSize));
            }
        }

        const double epsilon = 1e-10;
        for (; i < len; i++)
        {
            double avgGain = gainSpan[i];
            double avgLoss = lossSpan[i];

            if (avgLoss < epsilon)
            {
                output[i] = (avgGain < epsilon) ? 50 : 100;
            }
            else
            {
                double rs = avgGain / avgLoss;
                output[i] = 100.0 - (100.0 / (1.0 + rs));
            }
        }

        System.Buffers.ArrayPool<double>.Shared.Return(gains);
        System.Buffers.ArrayPool<double>.Shared.Return(losses);
    }

    public override void Reset()
    {
        _avgGain.Reset();
        _avgLoss.Reset();
        _prevValue = double.NaN;
        _p_prevValue = double.NaN;
        Last = default;
    }
}
