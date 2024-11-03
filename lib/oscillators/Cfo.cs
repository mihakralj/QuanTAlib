using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CFO: Chande Forecast Oscillator
/// A momentum oscillator that measures the percentage difference between the actual price
/// and its linear regression forecast value.
/// </summary>
/// <remarks>
/// The CFO calculation process:
/// 1. Calculate linear regression forecast value for the current period
/// 2. Calculate percentage difference between actual price and forecast
///
/// Key characteristics:
/// - Oscillates above and below zero
/// - Measures deviation of price from its forecasted value
/// - Positive values indicate price is above forecast (bullish)
/// - Negative values indicate price is below forecast (bearish)
/// - Can identify potential trend reversals and price divergences
///
/// Formula:
/// CFO = ((Price - Forecast) / Price) * 100
/// where:
/// - Price is typically the closing price
/// - Forecast is the linear regression forecast value
///
/// Sources:
///     Tushar Chande (1990s)
///     Technical Analysis of Stocks and Commodities magazine
/// </remarks>

[SkipLocalsInit]
public sealed class Cfo : AbstractBase
{
    private readonly int _period;
    private readonly double[] _prices;
    private double _sumX;
    private double _sumY;
    private double _sumXY;
    private double _sumX2;

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The calculation period (default: 14)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cfo(object source, int period = 14) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cfo(int period = 14)
    {
        _period = period;
        _prices = new double[period];
        WarmupPeriod = period;
        Name = "CFO";
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
    private void UpdateSums(double oldPrice, double newPrice, int oldX, int newX)
    {
        _sumY -= oldPrice;
        _sumY += newPrice;
        _sumXY -= oldPrice * oldX;
        _sumXY += newPrice * newX;
        _sumX -= oldX;
        _sumX += newX;
        _sumX2 -= oldX * oldX;
        _sumX2 += newX * newX;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateForecast()
    {
        var count = System.Math.Min(_period, _index + 1);
        var n = (double)count;

        // Calculate linear regression coefficients
        var slope = ((n * _sumXY) - (_sumX * _sumY)) / ((n * _sumX2) - (_sumX * _sumX));
        var intercept = (_sumY - (slope * _sumX)) / n;

        // Calculate forecast for next period
        return intercept + (slope * count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        var price = Input.Value;
        var idx = _index % _period;
        var oldPrice = _prices[idx];
        _prices[idx] = price;

        var oldX = idx + 1;
        var newX = _index < _period ? idx + 1 : _period;

        UpdateSums(oldPrice, price, oldX, newX);
        if (_index < _period - 1) return double.NaN;

        var forecast = CalculateForecast();
        if (price <= double.Epsilon) return 0;

        return ((price - forecast) / price) * 100;
    }
}
