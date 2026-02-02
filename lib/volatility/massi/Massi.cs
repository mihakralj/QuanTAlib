using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MASSI: Mass Index
/// </summary>
/// <remarks>
/// Developed by Donald Dorsey to identify trend reversals by measuring the narrowing
/// and widening of the range between high and low prices. A "reversal bulge" occurs
/// when Mass Index rises above 27 and then drops below 26.5.
///
/// Calculation:
/// 1. EMA1 = EMA(High - Low, emaLength)
/// 2. EMA2 = EMA(EMA1, emaLength) (double-smoothed)
/// 3. Ratio = EMA1 / EMA2
/// 4. MASSI = Sum(Ratio, sumLength)
///
/// Default: emaLength=9, sumLength=25 → typical MASSI(9,25).
/// </remarks>
/// <seealso href="Massi.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Massi : AbstractBase
{
    private const double COMPENSATOR_THRESHOLD = 1e-10;

    private readonly double _alpha;
    private readonly double _decay;
    private readonly RingBuffer _sumBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;

    private State _s;
    private State _ps;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        // EMA states (raw, uncompensated accumulators)
        public double Ema1Raw;
        public double Ema2Raw;
        public double E;            // compensation factor (decays to 0)
        public bool IsCompensated;  // true when E <= threshold

        // Last valid price range (for NaN handling)
        public double LastRange;

        // Bar counter
        public int Bars;
    }

    /// <summary>
    /// Gets the current EMA1 value (smoothed range).
    /// </summary>
    public double Ema1 { get; private set; }

    /// <summary>
    /// Gets the current EMA2 value (double-smoothed range).
    /// </summary>
    public double Ema2 { get; private set; }

    /// <summary>
    /// Gets the current ratio (EMA1/EMA2).
    /// </summary>
    public double Ratio { get; private set; }

    public override bool IsHot => _s.Bars >= WarmupPeriod;

    /// <summary>
    /// Creates MASSI with specified parameters.
    /// </summary>
    /// <param name="emaLength">Period for EMA smoothing of High-Low range (default: 9)</param>
    /// <param name="sumLength">Period for summing the EMA ratio (default: 25)</param>
    public Massi(int emaLength = 9, int sumLength = 25)
    {
        if (emaLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(emaLength), "EMA length must be >= 1.");
        }
        if (sumLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sumLength), "Sum length must be >= 1.");
        }

        _alpha = 2.0 / (emaLength + 1);
        _decay = 1.0 - _alpha;

        _sumBuffer = new RingBuffer(sumLength);
        _handler = Handle;

        // Warmup: need enough bars to fill the sum buffer + EMA stabilization
        WarmupPeriod = emaLength + sumLength;
        Name = $"Massi({emaLength},{sumLength})";

        Reset();
    }

    /// <summary>
    /// Creates MASSI with specified source and parameters.
    /// </summary>
    public Massi(ITValuePublisher source, int emaLength = 9, int sumLength = 25)
        : this(emaLength, sumLength)
    {
        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _s = new State { E = 1.0 };
        _ps = _s;
        _sumBuffer.Clear();
        Ema1 = 0;
        Ema2 = 0;
        Ratio = 0;
        Last = default;
    }

    /// <summary>
    /// Updates MASSI with a TBar (OHLCV) input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double range = input.High - input.Low;
        return UpdateCore(input.Time, range, isNew);
    }

    /// <summary>
    /// Updates MASSI with a TValue input (treats value as pre-calculated range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, isNew);
    }

    /// <summary>
    /// Updates MASSI with a TBarSeries.
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

        Reset();
        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            double range = bar.High - bar.Low;
            tSpan[i] = bar.Time;
            vSpan[i] = CalculateMassiStep(range);
        }

        // Sync state
        _ps = _s;
        _sumBuffer.Snapshot();

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates MASSI with a TSeries (assumes values are pre-calculated ranges).
    /// </summary>
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

        source.Times.CopyTo(tSpan);

        Reset();
        for (int i = 0; i < len; i++)
        {
            vSpan[i] = CalculateMassiStep(source.Values[i]);
        }

        _ps = _s;
        _sumBuffer.Snapshot();

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long time, double range, bool isNew)
    {
        HandleStateSnapshot(isNew);

        // Handle non-finite values
        if (!double.IsFinite(range) || range < 0)
        {
            if (_s.Bars == 0)
            {
                Last = new TValue(time, double.NaN);  // time is already long (ticks)
                PubEvent(Last, isNew);
                return Last;
            }
            range = _s.LastRange;
        }
        else
        {
            _s.LastRange = range;
        }

        _s.Bars++;

        double massi = CalculateMassi(range);
        Last = new TValue(time, massi);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleStateSnapshot(bool isNew)
    {
        if (isNew)
        {
            _ps = _s;
            _sumBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _sumBuffer.Restore();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateMassi(double range)
    {
        // Update EMA1 (smoothed range)
        _s.Ema1Raw = Math.FusedMultiplyAdd(_s.Ema1Raw, _decay, _alpha * range);

        // Update EMA2 (double-smoothed, uses EMA1 raw for continuity)
        _s.Ema2Raw = Math.FusedMultiplyAdd(_s.Ema2Raw, _decay, _alpha * _s.Ema1Raw);

        // Compensation handling
        double ema1, ema2;
        if (!_s.IsCompensated)
        {
            _s.E *= _decay;
            if (_s.E <= COMPENSATOR_THRESHOLD)
            {
                _s.IsCompensated = true;
                ema1 = _s.Ema1Raw;
                ema2 = _s.Ema2Raw;
            }
            else
            {
                double c = 1.0 / (1.0 - _s.E);
                ema1 = _s.Ema1Raw * c;
                ema2 = _s.Ema2Raw * c;
            }
        }
        else
        {
            ema1 = _s.Ema1Raw;
            ema2 = _s.Ema2Raw;
        }

        // Store for property access
        Ema1 = ema1;
        Ema2 = ema2;

        // Calculate ratio (avoid division by zero)
        double ratio = ema2 > 1e-10 ? ema1 / ema2 : 0.0;
        Ratio = ratio;

        // Add to rolling sum buffer
        _sumBuffer.Add(ratio);

        // Return sum of ratios
        return _sumBuffer.Sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateMassiStep(double range)
    {
        // Handle non-finite values
        if (!double.IsFinite(range) || range < 0)
        {
            if (_s.Bars == 0)
            {
                return double.NaN;
            }
            range = _s.LastRange;
        }
        else
        {
            _s.LastRange = range;
        }

        _s.Bars++;
        return CalculateMassi(range);
    }

    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    protected override void Dispose(bool disposing)
    {
        if (disposing && _source != null)
        {
            _source.Pub -= _handler;
        }
        base.Dispose(disposing);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();
        foreach (var value in source)
        {
            _ = CalculateMassiStep(value);
        }
        _ps = _s;
        _sumBuffer.Snapshot();
    }

    /// <summary>
    /// Calculates MASSI for the entire TBarSeries using a new instance.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int emaLength = 9, int sumLength = 25)
    {
        var massi = new Massi(emaLength, sumLength);
        return massi.Update(source);
    }

    /// <summary>
    /// Calculates MASSI for the entire TSeries (ranges) using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int emaLength = 9, int sumLength = 25)
    {
        var massi = new Massi(emaLength, sumLength);
        return massi.Update(source);
    }

    /// <summary>
    /// Static helper for span-based calculation (assumes input is H-L range).
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output,
                                 int emaLength = 9, int sumLength = 25)
    {
        if (output.Length != source.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        var massi = new Massi(emaLength, sumLength);
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = massi.CalculateMassiStep(source[i]);
        }
    }
}