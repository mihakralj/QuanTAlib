using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PVO: Percentage Volume Oscillator
/// A momentum indicator for volume that shows the relationship between two volume moving averages
/// as a percentage. Similar to the Price Oscillator but uses volume instead of price.
/// </summary>
/// <remarks>
/// The PVO calculation process:
/// 1. Calculate short-term EMA of volume
/// 2. Calculate long-term EMA of volume
/// 3. Calculate PVO:
///    PVO = ((Short EMA - Long EMA) / Long EMA) * 100
///
/// Key characteristics:
/// - Volume-based momentum indicator
/// - Oscillates around zero
/// - Shows volume trends
/// - Default periods are 12 and 26 days
/// - Percentage-based measure
///
/// Formula:
/// Short EMA = EMA(Volume, shortPeriod)
/// Long EMA = EMA(Volume, longPeriod)
/// PVO = ((Short EMA - Long EMA) / Long EMA) * 100
///
/// Market Applications:
/// - Volume trend analysis
/// - Divergence identification
/// - Volume momentum measurement
/// - Market tops and bottoms
/// - Trading volume patterns
///
/// Sources:
///     https://www.investopedia.com/terms/p/pvo.asp
///
/// Note: Positive values indicate higher short-term volume, while negative values indicate higher long-term volume
/// </remarks>
[SkipLocalsInit]
public sealed class Pvo : AbstractBase
{
    private readonly int _longPeriod;
    private double _shortEma;
    private double _longEma;
    private readonly double _shortAlpha;
    private readonly double _longAlpha;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvo(int shortPeriod = 12, int longPeriod = 26)
    {
        _longPeriod = longPeriod;
        WarmupPeriod = longPeriod;
        Name = $"PVO({shortPeriod},{_longPeriod})";
        _shortAlpha = 2.0 / (shortPeriod + 1);
        _longAlpha = 2.0 / (longPeriod + 1);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvo(object source, int shortPeriod = 12, int longPeriod = 26) : this(shortPeriod, longPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _shortEma = 0;
        _longEma = 0;
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

        // Initialize or update EMAs
        if (_index <= _longPeriod)
        {
            _shortEma = BarInput.Volume;
            _longEma = BarInput.Volume;
            return 0;
        }

        // Update EMAs
        _shortEma = (_shortAlpha * BarInput.Volume) + ((1 - _shortAlpha) * _shortEma);
        _longEma = (_longAlpha * BarInput.Volume) + ((1 - _longAlpha) * _longEma);

        // Calculate PVO

        double pvo = Math.Abs(_longEma) >= double.Epsilon ? ((_shortEma - _longEma) / _longEma) * 100 : 0;

        IsHot = _index >= WarmupPeriod;
        return pvo;
    }
}
