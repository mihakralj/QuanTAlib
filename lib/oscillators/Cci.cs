using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CCI: Commodity Channel Index
/// A momentum oscillator used to identify cyclical trends and measure the deviation of price
/// from its statistical mean.
/// </summary>
/// <remarks>
/// The CCI calculation process:
/// 1. Calculate Typical Price (TP) = (High + Low + Close) / 3
/// 2. Calculate Simple Moving Average of TP
/// 3. Calculate Mean Deviation
/// 4. CCI = (TP - SMA(TP)) / (0.015 * Mean Deviation)
///
/// Key characteristics:
/// - Oscillates above and below zero
/// - Typically ranges between +100 and -100
/// - Values above +100 indicate overbought conditions
/// - Values below -100 indicate oversold conditions
/// - Can identify trend strength and reversals
///
/// Formula:
/// CCI = (TypicalPrice - SMA(TypicalPrice, period)) / (0.015 * MeanDeviation)
/// where:
/// - TypicalPrice = (High + Low + Close) / 3
/// - MeanDeviation = Mean(|TP - SMA(TP)|)
///
/// Sources:
///     Donald Lambert (1980)
///     https://www.investopedia.com/terms/c/commoditychannelindex.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Cci : AbstractBase
{
    private readonly int _period;
    private readonly Sma _sma;
    private readonly double[] _typicalPrices;
    private readonly double _constant = 0.015;

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The calculation period (default: 20)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cci(object source, int period = 20) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cci(int period = 20)
    {
        _period = period;
        _sma = new Sma(period);
        _typicalPrices = new double[period];
        WarmupPeriod = period;
        Name = "CCI";
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
    private double CalculateMeanDeviation(double typicalPrice, double smaValue)
    {
        var sum = 0.0;
        var count = System.Math.Min(_period, _index + 1);

        for (var i = 0; i < count; i++)
        {
            sum += System.Math.Abs(_typicalPrices[i] - smaValue);
        }

        return sum / count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        var typicalPrice = (BarInput.High + BarInput.Low + BarInput.Close) / 3.0;
        var idx = _index % _period;
        _typicalPrices[idx] = typicalPrice;

        var smaValue = _sma.Calc(typicalPrice, BarInput.IsNew);
        if (_index < _period - 1) return double.NaN;

        var meanDeviation = CalculateMeanDeviation(typicalPrice, smaValue);
        if (meanDeviation <= double.Epsilon) return 0;

        return (typicalPrice - smaValue) / (_constant * meanDeviation);
    }
}
