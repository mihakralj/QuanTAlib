using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// OBV: On-Balance Volume
/// A momentum indicator that uses volume flow to predict changes in stock price.
/// It accumulates volume on up days and subtracts volume on down days.
/// </summary>
/// <remarks>
/// The OBV calculation process:
/// 1. Compare current close with previous close
/// 2. If current close is higher:
///    OBV = Previous OBV + Current Volume
/// 3. If current close is lower:
///    OBV = Previous OBV - Current Volume
/// 4. If current close equals previous close:
///    OBV = Previous OBV
///
/// Key characteristics:
/// - Cumulative indicator
/// - Volume-based momentum measure
/// - Leading indicator
/// - No upper or lower bounds
/// - Focuses on volume flow
///
/// Formula:
/// If Close > Previous Close:
///    OBV = Previous OBV + Volume
/// If Close < Previous Close:
///    OBV = Previous OBV - Volume
/// If Close = Previous Close:
///    OBV = Previous OBV
///
/// Market Applications:
/// - Trend confirmation
/// - Potential breakouts
/// - Divergence analysis
/// - Volume flow analysis
/// - Price movement prediction
///
/// Sources:
///     Joe Granville - Original development (1963)
///     https://www.investopedia.com/terms/o/onbalancevolume.asp
///
/// Note: Rising OBV suggests buying pressure, while falling OBV suggests selling pressure
/// </remarks>

[SkipLocalsInit]
public sealed class Obv : AbstractBase
{
    private double _prevClose;
    private double _prevObv;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Obv()
    {
        WarmupPeriod = 2;  // Need previous close
        Name = "OBV";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Obv(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _prevObv = 0;
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

        // Calculate OBV
        if (BarInput.Close > _prevClose)
        {
            _prevObv += BarInput.Volume;
        }
        else if (BarInput.Close < _prevClose)
        {
            _prevObv -= BarInput.Volume;
        }
        // If prices equal, OBV remains the same

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        IsHot = _index >= WarmupPeriod;
        return _prevObv;
    }
}
