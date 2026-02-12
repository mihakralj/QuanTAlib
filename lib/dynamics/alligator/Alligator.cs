using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ALLIGATOR: Williams Alligator Indicator
/// </summary>
/// <remarks>
/// Bill Williams' trend-following indicator using three SMMA lines with different periods and offsets.
/// The lines represent the Jaw (blue), Teeth (red), and Lips (green) of an alligator.
/// When lines are intertwined, the alligator is "sleeping" (no trend). When separated, it's "eating" (trending).
///
/// Default parameters:
/// - Jaw: SMMA(13), offset 8 bars forward (blue)
/// - Teeth: SMMA(8), offset 5 bars forward (red)
/// - Lips: SMMA(5), offset 3 bars forward (green)
///
/// Uses Wilder's smoothing (RMA/SMMA) with α = 1/period.
/// </remarks>
/// <seealso href="Alligator.md">Detailed documentation</seealso>
/// <seealso href="alligator.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Alligator : ITValuePublisher
{
    // SMMA state for each line (Wilder's smoothing with bias compensation)
    [StructLayout(LayoutKind.Auto)]
    private record struct SmmaState
    {
        public double Ema;      // Running SMMA value
        public double E;        // Warmup compensator (starts at 1.0, decays)
        public bool IsHot;      // True when warmed up

        public static SmmaState New() => new() { Ema = 0.0, E = 1.0, IsHot = false };
    }

    private readonly int _jawPeriod;
    private readonly int _teethPeriod;
    private readonly int _lipsPeriod;
    private readonly int _jawOffset;
    private readonly int _teethOffset;
    private readonly int _lipsOffset;
    private readonly double _alphaJaw;
    private readonly double _alphaTeeth;
    private readonly double _alphaLips;

    private SmmaState _jawState;
    private SmmaState _teethState;
    private SmmaState _lipsState;

    // Previous states for bar correction
    private SmmaState _p_jawState;
    private SmmaState _p_teethState;
    private SmmaState _p_lipsState;

    private double _lastValidValue;
    private double _p_lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current Jaw value (SMMA of longest period, slowest line).
    /// Note: This is the current SMMA value; offset is applied in plotting.
    /// </summary>
    public TValue Jaw { get; private set; }

    /// <summary>
    /// Current Teeth value (SMMA of medium period, middle line).
    /// Note: This is the current SMMA value; offset is applied in plotting.
    /// </summary>
    public TValue Teeth { get; private set; }

    /// <summary>
    /// Current Lips value (SMMA of shortest period, fastest line).
    /// Note: This is the current SMMA value; offset is applied in plotting.
    /// </summary>
    public TValue Lips { get; private set; }

    /// <summary>
    /// The last computed value (defaults to Lips, the fastest line).
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if all three SMMA lines have warmed up.
    /// </summary>
    public bool IsHot => _jawState.IsHot && _teethState.IsHot && _lipsState.IsHot;

    /// <summary>
    /// The number of bars required for full warmup (based on longest period).
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates Williams Alligator with default parameters.
    /// Jaw: period=13, offset=8; Teeth: period=8, offset=5; Lips: period=5, offset=3.
    /// </summary>
    public Alligator() : this(13, 8, 8, 5, 5, 3)
    {
    }

    /// <summary>
    /// Creates Williams Alligator with specified parameters.
    /// </summary>
    /// <param name="jawPeriod">Period for Jaw SMMA (typically 13)</param>
    /// <param name="jawOffset">Forward offset for Jaw (typically 8)</param>
    /// <param name="teethPeriod">Period for Teeth SMMA (typically 8)</param>
    /// <param name="teethOffset">Forward offset for Teeth (typically 5)</param>
    /// <param name="lipsPeriod">Period for Lips SMMA (typically 5)</param>
    /// <param name="lipsOffset">Forward offset for Lips (typically 3)</param>
    public Alligator(int jawPeriod, int jawOffset, int teethPeriod, int teethOffset, int lipsPeriod, int lipsOffset)
    {
        if (jawPeriod <= 0)
        {
            throw new ArgumentException("Jaw period must be greater than 0", nameof(jawPeriod));
        }
        if (teethPeriod <= 0)
        {
            throw new ArgumentException("Teeth period must be greater than 0", nameof(teethPeriod));
        }
        if (lipsPeriod <= 0)
        {
            throw new ArgumentException("Lips period must be greater than 0", nameof(lipsPeriod));
        }
        if (jawOffset < 0)
        {
            throw new ArgumentException("Jaw offset must be non-negative", nameof(jawOffset));
        }
        if (teethOffset < 0)
        {
            throw new ArgumentException("Teeth offset must be non-negative", nameof(teethOffset));
        }
        if (lipsOffset < 0)
        {
            throw new ArgumentException("Lips offset must be non-negative", nameof(lipsOffset));
        }

        _jawPeriod = jawPeriod;
        _teethPeriod = teethPeriod;
        _lipsPeriod = lipsPeriod;
        _jawOffset = jawOffset;
        _teethOffset = teethOffset;
        _lipsOffset = lipsOffset;

        // Wilder's smoothing: alpha = 1 / period
        _alphaJaw = 1.0 / jawPeriod;
        _alphaTeeth = 1.0 / teethPeriod;
        _alphaLips = 1.0 / lipsPeriod;

        _jawState = SmmaState.New();
        _teethState = SmmaState.New();
        _lipsState = SmmaState.New();
        _p_jawState = _jawState;
        _p_teethState = _teethState;
        _p_lipsState = _lipsState;

        Name = $"Alligator({jawPeriod},{jawOffset},{teethPeriod},{teethOffset},{lipsPeriod},{lipsOffset})";
        WarmupPeriod = Math.Max(Math.Max(jawPeriod, teethPeriod), lipsPeriod);
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _jawState = SmmaState.New();
        _teethState = SmmaState.New();
        _lipsState = SmmaState.New();
        _p_jawState = _jawState;
        _p_teethState = _teethState;
        _p_lipsState = _lipsState;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Jaw = default;
        Teeth = default;
        Lips = default;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeSmma(double input, double alpha, ref SmmaState state)
    {
        // SMMA/RMA formula: ema = alpha * (input - ema) + ema
        state.Ema = Math.FusedMultiplyAdd(alpha, input - state.Ema, state.Ema);

        double result;
        if (!state.IsHot)
        {
            // Bias compensation during warmup
            state.E *= (1.0 - alpha);
            double compensator = 1.0 / (1.0 - state.E);
            result = compensator * state.Ema;
            // Standard warmup threshold (matches EMA/RMA pattern)
            state.IsHot = state.E <= 0.05;
        }
        else
        {
            result = state.Ema;
        }

        return result;
    }

    /// <summary>
    /// Updates the indicator with a new price bar.
    /// </summary>
    /// <param name="input">Price bar (uses HLC3 - typical price)</param>
    /// <param name="isNew">True for new bar, false for bar update/correction</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double hlc3 = (input.High + input.Low + input.Close) / 3.0;
        return Update(new TValue(input.Time, hlc3), isNew);
    }

    /// <summary>
    /// Updates the indicator with a new value.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for bar update/correction</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_jawState = _jawState;
            _p_teethState = _teethState;
            _p_lipsState = _lipsState;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _jawState = _p_jawState;
            _teethState = _p_teethState;
            _lipsState = _p_lipsState;
            _lastValidValue = _p_lastValidValue;
        }

        double val = GetValidValue(input.Value);

        double jawVal = ComputeSmma(val, _alphaJaw, ref _jawState);
        double teethVal = ComputeSmma(val, _alphaTeeth, ref _teethState);
        double lipsVal = ComputeSmma(val, _alphaLips, ref _lipsState);

        Jaw = new TValue(input.Time, jawVal);
        Teeth = new TValue(input.Time, teethVal);
        Lips = new TValue(input.Time, lipsVal);
        Last = Lips; // Primary output is the fastest line

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Processes a TBarSeries and returns TSeries of Lips values.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var tList = new List<long>(len);
        var vList = new List<double>(len);

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            Update(bar, isNew: true);
            tList.Add(bar.Time);
            vList.Add(Lips.Value);
        }

        return new TSeries(tList, vList);
    }


    /// <summary>
    /// Initializes the indicator state using the provided bar series history.
    /// </summary>
    /// <param name="source">Historical bar data.</param>
    public void Prime(TBarSeries source)
    {
        Reset();
        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Calculates Alligator for the entire series using default parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source)
    {
        var alligator = new Alligator();
        return alligator.Update(source);
    }

    /// <summary>
    /// Calculates Alligator for the entire series using custom parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int jawPeriod, int jawOffset, int teethPeriod, int teethOffset, int lipsPeriod, int lipsOffset)
    {
        var alligator = new Alligator(jawPeriod, jawOffset, teethPeriod, teethOffset, lipsPeriod, lipsOffset);
        return alligator.Update(source);
    }

    public static (TSeries Results, Alligator Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Alligator();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }


    /// <summary>
    /// Gets the Jaw period value.
    /// </summary>
    public int JawPeriod => _jawPeriod;

    /// <summary>
    /// Gets the Teeth period value.
    /// </summary>
    public int TeethPeriod => _teethPeriod;

    /// <summary>
    /// Gets the Lips period value.
    /// </summary>
    public int LipsPeriod => _lipsPeriod;

    /// <summary>
    /// Gets the Jaw offset value (bars forward).
    /// </summary>
    public int JawOffset => _jawOffset;

    /// <summary>
    /// Gets the Teeth offset value (bars forward).
    /// </summary>
    public int TeethOffset => _teethOffset;

    /// <summary>
    /// Gets the Lips offset value (bars forward).
    /// </summary>
    public int LipsOffset => _lipsOffset;
}