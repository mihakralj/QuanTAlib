using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CVI: Chaikin's Volatility
/// A technical indicator developed by Marc Chaikin that measures the volatility of a financial instrument by comparing the spread between the high and low prices.
/// </summary>
/// <remarks>
/// The CVI calculation process:
/// 1. Calculates the difference between the high and low prices.
/// 2. Applies an exponential moving average (EMA) to the differences.
/// 3. Computes the percentage change in the EMA over a specified period.
///
/// Key characteristics:
/// - Measures volatility
/// - Uses high and low prices
/// - Percentage-based
/// - EMA smoothing
///
/// Formula:
/// CVI = (EMA(high - low, period) - EMA(high - low, period, offset)) / EMA(high - low, period, offset) * 100
///
/// Market Applications:
/// - Volatility assessment
/// - Trend confirmation
/// - Risk management
/// - Entry/exit timing
///
/// Sources:
///     Marc Chaikin - Original development
///     https://www.investopedia.com/terms/c/chaikins-volatility.asp
///
/// Note: Higher CVI values indicate higher volatility
/// </remarks>
[SkipLocalsInit]
public sealed class Cvi : AbstractBase
{
    private readonly int _period;
    private readonly Ema _ema;
    private readonly CircularBuffer _buffer;
    private double _prevEma;

    /// <param name="period">The number of periods for CVI calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cvi(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 1.");
        }
        _period = period;
        _ema = new Ema(period);
        _buffer = new CircularBuffer(period);
        WarmupPeriod = period;
        Name = $"CVI({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for CVI calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cvi(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _ema.Init();
        _buffer.Clear();
        _prevEma = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        double highLowDiff = BarInput.High - BarInput.Low;
        _buffer.Add(highLowDiff, BarInput.IsNew);

        double ema = _ema.Calc(new TValue(Input.Time, highLowDiff, BarInput.IsNew)).Value;

        double cvi = 0;
        if (_index >= _period)
        {
            double prevEma = _buffer[_buffer.Count - _period];
            cvi = (ema - prevEma) / prevEma * 100;
        }

        _prevEma = ema;
        IsHot = _index >= WarmupPeriod;
        return cvi;
    }
}
