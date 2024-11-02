using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// DPO: Detrended Price Oscillator
/// A momentum indicator that removes the trend from price by comparing the current price
/// to a past moving average, helping to identify cycles in the price.
/// </summary>
/// <remarks>
/// The DPO calculation process:
/// 1. Calculate the period shifted back by (period / 2 + 1) days
/// 2. Calculate SMA for the shifted period
/// 3. DPO = Price - SMA(Price, period) shifted back
///
/// Key characteristics:
/// - Removes long-term trends
/// - Helps identify cycles
/// - Oscillates above and below zero
/// - Default period is 20 days
/// - Uses price displacement
///
/// Formula:
/// DPO = Price - SMA(Price, period) shifted (period/2 + 1) bars back
///
/// Market Applications:
/// - Cycle identification
/// - Overbought/Oversold conditions
/// - Price momentum
/// - Trading signals
/// - Market timing
///
/// Sources:
///     Donald Dorsey - Original development
///     https://www.investopedia.com/terms/d/detrended-price-oscillator-dpo.asp
///
/// Note: DPO helps identify cycles by removing the trend component from the price data
/// </remarks>

[SkipLocalsInit]
public sealed class Dpo : AbstractBase
{
    private readonly int _shift;
    private readonly CircularBuffer _prices;
    private readonly CircularBuffer _sma;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dpo(int period = 20)
    {
        _shift = period / 2 + 1;
        WarmupPeriod = period + _shift;
        Name = $"DPO({period})";
        _prices = new CircularBuffer(WarmupPeriod);
        _sma = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dpo(object source, int period = 20) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prices.Clear();
        _sma.Clear();
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

        // Add current price to buffer
        _prices.Add(BarInput.Close);

        // Need enough prices for the shifted SMA calculation
        if (_index <= _shift)
        {
            return 0;
        }

        // Add price from shift periods ago to SMA buffer
        _sma.Add(_prices[_shift]);

        // Need enough prices for full calculation
        if (_index <= WarmupPeriod)
        {
            return 0;
        }

        // Calculate DPO
        double dpo = BarInput.Close - _sma.Average();

        IsHot = _index >= WarmupPeriod;
        return dpo;
    }
}
