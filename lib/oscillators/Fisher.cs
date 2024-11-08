using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// FISHER: Fisher Transform
/// A technical indicator that converts prices into a Gaussian normal distribution.
/// </summary>
/// <remarks>
/// The Fisher Transform calculation process:
/// 1. Calculate the value of the price relative to its high-low range.
/// 2. Apply the Fisher Transform formula to the normalized price.
/// 3. Smooth the result using an exponential moving average.
///
/// Key characteristics:
/// - Oscillates between -1 and 1
/// - Emphasizes price reversals
/// - Can be used to identify overbought and oversold conditions
///
/// Formula:
/// Fisher Transform = 0.5 * log((1 + x) / (1 - x))
/// where:
/// x = 2 * ((price - min) / (max - min) - 0.5)
///
/// Sources:
///     John F. Ehlers - "Rocket Science for Traders" (2001)
///     https://www.investopedia.com/terms/f/fisher-transform.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Fisher : AbstractBase
{
    private readonly int _period;
    private readonly double[] _prices;
    private double _prevFisher;
    private double _prevValue;

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The calculation period (default: 10)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fisher(object source, int period = 10) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Fisher(int period = 10)
    {
        _period = period;
        _prices = new double[period];
        WarmupPeriod = period;
        Name = "FISHER";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double NormalizePrice(double price, double min, double max)
    {
        return 2 * (((price - min) / (max - min)) - 0.5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double FisherTransform(double value)
    {
        return 0.5 * System.Math.Log((1 + value) / (1 - value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        var idx = _index % _period;
        _prices[idx] = Input.Value;

        if (_index < _period - 1) return double.NaN;

        var min = _prices.Min();
        var max = _prices.Max();
        var normalizedPrice = NormalizePrice(Input.Value, min, max);
        var fisherValue = FisherTransform(normalizedPrice);

        var smoothedFisher = 0.5 * (fisherValue + _prevFisher);
        _prevFisher = smoothedFisher;

        return smoothedFisher;
    }
}
