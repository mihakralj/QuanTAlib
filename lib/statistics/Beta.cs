using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// BETA: Beta Coefficient
/// A statistical measure that quantifies the volatility of an asset or portfolio
/// in relation to the overall market. Beta is used to assess the risk and return
/// characteristics of an investment.
/// </summary>
/// <remarks>
/// The Beta calculation process:
/// 1. Calculates covariance between asset and market returns
/// 2. Computes variance of market returns
/// 3. Divides covariance by market variance
///
/// Key characteristics:
/// - Measures relative volatility
/// - Beta > 1: More volatile than market
/// - Beta < 1: Less volatile than market
/// - Beta = 1: Same volatility as market
/// - Beta < 0: Inverse relationship with market
///
/// Formula:
/// β = Cov(Ra, Rm) / Var(Rm)
/// where:
/// Ra = asset returns
/// Rm = market returns
///
/// Market Applications:
/// - Risk assessment
/// - Portfolio management
/// - Asset allocation
/// - Performance analysis
/// - Hedging strategies
///
/// Sources:
///     https://en.wikipedia.org/wiki/Beta_(finance)
///     "Modern Portfolio Theory" - Harry Markowitz
///
/// Note: Assumes linear relationship between asset and market returns
/// </remarks>
[SkipLocalsInit]
public sealed class Beta : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _assetReturns;
    private readonly CircularBuffer _marketReturns;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for beta calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Beta(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for beta calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _assetReturns = new CircularBuffer(period);
        _marketReturns = new CircularBuffer(period);
        Name = $"Beta(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for beta calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Beta(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _assetReturns.Clear();
        _marketReturns.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateMean(ReadOnlySpan<double> values)
    {
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        return sum / values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateCovariance(ReadOnlySpan<double> assetReturns, ReadOnlySpan<double> marketReturns, double assetMean, double marketMean)
    {
        double covariance = 0;
        for (int i = 0; i < assetReturns.Length; i++)
        {
            covariance += (assetReturns[i] - assetMean) * (marketReturns[i] - marketMean);
        }
        return covariance / assetReturns.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateVariance(ReadOnlySpan<double> values, double mean)
    {
        double variance = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            variance += diff * diff;
        }
        return variance / values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _assetReturns.Add(Input.Value, Input.IsNew);
        _marketReturns.Add(Input2.Value, Input.IsNew);

        double beta = 0;
        if (_assetReturns.Count >= MinimumPoints && _marketReturns.Count >= MinimumPoints)
        {
            ReadOnlySpan<double> assetValues = _assetReturns.GetSpan();
            ReadOnlySpan<double> marketValues = _marketReturns.GetSpan();

            double assetMean = CalculateMean(assetValues);
            double marketMean = CalculateMean(marketValues);

            double covariance = CalculateCovariance(assetValues, marketValues, assetMean, marketMean);
            double marketVariance = CalculateVariance(marketValues, marketMean);

            if (marketVariance > Epsilon)
            {
                beta = covariance / marketVariance;
            }
        }

        IsHot = _assetReturns.Count >= Period && _marketReturns.Count >= Period;
        return beta;
    }
}
