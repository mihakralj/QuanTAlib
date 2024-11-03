using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VF: Volume Force
/// A volume-based indicator that measures the strength of volume relative to price
/// movement. It helps identify whether volume is supporting or contradicting the
/// current price trend.
/// </summary>
/// <remarks>
/// The VF calculation process:
/// 1. Calculate price change
/// 2. Calculate volume force as volume * price change
/// 3. Optionally smooth the result with EMA
///
/// Key characteristics:
/// - Volume-weighted measure
/// - Trend strength indicator
/// - No upper/lower bounds
/// - Raw and smoothed versions
/// - Divergence indicator
///
/// Formula:
/// VF = Volume * (Close - Close[1])
/// Smoothed VF = EMA(VF, period)
///
/// Market Applications:
/// - Volume analysis
/// - Trend confirmation
/// - Price/volume divergence
/// - Market participation
/// - Momentum confirmation
///
/// Note: Higher values indicate stronger volume force
/// </remarks>
[SkipLocalsInit]
public sealed class Vf : AbstractBase
{
    private readonly Ema _ema;
    private double _prevClose;
    private double _p_prevClose;
    private const int DefaultPeriod = 13;

    /// <param name="period">The smoothing period for EMA calculation (default 13).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vf(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _ema = new(period);
        WarmupPeriod = period + 1;
        Name = $"VF({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The smoothing period for EMA calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vf(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _ema.Init();
        _prevClose = double.NaN;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevClose = _prevClose;
        }
        else
        {
            _prevClose = _p_prevClose;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate raw volume force
        double priceChange = BarInput.Close - _prevClose;
        double volumeForce = BarInput.Volume * priceChange;

        // Update previous close
        _prevClose = BarInput.Close;

        // Apply EMA smoothing
        return _ema.Calc(volumeForce, BarInput.IsNew);
    }
}
