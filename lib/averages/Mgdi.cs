using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MGDI: Modified Geometric Decay Index
/// A moving average that uses geometric decay with a ratio-based adjustment factor.
/// The decay rate is modified based on the ratio between current and previous values,
/// allowing for adaptive smoothing based on price movement magnitude.
/// </summary>
/// <remarks>
/// The MGDI calculation process:
/// 1. Calculates ratio between current and previous values
/// 2. Uses ratio to modify the geometric decay rate
/// 3. Applies modified decay to smooth the data
/// 4. Adjusts smoothing based on K-factor parameter
///
/// Key characteristics:
/// - Geometric decay-based smoothing
/// - Adaptive to price movement magnitude
/// - Adjustable smoothing via K-factor
/// - More responsive to large price changes
/// - Maintains smoothness during small fluctuations
///
/// Implementation:
///     Based on geometric decay principles with ratio-based modification
/// </remarks>

public class Mgdi : AbstractBase
{
    private readonly int _period;
    private readonly double _kFactor;
    private readonly double _kFactorPeriod;  // Precalculated k * period
    private double _prevMd, _p_prevMd;

    /// <param name="period">The number of periods used in the MGDI calculation.</param>
    /// <param name="kFactor">The K-factor controlling the decay rate adjustment (default 0.6).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period or kFactor is less than or equal to 0.</exception>
    public Mgdi(int period, double kFactor = 0.6)
    {
        if (period <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0.");
        }
        if (kFactor <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(kFactor), "K-Factor must be greater than 0.");
        }
        _period = period;
        _kFactor = kFactor;
        _kFactorPeriod = kFactor * period;
        Name = "Mgdi";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the MGDI calculation.</param>
    /// <param name="kFactor">The K-factor controlling the decay rate adjustment (default 0.6).</param>
    public Mgdi(object source, int period, double kFactor = 0.6) : this(period, kFactor)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevMd = _p_prevMd = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_prevMd = _prevMd;
            _index++;
        }
        else
        {
            _prevMd = _p_prevMd;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateRatio(double value)
    {
        return _prevMd != 0 ? value / _prevMd : 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateMd(double value, double ratio)
    {
        return _prevMd + ((value - _prevMd) / (_kFactorPeriod * System.Math.Pow(ratio, 4)));
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        double value = Input.Value;
        if (_index < 2)
        {
            _prevMd = value;
        }
        else
        {
            double ratio = CalculateRatio(value);
            _prevMd = CalculateMd(value, ratio);
        }

        IsHot = _index >= _period;
        return _prevMd;
    }
}
