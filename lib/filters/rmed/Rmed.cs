using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Rmed : AbstractBase
{
    private const int MedianWindow = 5;

    private readonly double _alpha;
    private readonly double _decay; // 1 - alpha
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private int _index;

    // 5-bar circular buffer for median computation
    private readonly double[] _buf;
    private readonly double[] _pBuf;
    private int _head;
    private int _pHead;

    private State _s;
    private State _ps;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Rm;
        public double LastValue;
        public bool Initialized;
    }

    public int Period { get; }
    public double Alpha => _alpha;
    public override bool IsHot => _index >= WarmupPeriod;

    public Rmed(int period = 12)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        Period = period;

        // Ehlers alpha from cycle period: α = (cos θ + sin θ - 1) / cos θ, θ = 2π/P
        double angle = 2.0 * Math.PI / period;
        double cosA = Math.Cos(angle);
        double sinA = Math.Sin(angle);
        _alpha = Math.Clamp((cosA + sinA - 1.0) / cosA, 0.0, 1.0);
        _decay = 1.0 - _alpha;

        WarmupPeriod = MedianWindow;
        Name = $"Rmed({period})";

        _buf = new double[MedianWindow];
        _pBuf = new double[MedianWindow];

        Init();
    }

    public Rmed(TSeries source, int period = 12)
        : this(period)
    {
        _publisher = source;
        _handler = Sub;
        source.Pub += _handler;
    }

    private void Sub(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public void Init()
    {
        _index = 0;
        _head = 0;
        _pHead = 0;
        _s = default;
        _ps = default;
        Array.Clear(_buf);
        Array.Clear(_pBuf);
    }

    public override void Reset()
    {
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _pHead = _head;
            Array.Copy(_buf, _pBuf, MedianWindow);
            _index++;
        }
        else
        {
            _s = _ps;
            _head = _pHead;
            Array.Copy(_pBuf, _buf, MedianWindow);
        }

        var s = _s;

        double val = input.Value;
        if (double.IsNaN(val) || double.IsInfinity(val))
        {
            val = s.LastValue;
        }

        // Write into 5-bar circular buffer
        _buf[_head] = val;
        _head = (_head + 1) % MedianWindow;

        // Compute 5-bar median via sorting network (constant-time for 5 elements)
        double med5 = Median5(_buf);

        double result;
        if (!s.Initialized)
        {
            s.Rm = val;
            s.Initialized = true;
            result = val;
        }
        else
        {
            // RM = α·Med5 + (1-α)·RM_prev  → FMA: decay·RM_prev + α·Med5
            s.Rm = Math.FusedMultiplyAdd(_decay, s.Rm, _alpha * med5);
            result = s.Rm;
        }

        if (!double.IsNaN(val) && !double.IsInfinity(val))
        {
            s.LastValue = val;
        }

        _s = s;

        TValue output = new(input.Time, result);
        Last = output;
        PubEvent(output, isNew);
        return output;
    }

    public override TSeries Update(TSeries source)
    {
        var tsResult = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Batch(srcSpan, outArray.AsSpan(), Period);

        for (int i = 0; i < outArray.Length; i++)
        {
            tsResult.Add(new TValue(source.Times[i], outArray[i]));
        }

        if (srcSpan.Length > 0)
        {
            int replayStart = Math.Max(0, srcSpan.Length - Math.Max(WarmupPeriod, MedianWindow));
            Reset();
            for (int i = replayStart; i < srcSpan.Length; i++)
            {
                Update(new TValue(source.Times[i], srcSpan[i]), isNew: true);
            }
        }

        return tsResult;
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 12)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output lengths must match.", nameof(output));
        }
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        // Ehlers alpha
        double angle = 2.0 * Math.PI / period;
        double cosA = Math.Cos(angle);
        double sinA = Math.Sin(angle);
        double alpha = Math.Clamp((cosA + sinA - 1.0) / cosA, 0.0, 1.0);
        double decay = 1.0 - alpha;

        // Pre-clean source for NaN/Infinity (so median lookback matches streaming)
        int len = source.Length;
        double[] rented = ArrayPool<double>.Shared.Rent(len);
        Span<double> clean = rented.AsSpan(0, len);

        try
        {
            double lastClean = 0;
            for (int i = 0; i < len; i++)
            {
                double v = source[i];
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    clean[i] = lastClean;
                }
                else
                {
                    clean[i] = v;
                    lastClean = v;
                }
            }

            // 5-bar circular buffer
            Span<double> buf = stackalloc double[MedianWindow];
            buf.Clear();
            int head = 0;
            double rm = 0;
            bool initialized = false;

            for (int i = 0; i < len; i++)
            {
                double val = clean[i];

                buf[head] = val;
                head = (head + 1) % MedianWindow;

                double med5 = Median5Span(buf);

                if (!initialized)
                {
                    rm = val;
                    initialized = true;
                }
                else
                {
                    rm = Math.FusedMultiplyAdd(decay, rm, alpha * med5);
                }

                output[i] = rm;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rented);
        }
    }

    public static TSeries Batch(TSeries source, int period = 12)
    {
        var result = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Batch(srcSpan, outArray.AsSpan(), period);

        for (int i = 0; i < outArray.Length; i++)
        {
            result.Add(new TValue(source.Times[i], outArray[i]));
        }

        return result;
    }

    public static (TSeries Results, Rmed Indicator) Calculate(TSeries source, int period = 12)
    {
        var indicator = new Rmed(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Median of exactly 5 elements using a sorting network (9 compare-swaps).
    /// Constant-time, branch-based, zero-allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Median5(double[] a)
    {
        double a0 = a[0], a1 = a[1], a2 = a[2], a3 = a[3], a4 = a[4];
        return Median5Core(a0, a1, a2, a3, a4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Median5Span(Span<double> a)
    {
        double a0 = a[0], a1 = a[1], a2 = a[2], a3 = a[3], a4 = a[4];
        return Median5Core(a0, a1, a2, a3, a4);
    }

    /// <summary>
    /// Optimal 6-comparison median-of-5 algorithm.
    /// Based on the known optimal comparison network for median selection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Median5Core(double a0, double a1, double a2, double a3, double a4)
    {
        // Sort pairs to guarantee a0<=a1, a3<=a4
        if (a0 > a1) { (a0, a1) = (a1, a0); }
        if (a3 > a4) { (a3, a4) = (a4, a3); }

        // Ensure the pair with smaller minimum comes first: a0<=a3
        if (a0 > a3) { (a0, a3) = (a3, a0); (a1, a4) = (a4, a1); }

        // a0 is now the global minimum → discard it
        // Median is among {a1, a2, a3, a4}, find median of middle two

        // Compare a2 with a3
        if (a2 > a3) { (a2, a3) = (a3, a2); }

        // Median = min(a1, a3) means: the answer is max(min-of-pairs)
        // But more precisely: median = min(max(a1,a2), a3)
        double maxA1A2 = a1 > a2 ? a1 : a2;
        return maxA1A2 < a3 ? maxA1A2 : a3;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
