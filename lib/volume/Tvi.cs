using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// TVI: Trade Volume Index
/// A technical indicator that determines whether a security is being accumulated or distributed
/// based on price changes relative to a minimum tick value.
/// </summary>
/// <remarks>
/// The TVI calculation process:
/// 1. Calculate price change:
///    Price Change = Close - Previous Close
/// 2. Compare price change to minimum tick value:
///    If |Price Change| >= Minimum Tick:
///       Add/Subtract volume based on price direction
///
/// Key characteristics:
/// - Volume-based trend indicator
/// - Uses minimum tick value
/// - Cumulative measure
/// - No upper or lower bounds
/// - Focuses on significant moves
///
/// Formula:
/// If |Close - Previous Close| >= Minimum Tick:
///    If Close > Previous Close:
///       TVI = Previous TVI + Volume
///    If Close < Previous Close:
///       TVI = Previous TVI - Volume
/// Else:
///    TVI = Previous TVI
///
/// Market Applications:
/// - Trend identification
/// - Volume analysis
/// - Accumulation/distribution
/// - Price movement significance
/// - Trading signal generation
///
/// Note: Rising TVI suggests accumulation, while falling TVI suggests distribution
/// </remarks>
[SkipLocalsInit]
public sealed class Tvi : AbstractBase
{
    private readonly double _minTick;
    private double _prevClose;
    private double _prevTvi;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tvi(double minTick = 0.5)
    {
        _minTick = minTick;
        WarmupPeriod = 2;  // Need previous close
        Name = $"TVI({_minTick})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tvi(object source, double minTick = 0.5) : this(minTick)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _prevTvi = 0;
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

        // Skip first period to establish previous close
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate price change
        double priceChange = BarInput.Close - _prevClose;

        // Update TVI if price change exceeds minimum tick
        if (Math.Abs(priceChange) >= _minTick)
        {
            _prevTvi += priceChange > 0 ? BarInput.Volume : -BarInput.Volume;
        }

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        IsHot = _index >= WarmupPeriod;
        return _prevTvi;
    }
}
