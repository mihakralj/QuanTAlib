using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CMO: Chande Momentum Oscillator
/// A technical momentum indicator that measures the difference between upward and
/// downward momentum. CMO helps identify overbought and oversold conditions, as
/// well as trend strength and potential reversals.
/// </summary>
/// <remarks>
/// The CMO calculation process:
/// 1. Calculates price differences from previous period
/// 2. Separates positive (upward) and negative (downward) movements
/// 3. Sums upward and downward movements over period
/// 4. Calculates: 100 * ((sumUp - sumDown) / (sumUp + sumDown))
///
/// Key characteristics:
/// - Oscillates between -100 and +100
/// - Values above +50 indicate overbought
/// - Values below -50 indicate oversold
/// - Zero line crossovers signal trend changes
/// - High absolute values suggest strong trends
///
/// Formula:
/// CMO = 100 * ((ΣUp - ΣDown) / (ΣUp + ΣDown))
/// where:
/// Up = positive price changes
/// Down = absolute negative price changes
///
/// Sources:
///     Tushar Chande - "The New Technical Trader" (1994)
///     https://www.investopedia.com/terms/c/chandemomentumoscillator.asp
///
/// Note: Similar to RSI but with different scaling and calculation method
/// </remarks>

[SkipLocalsInit]
public sealed class Cmo : AbstractBase
{
    private readonly CircularBuffer _sumH;
    private readonly CircularBuffer _sumL;
    private double _prevValue, _p_prevValue;
    private const double Epsilon = 1e-10;
    private const double ScalingFactor = 100.0;

    /// <param name="period">The number of periods used in the CMO calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmo(int period)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _sumH = new(period);
        _sumL = new(period);

        WarmupPeriod = period + 1;
        Name = $"CMO({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the CMO calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmo(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevValue = _prevValue;
        }
        else
        {
            _prevValue = _p_prevValue;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double up, double down) CalculateMovements(double diff)
    {
        return diff > 0 ? (diff, 0) : (0, -diff);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateCmo(double sumH, double sumL)
    {
        double divisor = sumH + sumL;
        return (Math.Abs(divisor) > Epsilon) ? ScalingFactor * ((sumH - sumL) / divisor) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 0)
        {
            _prevValue = Input.Value;
        }

        // Calculate price difference
        double diff = Input.Value - _prevValue;
        _prevValue = Input.Value;

        // Separate upward and downward movements
        var (up, down) = CalculateMovements(diff);
        _sumH.Add(up, Input.IsNew);
        _sumL.Add(down, Input.IsNew);

        // Calculate sums and CMO value
        return CalculateCmo(_sumH.Sum(), _sumL.Sum());
    }
}
