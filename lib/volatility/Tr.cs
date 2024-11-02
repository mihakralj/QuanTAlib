using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// TR: True Range
/// A basic volatility measure that represents the greatest of three price ranges:
/// current high-low, current high-previous close, or current low-previous close.
/// </summary>
/// <remarks>
/// The TR calculation process:
/// 1. Calculate three differences:
///    - Current High minus Current Low
///    - |Current High minus Previous Close|
///    - |Current Low minus Previous Close|
/// 2. TR is the maximum of these three values
///
/// Key characteristics:
/// - Basic volatility measure
/// - Accounts for gaps between trading periods
/// - Foundation for other indicators (ATR, etc.)
/// - No upper bound
/// - Always positive
///
/// Formula:
/// TR = max(High - Low, |High - Previous Close|, |Low - Previous Close|)
///
/// Market Applications:
/// - Volatility measurement
/// - Stop loss placement
/// - Position sizing
/// - Market analysis
/// - Risk assessment
///
/// Sources:
///     J. Welles Wilder Jr. - Original development
///     https://www.investopedia.com/terms/t/truerange.asp
///
/// Note: True Range accounts for gaps between periods, making it more accurate than simple high-low range
/// </remarks>

[SkipLocalsInit]
public sealed class Tr : AbstractBase
{
    private double _prevClose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tr()
    {
        WarmupPeriod = 2;  // Need previous close
        Name = "TR";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tr(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
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
            return BarInput.High - BarInput.Low;
        }

        // Calculate True Range
        double tr = Math.Max(BarInput.High - BarInput.Low,
                   Math.Max(Math.Abs(BarInput.High - _prevClose),
                          Math.Abs(BarInput.Low - _prevClose)));

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        IsHot = _index >= WarmupPeriod;
        return tr;
    }
}
