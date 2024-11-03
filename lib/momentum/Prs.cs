using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PRS: Price Relative Strength
/// A momentum indicator that compares the performance of a security against a benchmark,
/// helping identify which is showing stronger relative momentum.
/// </summary>
/// <remarks>
/// The PRS calculation process:
/// 1. Take the current price of the security
/// 2. Take the current price of the benchmark
/// 3. Calculate the ratio between them
/// 4. Multiply by a scaling factor for better visualization
///
/// Key characteristics:
/// - Measures relative performance against a benchmark
/// - Helps identify market leaders and laggards
/// - Rising PRS indicates outperformance
/// - Falling PRS indicates underperformance
///
/// Formula:
/// PRS = (Price / Benchmark) * 100
///
/// Sources:
///     Technical Analysis of Financial Markets by John J. Murphy
///     StockCharts.com Technical Indicators
/// </remarks>
[SkipLocalsInit]
public sealed class Prs : AbstractBase
{
    private const double ScalingFactor = 100.0;
    private double _benchmark;
    private double _p_benchmark;

    /// <summary>
    /// Initializes a new instance of the PRS indicator
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Prs()
    {
        WarmupPeriod = 1;
        Name = "PRS";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Prs(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    /// <summary>
    /// Sets the current benchmark value
    /// </summary>
    /// <param name="benchmark">The benchmark value to compare against</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBenchmark(double benchmark)
    {
        _benchmark = benchmark;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _p_benchmark = _benchmark;
        else
            _benchmark = _p_benchmark;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_benchmark <= double.Epsilon)
            return 0.0;

        return (Input.Value / _benchmark) * ScalingFactor;
    }
}
