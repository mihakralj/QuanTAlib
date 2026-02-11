using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Defines the smoothing method applied to the final STC output.
/// </summary>
public enum StcSmoothing { None = 0, Ema = 1, Sigmoid = 2, Digital = 3 }

/// <summary>
/// STC: Schaff Trend Cycle - A cycle oscillator that combines MACD and Stochastic to detect market trends with improved speed and accuracy.
/// </summary>
/// <remarks>
/// The Schaff Trend Cycle (STC), developed by Doug Schaff, is an oscillator that moves between 0 and 100.
/// It identifies market trends and cycles by applying a Stochastic calculation to the MACD line,
/// and then smoothing the result. This results in an indicator that is faster than MACD and smoother than Stochastic.
///
/// Algorithm:
/// 1. Calculate MACD = Exponential Moving Average (Fast) - Exponential Moving Average (Slow).
/// 2. Calculate %K (Stoch K) of the MACD over a specified period.
/// 3. Smooth %K with a fast average to get %D (Stoch D).
/// 4. Re-calculate %K of the %D value (Stoch of Stoch).
/// 5. Smooth the result again to produce the final STC value.
///
/// Properties:
/// - Ranges from 0 to 100.
/// - High values (>75) indicate overbought conditions.
/// - Low values (<25) indicate oversold conditions.
/// - Signals are generated when the indicator crosses these thresholds.
/// - Minimizes false signals found in traditional MACD or Stochastic indicators.
///
/// Key Insight:
/// By performing a double stochastic calculation on the MACD (Stochastic of the Stochastic of MACD),
/// STC emphasizes the cyclic nature of trends while reducing noise.
/// </remarks>
[SkipLocalsInit]
public sealed class Stc : AbstractBase
{
    private readonly StcSmoothing _smoothing;

    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly double _dAlpha;

    private readonly RingBuffer _macdBuf;
    private readonly RingBuffer _stoch1Buf;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private bool _isNew;

    [StructLayout(LayoutKind.Sequential)]
    private record struct State
    {
        public double FastEma;
        public double SlowEma;
        public double Stoch1Ema;
        public double Stoch2Ema;
        public double PrevStc;
        public double LastFiniteInput;
        public bool HasFiniteInput;

        public double MacdMin;
        public double MacdMax;
        public double Stoch1Min;
        public double Stoch1Max;
    }

    private State _s, _ps;
    private int _samples;

    public Stc(
        int kPeriod = 10,
        int dPeriod = 3,
        int fastLength = 23,
        int slowLength = 50,
        StcSmoothing smoothing = StcSmoothing.Ema)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(kPeriod, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(dPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(fastLength, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(slowLength, 2);

        _smoothing = smoothing;

        _fastAlpha = 2.0 / (fastLength + 1.0);
        _slowAlpha = 2.0 / (slowLength + 1.0);
        _dAlpha = 2.0 / (dPeriod + 1.0);

        int bufSize = kPeriod;
        _macdBuf = new RingBuffer(bufSize);
        _stoch1Buf = new RingBuffer(bufSize);

        Name = $"Stc(k={kPeriod},d={dPeriod},fast={fastLength},slow={slowLength},{smoothing})";
        WarmupPeriod = slowLength + bufSize;

        Reset();
    }

    public Stc(ITValuePublisher source, int kPeriod = 10, int dPeriod = 3, int fastLength = 23, int slowLength = 50, StcSmoothing smoothing = StcSmoothing.Ema)
        : this(kPeriod, dPeriod, fastLength, slowLength, smoothing)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public bool IsNew => _isNew;
    public override bool IsHot => _samples >= WarmupPeriod;

    public override void Reset()
    {
        _s = new State
        {
            FastEma = double.NaN,
            SlowEma = double.NaN,
            Stoch1Ema = double.NaN,
            Stoch2Ema = double.NaN,
            PrevStc = double.NaN,
            LastFiniteInput = double.NaN,
            HasFiniteInput = false,
            MacdMin = double.PositiveInfinity,
            MacdMax = double.NegativeInfinity,
            Stoch1Min = double.PositiveInfinity,
            Stoch1Max = double.NegativeInfinity,
        };
        _ps = _s;
        _samples = 0;
        _macdBuf.Clear();
        _stoch1Buf.Clear();
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Clamp100(double x)
    {
        if (double.IsNaN(x))
        {
            return x;
        }

        return Math.Clamp(x, 0, 100);
    }

    /// <summary>
    /// Applies final smoothing to stoch2Raw based on smoothing mode.
    /// Shared between Update() and Calculate() to eliminate duplication.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplySmoothing(double stoch2Raw, StcSmoothing smoothing, double dAlpha, ref double stoch2Ema, ref double prevStc)
    {
        double stc;
        switch (smoothing)
        {
            case StcSmoothing.Ema:
                stoch2Ema = double.IsNaN(stoch2Ema)
                    ? stoch2Raw
                    : Math.FusedMultiplyAdd(dAlpha, stoch2Raw - stoch2Ema, stoch2Ema);
                stc = Clamp100(stoch2Ema);
                break;

            case StcSmoothing.Sigmoid:
                stc = 100.0 / (1.0 + Math.Exp(-0.1 * (stoch2Raw - 50.0)));
                break;

            case StcSmoothing.Digital:
                if (stoch2Raw > 75)
                {
                    stc = 100;
                }
                else if (stoch2Raw < 25)
                {
                    stc = 0;
                }
                else
                {
                    stc = double.IsNaN(prevStc) ? stoch2Raw : prevStc;
                }

                break;

            default: // Includes StcSmoothing.None
                stc = stoch2Raw;
                break;
        }
        prevStc = stc;
        return stc;
    }

    /// <summary>
    /// Updates min/max tracking for a sliding window.
    /// Returns true if a full rescan is needed (removed value was at boundary).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UpdateMinMaxCore(double added, double removed, bool hasRemoved, ref double min, ref double max)
    {
        if (double.IsNaN(added))
        {
            return false;
        }

        bool expandMin = added < min;
        bool expandMax = added > max;

        if (!hasRemoved)
        {
            if (expandMin)
            {
                min = added;
            }

            if (expandMax)
            {
                max = added;
            }

            return false;
        }

        // Use relative tolerance for floating-point comparison
        double tolerance = Math.Max(Math.Abs(min), Math.Abs(max)) * 1e-12;
        if (tolerance < 1e-15)
        {
            tolerance = 1e-15; // minimum absolute tolerance
        }

        bool removedMin = Math.Abs(removed - min) <= tolerance;
        bool removedMax = Math.Abs(removed - max) <= tolerance;

        if (expandMin)
        {
            min = added;
        }

        if (expandMax)
        {
            max = added;
        }

        return (removedMin && !expandMin) || (removedMax && !expandMax);
    }

    /// <summary>
    /// Rescans a span to find new min/max values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RescanMinMax(ReadOnlySpan<double> span, ref double min, ref double max)
    {
        min = double.PositiveInfinity;
        max = double.NegativeInfinity;
        foreach (double v in span)
        {
            if (double.IsNaN(v))
            {
                continue;
            }

            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateMinMax(double added, double removed, bool hasRemoved, RingBuffer buf, ref double min, ref double max)
    {
        if (UpdateMinMaxCore(added, removed, hasRemoved, ref min, ref max))
        {
            var span = buf.IsFull ? buf.InternalBuffer : buf.GetSpan();
            RescanMinMax(span, ref min, ref max);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateMinMax(double added, double removed, bool hasRemoved, ReadOnlySpan<double> buf, ref double min, ref double max)
    {
        if (UpdateMinMaxCore(added, removed, hasRemoved, ref min, ref max))
        {
            RescanMinMax(buf, ref min, ref max);
        }
    }

    // skipcq: CS-R1140 - Cyclomatic complexity justified: STC algorithm requires
    // sequential MACD→Stoch1→Stoch2→Smoothing pipeline with min/max tracking per stage.
    // Splitting would fragment the tightly-coupled state machine and harm readability.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double x = input.Value;

        if (!double.IsFinite(x))
        {
            if (!s.HasFiniteInput)
            {
                Last = new TValue(input.Time, double.NaN);
                PubEvent(Last, isNew);
                return Last;
            }
            x = s.LastFiniteInput;
        }
        else
        {
            s.LastFiniteInput = x;
            s.HasFiniteInput = true;
        }

        // 1) MACD
        s.FastEma = double.IsNaN(s.FastEma) ? x : Math.FusedMultiplyAdd(_fastAlpha, x - s.FastEma, s.FastEma);

        s.SlowEma = double.IsNaN(s.SlowEma) ? x : Math.FusedMultiplyAdd(_slowAlpha, x - s.SlowEma, s.SlowEma);

        double macd = s.FastEma - s.SlowEma;

        double removedMacd = 0;
        bool hasRemovedMacd;
        if (isNew)
        {
            hasRemovedMacd = _macdBuf.IsFull;
            removedMacd = _macdBuf.Add(macd);
        }
        else
        {
            removedMacd = _macdBuf.Newest;
            hasRemovedMacd = _macdBuf.Count > 0;
            _macdBuf.UpdateNewest(macd);
        }
        UpdateMinMax(macd, removedMacd, hasRemovedMacd, _macdBuf, ref s.MacdMin, ref s.MacdMax);

        // 2) Stoch1 of MACD
        double stoch1Raw;
        if (_macdBuf.IsFull)
        {
            double span = s.MacdMax - s.MacdMin;
            if (span > double.Epsilon)
            {
                stoch1Raw = 100.0 * (macd - s.MacdMin) / span;
            }
            else
            {
                stoch1Raw = double.IsNaN(s.Stoch1Ema) ? 50.0 : s.Stoch1Ema;
            }

            stoch1Raw = Clamp100(stoch1Raw);
        }
        else
        {
            stoch1Raw = 50.0;
        }

        // Smooth Stoch1
        if (!double.IsNaN(stoch1Raw))
        {
            s.Stoch1Ema = double.IsNaN(s.Stoch1Ema)
                ? stoch1Raw
                : Math.FusedMultiplyAdd(_dAlpha, stoch1Raw - s.Stoch1Ema, s.Stoch1Ema);
        }

        double stoch1 = double.NaN;
        if (!double.IsNaN(s.Stoch1Ema))
        {
            stoch1 = Clamp100(s.Stoch1Ema);

            double removedStoch1 = 0;
            bool hasRemovedStoch1;
            if (isNew)
            {
                hasRemovedStoch1 = _stoch1Buf.IsFull;
                removedStoch1 = _stoch1Buf.Add(stoch1);
            }
            else
            {
                removedStoch1 = _stoch1Buf.Newest;
                hasRemovedStoch1 = _stoch1Buf.Count > 0;
                _stoch1Buf.UpdateNewest(stoch1);
            }
            UpdateMinMax(stoch1, removedStoch1, hasRemovedStoch1, _stoch1Buf, ref s.Stoch1Min, ref s.Stoch1Max);
        }

        // 3) Stoch2 of Stoch1
        double stoch2Raw;
        if (_stoch1Buf.IsFull)
        {
            double span = s.Stoch1Max - s.Stoch1Min;
            if (span > double.Epsilon)
            {
                stoch2Raw = 100.0 * (stoch1 - s.Stoch1Min) / span;
            }
            else
            {
                stoch2Raw = double.IsNaN(s.Stoch2Ema) ? stoch1 : s.Stoch2Ema;
            }

            stoch2Raw = Clamp100(stoch2Raw);
        }
        else
        {
            stoch2Raw = stoch1;
        }

        // 4) Final Smooth
        double stc = double.NaN;
        if (!double.IsNaN(stoch2Raw))
        {
            stc = ApplySmoothing(stoch2Raw, _smoothing, _dAlpha, ref s.Stoch2Ema, ref s.PrevStc);
        }

        if (isNew)
        {
            _samples++;
        }

        _s = s;
        Last = new TValue(input.Time, stc);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries();
        foreach (var item in source)
        {
            result.Add(Update(item, isNew: true));
        }

        return result;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double v in source)
        {
            Update(new TValue(DateTime.MinValue, v), isNew: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Static convenience method that creates a new Stc instance and processes the entire series.
    /// </summary>
    public static TSeries Batch(TSeries source, int kPeriod = 10, int dPeriod = 3, int fastLength = 23, int slowLength = 50, StcSmoothing smoothing = StcSmoothing.Ema)
    {
        var indicator = new Stc(kPeriod, dPeriod, fastLength, slowLength, smoothing);
        return indicator.Update(source);
    }

    // skipcq: CS-R1140 - Cyclomatic complexity justified: span-based Calculate must
    // replicate the full STC state machine inline for zero-allocation performance.
    // The sequential MACD→Stoch1→Stoch2→Smoothing pipeline cannot be decomposed
    // without introducing heap allocations or sacrificing inlining opportunities.
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int kPeriod = 10, int dPeriod = 3, int fastLength = 23, int slowLength = 50, StcSmoothing smoothing = StcSmoothing.Ema)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }

        double fastAlpha = 2.0 / (fastLength + 1.0);
        double slowAlpha = 2.0 / (slowLength + 1.0);
        double dAlpha = 2.0 / (dPeriod + 1.0);

        double fastEma = double.NaN;
        double slowEma = double.NaN;
        double stoch1Ema = double.NaN;
        double stoch2Ema = double.NaN;
        double prevStc = double.NaN;
        double lastFiniteInput = double.NaN;
        bool hasFiniteInput = false;

        const int StackallocThreshold = 256;
        double[]? rentedMacd = null;
        double[]? rentedStoch1 = null;

        scoped Span<double> macdBuf;
        scoped Span<double> stoch1Buf;

        if (kPeriod <= StackallocThreshold)
        {
            macdBuf = stackalloc double[kPeriod];
            stoch1Buf = stackalloc double[kPeriod];
        }
        else
        {
            rentedMacd = ArrayPool<double>.Shared.Rent(kPeriod);
            macdBuf = rentedMacd.AsSpan(0, kPeriod);
            rentedStoch1 = ArrayPool<double>.Shared.Rent(kPeriod);
            stoch1Buf = rentedStoch1.AsSpan(0, kPeriod);
        }

        try
        {
            int macdIdx = 0;
            int stoch1Idx = 0;
            int macdCount = 0;
            int stoch1Count = 0;

            double macdMin = double.PositiveInfinity;
            double macdMax = double.NegativeInfinity;
            double stoch1Min = double.PositiveInfinity;
            double stoch1Max = double.NegativeInfinity;

            for (int i = 0; i < source.Length; i++)
            {
                double x = source[i];

                if (!double.IsFinite(x))
                {
                    if (!hasFiniteInput)
                    {
                        output[i] = double.NaN;
                        continue;
                    }
                    x = lastFiniteInput;
                }
                else
                {
                    lastFiniteInput = x;
                    hasFiniteInput = true;
                }

                // 1) MACD
                fastEma = double.IsNaN(fastEma) ? x : Math.FusedMultiplyAdd(fastAlpha, x - fastEma, fastEma);
                slowEma = double.IsNaN(slowEma) ? x : Math.FusedMultiplyAdd(slowAlpha, x - slowEma, slowEma);

                double macd = fastEma - slowEma;

                // Buffer MACD
                bool macdHasRemoved = macdCount == kPeriod;
                double macdRemoved = macdBuf[macdIdx];
                macdBuf[macdIdx] = macd;
                macdIdx = (macdIdx + 1) % kPeriod;
                if (!macdHasRemoved)
                {
                    macdCount++;
                }

                ReadOnlySpan<double> macdValidSpan = macdBuf.Slice(0, macdCount);
                UpdateMinMax(macd, macdRemoved, macdHasRemoved, macdValidSpan, ref macdMin, ref macdMax);

                // 2) Stoch1
                double stoch1Raw;
                if (macdCount == kPeriod)
                {
                    double span = macdMax - macdMin;
                    if (span > double.Epsilon)
                    {
                        stoch1Raw = 100.0 * (macd - macdMin) / span;
                    }
                    else
                    {
                        stoch1Raw = double.IsNaN(stoch1Ema) ? 50.0 : stoch1Ema;
                    }

                    stoch1Raw = Clamp100(stoch1Raw);
                }
                else
                {
                    stoch1Raw = 50.0;
                }

                // Smooth Stoch1
                if (!double.IsNaN(stoch1Raw))
                {
                    stoch1Ema = double.IsNaN(stoch1Ema)
                        ? stoch1Raw
                        : Math.FusedMultiplyAdd(dAlpha, stoch1Raw - stoch1Ema, stoch1Ema);
                }

                double stoch1 = double.NaN;
                if (!double.IsNaN(stoch1Ema))
                {
                    stoch1 = Clamp100(stoch1Ema);

                    // Buffer Stoch1
                    bool stochHasRemoved = stoch1Count == kPeriod;
                    double stochRemoved = stoch1Buf[stoch1Idx];
                    stoch1Buf[stoch1Idx] = stoch1;
                    stoch1Idx = (stoch1Idx + 1) % kPeriod;
                    if (!stochHasRemoved)
                    {
                        stoch1Count++;
                    }

                    ReadOnlySpan<double> stochValidSpan = stoch1Buf.Slice(0, stoch1Count);
                    UpdateMinMax(stoch1, stochRemoved, stochHasRemoved, stochValidSpan, ref stoch1Min, ref stoch1Max);
                }

                // 3) Stoch2
                double stoch2Raw;
                if (stoch1Count == kPeriod)
                {
                    double span = stoch1Max - stoch1Min;
                    if (span > double.Epsilon)
                    {
                        stoch2Raw = 100.0 * (stoch1 - stoch1Min) / span;
                    }
                    else
                    {
                        stoch2Raw = double.IsNaN(stoch2Ema) ? stoch1 : stoch2Ema;
                    }

                    stoch2Raw = Clamp100(stoch2Raw);
                }
                else
                {
                    stoch2Raw = stoch1;
                }

                // 4) Final Smooth
                double stc = double.NaN;
                if (!double.IsNaN(stoch2Raw))
                {
                    stc = ApplySmoothing(stoch2Raw, smoothing, dAlpha, ref stoch2Ema, ref prevStc);
                }

                output[i] = stc;
            }
        }
        finally
        {
            if (rentedMacd != null)
            {
                ArrayPool<double>.Shared.Return(rentedMacd);
            }

            if (rentedStoch1 != null)
            {
                ArrayPool<double>.Shared.Return(rentedStoch1);
            }
        }
    }

    public static (TSeries Results, Stc Indicator) Calculate(TSeries source, int kPeriod = 10, int dPeriod = 3, int fastLength = 23, int slowLength = 50, StcSmoothing smoothing = StcSmoothing.Ema)
    {
        var indicator = new Stc(kPeriod, dPeriod, fastLength, slowLength, smoothing);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}