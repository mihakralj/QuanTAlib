using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// MSTOCH: Ehlers MESA Stochastic.
/// Three-stage pipeline: (1) Roofing Filter = 2-pole Butterworth HP + Super Smoother,
/// (2) Standard stochastic on roofing-filtered data,
/// (3) Super Smoother of stochastic output. Result clamped to [0,1].
/// All IIR stages are O(1); only the min/max scan in stage 2 is O(stochLength).
/// Reference: John F. Ehlers, "Cycle Analytics for Traders" (2013), Chapter 6.
/// </summary>
[SkipLocalsInit]
public sealed class Mstoch : ITValuePublisher
{
    private readonly int _stochLength;
    private readonly int _hpLength;
    private readonly int _ssLength;

    // Precomputed IIR coefficients (readonly — fixed at construction)
    private readonly double _hpC1;
    private readonly double _hpC2;
    private readonly double _hpC3;
    private readonly double _ssC1;
    private readonly double _ssC2;
    private readonly double _ssC3;

    // Ring buffer for Filt values (stage-2 stochastic window)
    private readonly double[] _filtBuf;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Src1,         // src[t-1]
        double Src2,         // src[t-2]
        double Hp1,          // HP[t-1]
        double Hp2,          // HP[t-2]
        double Filt1,        // Filt[t-1]
        double Filt2,        // Filt[t-2]
        double Stoc1,        // stoc[t-1]  (for stage-3 input average)
        double Mstoc1,       // mstoc[t-1]
        double Mstoc2,       // mstoc[t-2]
        double LastValidSrc, // NaN substitution
        int BufHead,         // ring buffer write head
        int Count);          // bars seen

    private State _s;
    private State _ps;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _s.Count >= WarmupPeriod;

    public event TValuePublishedHandler? Pub;

    public Mstoch(int stochLength = 20, int hpLength = 48, int ssLength = 10)
    {
        if (stochLength < 2)
        {
            throw new ArgumentException("Stochastic length must be >= 2", nameof(stochLength));
        }
        if (hpLength < 1)
        {
            throw new ArgumentException("HP length must be >= 1", nameof(hpLength));
        }
        if (ssLength < 1)
        {
            throw new ArgumentException("SS length must be >= 1", nameof(ssLength));
        }

        _stochLength = stochLength;
        _hpLength = hpLength;
        _ssLength = ssLength;

        // Precompute HP coefficients
        double hpArg = Sqrt(2.0) * PI / hpLength;
        double hpExp = Exp(-hpArg);
        _hpC2 = 2.0 * hpExp * Cos(hpArg);
        _hpC3 = -(hpExp * hpExp);
        _hpC1 = (1.0 + _hpC2 - _hpC3) / 4.0;

        // Precompute Super Smoother coefficients
        double ssArg = Sqrt(2.0) * PI / ssLength;
        double ssExp = Exp(-ssArg);
        _ssC2 = 2.0 * ssExp * Cos(ssArg);
        _ssC3 = -(ssExp * ssExp);
        _ssC1 = 1.0 - _ssC2 - _ssC3;

        _filtBuf = new double[stochLength];

        _s = new State(0, 0, 0, 0, 0, 0, 0.5, 0.5, 0.5, double.NaN, 0, 0);
        _ps = _s;

        Name = $"Mstoch({stochLength},{hpLength},{ssLength})";
        WarmupPeriod = stochLength + ssLength + 2; // conservative estimate
    }

    public Mstoch(ITValuePublisher source, int stochLength = 20, int hpLength = 48, int ssLength = 10)
        : this(stochLength, hpLength, ssLength)
    {
        source.Pub += (object? _, in TValueEventArgs e) => Update(e.Value, e.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double src = input.Value;
        if (double.IsFinite(src))
        {
            s.LastValidSrc = src;
        }
        else
        {
            src = double.IsNaN(s.LastValidSrc) ? 0.0 : s.LastValidSrc;
        }

        // === Stage 1: Highpass (2-pole Butterworth, removes trend) ===
        // HP = c1*(src - 2*src1 + src2) + c2*hp1 + c3*hp2
        double hp = Math.FusedMultiplyAdd(
            _hpC1, src - (2.0 * s.Src1) + s.Src2,
            Math.FusedMultiplyAdd(_hpC2, s.Hp1, _hpC3 * s.Hp2));

        // === Stage 1: Super Smoother of HP =>  Filt ===
        // Filt = c1*(hp + hp1)/2 + c2*filt1 + c3*filt2
        double filtIn = (hp + s.Hp1) * 0.5;
        double filt = Math.FusedMultiplyAdd(
            _ssC1, filtIn,
            Math.FusedMultiplyAdd(_ssC2, s.Filt1, _ssC3 * s.Filt2));

        // === Stage 2: Stochastic on Filt ring buffer ===
        int head = s.BufHead;
        _filtBuf[head] = filt;

        int count = s.Count + (isNew ? 1 : 0);
        if (isNew)
        {
            s.Count = count;
            s.BufHead = (head + 1) % _stochLength;
        }

        int filled = Min(count, _stochLength);

        double highestC = filt;
        double lowestC = filt;
        int startHead = isNew ? s.BufHead : head; // new head after increment
        for (int i = 0; i < filled; i++)
        {
            int idx = (startHead - 1 - i + _stochLength) % _stochLength;
            // For isNew path, startHead = new s.BufHead, so idx wraps correctly
            double val = _filtBuf[idx];
            if (val > highestC) { highestC = val; }
            if (val < lowestC) { lowestC = val; }
        }

        double rangeVal = highestC - lowestC;
        double stoc = rangeVal > 0.0 ? (filt - lowestC) / rangeVal : 0.5;

        // === Stage 3: Super Smoother of stochastic ===
        double mstocIn = (stoc + s.Stoc1) * 0.5;
        double mstoc = Math.FusedMultiplyAdd(
            _ssC1, mstocIn,
            Math.FusedMultiplyAdd(_ssC2, s.Mstoc1, _ssC3 * s.Mstoc2));

        double result = Max(0.0, Min(1.0, mstoc));

        // Update state
        s.Src2 = s.Src1;
        s.Src1 = src;
        s.Hp2 = s.Hp1;
        s.Hp1 = hp;
        s.Filt2 = s.Filt1;
        s.Filt1 = filt;
        s.Stoc1 = stoc;
        s.Mstoc2 = s.Mstoc1;
        s.Mstoc1 = mstoc;

        _s = s;

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var vSpan = CollectionsMarshal.AsSpan(v);
        Batch(source.Values, vSpan, _stochLength, _hpLength, _ssLength);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        // Prime internal state for continued streaming from the last bars
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
    }

    public void Reset()
    {
        Array.Clear(_filtBuf);
        _s = new State(0, 0, 0, 0, 0, 0, 0.5, 0.5, 0.5, double.NaN, 0, 0);
        _ps = _s;
        Last = default;
    }

    // === Static Batch (span) ===
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> src,
        Span<double> output,
        int stochLength = 20,
        int hpLength = 48,
        int ssLength = 10)
    {
        if (stochLength < 2)
        {
            throw new ArgumentException("Stochastic length must be >= 2", nameof(stochLength));
        }
        if (hpLength < 1)
        {
            throw new ArgumentException("HP length must be >= 1", nameof(hpLength));
        }
        if (ssLength < 1)
        {
            throw new ArgumentException("SS length must be >= 1", nameof(ssLength));
        }
        if (output.Length < src.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input", nameof(output));
        }

        int len = src.Length;
        if (len == 0)
        {
            return;
        }

        // Precompute coefficients
        double hpArg = Sqrt(2.0) * PI / hpLength;
        double hpExp = Exp(-hpArg);
        double hpC2 = 2.0 * hpExp * Cos(hpArg);
        double hpC3 = -(hpExp * hpExp);
        double hpC1 = (1.0 + hpC2 - hpC3) / 4.0;

        double ssArg = Sqrt(2.0) * PI / ssLength;
        double ssExp = Exp(-ssArg);
        double ssC2 = 2.0 * ssExp * Cos(ssArg);
        double ssC3 = -(ssExp * ssExp);
        double ssC1 = 1.0 - ssC2 - ssC3;

        const int StackallocThreshold = 256;
        double[]? rentedFilt = null;
        double[]? rentedBuf = null;
        scoped Span<double> filtArr;
        scoped Span<double> filtBuf;

        if (len <= StackallocThreshold)
        {
            filtArr = stackalloc double[len];
        }
        else
        {
            rentedFilt = ArrayPool<double>.Shared.Rent(len);
            filtArr = rentedFilt.AsSpan(0, len);
        }

        if (stochLength <= StackallocThreshold)
        {
            filtBuf = stackalloc double[stochLength];
        }
        else
        {
            rentedBuf = ArrayPool<double>.Shared.Rent(stochLength);
            filtBuf = rentedBuf.AsSpan(0, stochLength);
        }
        filtBuf.Clear();

        try
        {
            // Pass 1: compute HP + Filt for all bars
            double prevSrc2 = 0.0, prevSrc1 = 0.0;
            double prevHp2 = 0.0, prevHp1 = 0.0;
            double prevFilt2 = 0.0, prevFilt1 = 0.0;

            for (int i = 0; i < len; i++)
            {
                double srcVal = src[i];
                double s;
                if (double.IsFinite(srcVal))
                {
                    s = srcVal;
                }
                else if (i > 0)
                {
                    s = src[i - 1];
                }
                else
                {
                    s = 0.0;
                }

                double hp = Math.FusedMultiplyAdd(
                    hpC1, s - (2.0 * prevSrc1) + prevSrc2,
                    Math.FusedMultiplyAdd(hpC2, prevHp1, hpC3 * prevHp2));

                double filtIn = (hp + prevHp1) * 0.5;
                double filt = Math.FusedMultiplyAdd(
                    ssC1, filtIn,
                    Math.FusedMultiplyAdd(ssC2, prevFilt1, ssC3 * prevFilt2));

                filtArr[i] = filt;

                prevSrc2 = prevSrc1;
                prevSrc1 = s;
                prevHp2 = prevHp1;
                prevHp1 = hp;
                prevFilt2 = prevFilt1;
                prevFilt1 = filt;
            }

            // Pass 2: stochastic + super smoother
            int bufHead = 0;
            double prevStoc1 = 0.5;
            double prevMstoc2 = 0.5, prevMstoc1 = 0.5;

            for (int i = 0; i < len; i++)
            {
                double filt = filtArr[i];
                filtBuf[bufHead] = filt;
                bufHead = (bufHead + 1) % stochLength;

                int filled = Min(i + 1, stochLength);
                double highestC = filt;
                double lowestC = filt;
                for (int k = 0; k < filled; k++)
                {
                    int idx = (bufHead - 1 - k + stochLength) % stochLength;
                    double val = filtBuf[idx];
                    if (val > highestC) { highestC = val; }
                    if (val < lowestC) { lowestC = val; }
                }

                double rangeVal = highestC - lowestC;
                double stoc = rangeVal > 0.0 ? (filt - lowestC) / rangeVal : 0.5;

                double mstocIn = (stoc + prevStoc1) * 0.5;
                double mstoc = Math.FusedMultiplyAdd(
                    ssC1, mstocIn,
                    Math.FusedMultiplyAdd(ssC2, prevMstoc1, ssC3 * prevMstoc2));

                output[i] = Max(0.0, Min(1.0, mstoc));

                prevStoc1 = stoc;
                prevMstoc2 = prevMstoc1;
                prevMstoc1 = mstoc;
            }
        }
        finally
        {
            if (rentedFilt != null)
            {
                ArrayPool<double>.Shared.Return(rentedFilt);
            }
            if (rentedBuf != null)
            {
                ArrayPool<double>.Shared.Return(rentedBuf);
            }
        }
    }

    public static TSeries Batch(TSeries source, int stochLength = 20, int hpLength = 48, int ssLength = 10)
    {
        if (source == null || source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.Values, CollectionsMarshal.AsSpan(v), stochLength, hpLength, ssLength);
        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    public static (TSeries Result, Mstoch Indicator) Calculate(
        TSeries source, int stochLength = 20, int hpLength = 48, int ssLength = 10)
    {
        var indicator = new Mstoch(stochLength, hpLength, ssLength);
        var result = indicator.Update(source);
        return (result, indicator);
    }
}
