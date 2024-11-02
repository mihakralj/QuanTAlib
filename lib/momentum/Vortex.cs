using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VORTEX: Vortex Indicator
/// A technical indicator consisting of two oscillating lines that identify trend reversals
/// and confirm current trends based on the highs and lows of the previous period.
/// </summary>
/// <remarks>
/// The Vortex calculation process:
/// 1. Calculate True Range (TR):
///    TR = max(High - Low, |High - Previous Close|, |Low - Previous Close|)
/// 2. Calculate +VM (Positive Movement):
///    +VM = |Current High - Previous Low|
/// 3. Calculate -VM (Negative Movement):
///    -VM = |Current Low - Previous High|
/// 4. Calculate period sums:
///    TR Period Sum = Sum(TR, period)
///    +VM Period Sum = Sum(+VM, period)
///    -VM Period Sum = Sum(-VM, period)
/// 5. Calculate +VI and -VI:
///    +VI = +VM Period Sum / TR Period Sum
///    -VI = -VM Period Sum / TR Period Sum
///
/// Key characteristics:
/// - Two oscillating lines (+VI and -VI)
/// - No upper or lower bounds
/// - Default period is 14 days
/// - Crossovers signal trend changes
/// - Uses true range normalization
///
/// Formula:
/// +VI = Sum(+VM, period) / Sum(TR, period)
/// -VI = Sum(-VM, period) / Sum(TR, period)
///
/// Market Applications:
/// - Trend identification
/// - Trend reversals
/// - Trend confirmation
/// - Trading signals
/// - Market momentum
///
/// Sources:
///     Etienne Botes and Douglas Siepman - Original development (2010)
///     https://www.investopedia.com/terms/v/vortex-indicator-vi.asp
///
/// Note: When +VI crosses above -VI, it signals a potential uptrend, and vice versa
/// </remarks>

[SkipLocalsInit]
public sealed class Vortex : AbstractBase
{
    private readonly CircularBuffer _tr;
    private readonly CircularBuffer _vmPlus;
    private readonly CircularBuffer _vmMinus;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    public double _viPlus { get; set; }
    public double _viMinus { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vortex(int period = 14)
    {
        WarmupPeriod = period + 1;  // Need one extra period for previous values
        Name = $"VORTEX({period})";
        _tr = new CircularBuffer(period);
        _vmPlus = new CircularBuffer(period);
        _vmMinus = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vortex(object source, int period = 14) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _viPlus = 0;
        _viMinus = 0;
        _tr.Clear();
        _vmPlus.Clear();
        _vmMinus.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Skip first period to establish previous values
        if (_index == 1)
        {
            _prevHigh = BarInput.High;
            _prevLow = BarInput.Low;
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate True Range
        double tr = Math.Max(BarInput.High - BarInput.Low,
                   Math.Max(Math.Abs(BarInput.High - _prevClose),
                          Math.Abs(BarInput.Low - _prevClose)));

        // Calculate VM+ and VM-
        double vmPlus = Math.Abs(BarInput.High - _prevLow);
        double vmMinus = Math.Abs(BarInput.Low - _prevHigh);

        // Add values to buffers
        _tr.Add(tr);
        _vmPlus.Add(vmPlus);
        _vmMinus.Add(vmMinus);

        // Calculate VI+ and VI-
        double trSum = _tr.Sum();
        if (Math.Abs(trSum) > double.Epsilon)
        {
            _viPlus = _vmPlus.Sum() / trSum;
            _viMinus = _vmMinus.Sum() / trSum;
        }

        // Store current values for next calculation
        _prevHigh = BarInput.High;
        _prevLow = BarInput.Low;
        _prevClose = BarInput.Close;

        // Return the difference between VI+ and VI-
        double vortex = _viPlus - _viMinus;

        IsHot = _index >= WarmupPeriod;
        return vortex;
    }

    /// <summary>
    /// Gets the positive Vortex line (VI+)
    /// </summary>
    public double ViPlus => _viPlus;

    /// <summary>
    /// Gets the negative Vortex line (VI-)
    /// </summary>
    public double ViMinus => _viMinus;
}
