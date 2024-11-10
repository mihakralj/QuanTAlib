using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// KENDALL: Kendall's Rank Correlation Coefficient (Tau)
/// A nonparametric measure that evaluates the degree of similarity between two sets
/// of rankings by analyzing concordant and discordant pairs. Unlike Spearman correlation,
/// Kendall's tau measures the ordinal association between two variables.
/// </summary>
/// <remarks>
/// The Kendall calculation process:
/// 1. Compares each pair of observations
/// 2. Counts concordant and discordant pairs
/// 3. Handles ties in both variables
///
/// Key characteristics:
/// - Measures ordinal association
/// - Range: -1 to +1
/// - Robust to outliers
/// - More intuitive probabilistic interpretation
/// - Less sensitive to error than Spearman
///
/// Formula:
/// τ = (nc - nd) / sqrt((n0 - n1)(n0 - n2))
/// where:
/// nc = number of concordant pairs
/// nd = number of discordant pairs
/// n0 = n(n-1)/2
/// n1 = sum(u(u-1)/2) for ties in x
/// n2 = sum(v(v-1)/2) for ties in y
///
/// Market Applications:
/// - Rank correlation analysis
/// - Portfolio diversification
/// - Risk assessment
/// - Market trend analysis
/// - Pattern recognition
///
/// Sources:
///     https://en.wikipedia.org/wiki/Kendall_rank_correlation_coefficient
///     "Rank Correlation Methods" - Maurice G. Kendall
///
/// Note: More robust to outliers and errors than other correlation measures
/// </remarks>
[SkipLocalsInit]
public sealed class Kendall : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _xValues;
    private readonly CircularBuffer _yValues;
    private const double Epsilon = 1e-10;
    private const int MinimumPoints = 2;

    /// <param name="period">The number of points to consider for Kendall correlation calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 2.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Kendall(int period)
    {
        if (period < MinimumPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 2 for Kendall correlation calculation.");
        }
        Period = period;
        WarmupPeriod = MinimumPoints;
        _xValues = new CircularBuffer(period);
        _yValues = new CircularBuffer(period);
        Name = $"Kendall(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for Kendall correlation calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Kendall(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _xValues.Clear();
        _yValues.Clear();
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
    private static (int concordant, int discordant, int tiesX, int tiesY) CountPairs(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        int n = x.Length;
        int concordant = 0;
        int discordant = 0;
        int tiesX = 0;
        int tiesY = 0;

        for (int i = 0; i < n - 1; i++)
        {
            if (double.IsNaN(x[i]) || double.IsNaN(y[i])) continue;

            for (int j = i + 1; j < n; j++)
            {
                if (double.IsNaN(x[j]) || double.IsNaN(y[j])) continue;

                double xDiff = x[i] - x[j];
                double yDiff = y[i] - y[j];

                if (Math.Abs(xDiff) < Epsilon && Math.Abs(yDiff) < Epsilon)
                {
                    tiesX++;
                    tiesY++;
                }
                else if (Math.Abs(xDiff) < Epsilon)
                {
                    tiesX++;
                }
                else if (Math.Abs(yDiff) < Epsilon)
                {
                    tiesY++;
                }
                else
                {
                    int xSign = xDiff > 0 ? 1 : -1;
                    int ySign = yDiff > 0 ? 1 : -1;
                    if (xSign == ySign)
                    {
                        concordant++;
                    }
                    else
                    {
                        discordant++;
                    }
                }
            }
        }

        return (concordant, discordant, tiesX, tiesY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _xValues.Add(Input.Value, Input.IsNew);
        _yValues.Add(Input2.Value, Input.IsNew);

        double correlation = 0;
        if (_xValues.Count >= MinimumPoints && _yValues.Count >= MinimumPoints)
        {
            ReadOnlySpan<double> xValues = _xValues.GetSpan();
            ReadOnlySpan<double> yValues = _yValues.GetSpan();

            var (concordant, discordant, tiesX, tiesY) = CountPairs(xValues, yValues);

            int n = xValues.Length;
            int n0 = (n * (n - 1)) / 2;

            // Calculate denominator considering ties
            double denominator = Math.Sqrt((n0 - tiesX) * (n0 - tiesY));

            if (denominator > Epsilon)
            {
                correlation = (concordant - discordant) / denominator;
            }
        }

        IsHot = _xValues.Count >= Period && _yValues.Count >= Period;
        return correlation;
    }
}
