using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// APO: Absolute Price Oscillator
/// A momentum indicator that measures the difference between two moving averages
/// of different periods. Similar to PPO but shows absolute difference instead of percentage.
/// </summary>
public sealed class Apo : AbstractBase
{
    private readonly AbstractBase _fastMa, _slowMa;

    /// <param name="fastPeriod">The period for the faster moving average.</param>
    /// <param name="slowPeriod">The period for the slower moving average.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when fastPeriod or slowPeriod is less than 1, or when fastPeriod is greater than or equal to slowPeriod.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Apo(int fastPeriod = 12, int slowPeriod = 26)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fastPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(slowPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fastPeriod, slowPeriod);

        _fastMa = new Ema(fastPeriod);
        _slowMa = new Ema(slowPeriod);
        WarmupPeriod = slowPeriod;
        Name = $"APO({fastPeriod},{slowPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="fastPeriod">The period for the faster moving average.</param>
    /// <param name="slowPeriod">The period for the slower moving average.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Apo(object source, int fastPeriod, int slowPeriod) : this(fastPeriod, slowPeriod)
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
            _lastValidValue = Input.Value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _fastMa.Calc(Input);
        _slowMa.Calc(Input);
        return _fastMa.Value - _slowMa.Value;
    }
}
