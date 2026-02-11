using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Hodrick-Prescott Filter (HP): A causal approximation of the Hodrick-Prescott Filter trend component.
/// The standard HP filter is non-causal (two-sided), but this implementation uses a causal approximation
/// with O(1) complexity suitable for streaming data.
/// </summary>
/// <remarks>
/// The One-Sided Hodrick-Prescott filter is an approximation of the standard HP filter that relies only on current and past data.
/// Algorithm based on: https://github.com/mihakralj/pinescript/blob/main/filters/hp/hp.pine
/// Complexity: O(1)
/// </remarks>
[SkipLocalsInit]
public sealed class Hp : AbstractBase
{
    private readonly double _alpha;
    private readonly double _oneMinusAlpha;
    private readonly double _halfAlpha;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;
    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double Trend;
        public double PrevTrend;
        public double PrevPrice;
        public bool IsInitialized;
    }

    /// <summary>
    /// Smoothing parameter (lambda). Common values: 1600 (Quarterly), 14400 (Monthly), 6.25 (Annual).
    /// </summary>
    public double Lambda { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Hp"/> class.
    /// </summary>
    /// <param name="lambda">Smoothing parameter (lambda). Default is 1600.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when lambda is less than or equal to 0.</exception>
    public Hp(double lambda = 1600.0)
    {
        if (lambda <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lambda), "Lambda must be positive.");
        }

        Lambda = lambda;

        double s = Math.Sqrt(lambda);
        _alpha = (s * 0.5 - 1.0) / (s * 0.5 + 1.0);
        _alpha = Math.Clamp(_alpha, 0.0001, 0.9999);
        _oneMinusAlpha = 1.0 - _alpha;
        _halfAlpha = 0.5 * _alpha;

        Name = $"HP({lambda})";
        WarmupPeriod = (int)Math.Ceiling(s * 2);
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Hp"/> class with a publisher source.
    /// </summary>
    /// <param name="source">The source publisher.</param>
    /// <param name="lambda">Smoothing parameter (lambda).</param>
    public Hp(ITValuePublisher source, double lambda = 1600.0) : this(lambda)
    {
        _publisher = source;
        _handler = Handle;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _state = new State();
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value), isNew: true);
        }
    }

    public override bool IsHot => _state.IsInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double price = input.Value;

        if (!_state.IsInitialized)
        {
            // First bar
            _state.Trend = price;
            _state.PrevTrend = price;
            _state.PrevPrice = price;
            _state.IsInitialized = true;

            Last = new TValue(input.Time, price);
            PubEvent(Last, isNew);
            return Last;
        }

        double prevTrend = _state.Trend;
        double prevPrevTrend = _state.PrevTrend;

        double val = _oneMinusAlpha * price;
        val = Math.FusedMultiplyAdd(_alpha, prevTrend, val);
        val = Math.FusedMultiplyAdd(_halfAlpha, prevTrend - prevPrevTrend, val);

        double currentTrend = val;

        if (isNew)
        {
            _state.PrevTrend = prevTrend;
            _state.Trend = currentTrend;
            _state.PrevPrice = price;
        }
        else
        {
            _state.Trend = currentTrend;
        }

        Last = new TValue(input.Time, currentTrend);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var resultValues = new double[source.Count];
        Batch(source.Values, resultValues, Lambda);

        var result = new TSeries();
        var times = source.Times;
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(new TValue(times[i], resultValues[i]));
        }

        // Set state to match end of batch
        if (source.Count > 1)
        {
            _state.Trend = resultValues[^1];
            _state.PrevTrend = resultValues[^2];
            _state.PrevPrice = source.Values[^1];
            _state.IsInitialized = true;
        }
        else
        {
            _state.Trend = resultValues[^1];
            _state.PrevTrend = resultValues[^1];
            _state.PrevPrice = source.Values[^1];
            _state.IsInitialized = true;
        }

        // Synchronize _p_state with _state so subsequent isNew=false calls don't rollback to stale values
        _p_state = _state;

        return result;
    }

    public static TSeries Batch(TSeries source, double lambda = 1600.0)
    {
        var indicator = new Hp(lambda);
        return indicator.Update(source);
    }

    /// <summary>
    /// Static calculation of HP Filter on a span.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double lambda)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        double s = Math.Sqrt(lambda);
        double alpha = (s * 0.5 - 1.0) / (s * 0.5 + 1.0);
        alpha = Math.Clamp(alpha, 0.0001, 0.9999);

        double oneMinusAlpha = 1.0 - alpha;
        double halfAlpha = 0.5 * alpha;

        double prevTrend = source[0];
        double prevPrevTrend = source[0];

        output[0] = source[0];

        for (int i = 1; i < source.Length; i++)
        {
            double price = source[i];
            double val = oneMinusAlpha * price;
            val = Math.FusedMultiplyAdd(alpha, prevTrend, val);
            val = Math.FusedMultiplyAdd(halfAlpha, prevTrend - prevPrevTrend, val);

            output[i] = val;

            prevPrevTrend = prevTrend;
            prevTrend = val;
        }
    }
    public static (TSeries Results, Hp Indicator) Calculate(TSeries source, double lambda = 1600.0)
    {
        var indicator = new Hp(lambda);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Unsubscribes from the source publisher if one was provided during construction.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}