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
    private readonly double _lambda;
    private readonly double _alpha;
    private readonly double _oneMinusAlpha;
    private readonly double _halfAlpha;
    private State _state;
    private State _p_state;

    [StructLayout(LayoutKind.Auto)]
    private struct State
    {
        public double Trend;
        public double PrevTrend;
        public double PrevPrice;
        public bool IsInitialized;
    }

    /// <summary>
    /// Smoothing parameter (lambda). Common values: 1600 (Quarterly), 14400 (Monthly), 6.25 (Annual).
    /// </summary>
    public double Lambda => _lambda;

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

        _lambda = lambda;
        
        // Alpha calculation from Pine Script approximation
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
        source.Pub += Handle;
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
            Update(new TValue(DateTime.MinValue, value), true);
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
        double prevPrevTrend = _state.PrevTrend; // Pine: nz(hp_trend[2], prev_trend)
        
        // Manual optimization:
        // currentTrend = (1 - alpha) * price + alpha * prevTrend + 0.5 * alpha * (prevTrend - prevPrevTrend)
        // Group alpha terms:
        // = (1 - alpha) * price + alpha * (prevTrend + 0.5 * (prevTrend - prevPrevTrend))
        
        // Using FMA:
        // term1 = prevTrend - prevPrevTrend
        // term2 = Math.FusedMultiplyAdd(0.5, term1, prevTrend)
        // term3 = Math.FusedMultiplyAdd(_alpha, term2, _oneMinusAlpha * price) -> FMA not perfect here due to structure
        // Let's stick to simple FMA chain if possible
        
        // target: _oneMinusAlpha * price + _alpha * prevTrend + _halfAlpha * (prevTrend - prevPrevTrend)
        // = _oneMinusAlpha * price + _alpha * prevTrend + _halfAlpha * prevTrend - _halfAlpha * prevPrevTrend
        // = _oneMinusAlpha * price + (_alpha + _halfAlpha) * prevTrend - _halfAlpha * prevPrevTrend
        
        // Precompute (_alpha + _halfAlpha) could be useful
        // But let's use FMA on the original form for precision:
        
        // val = _oneMinusAlpha * price
        // val = FMA(_alpha, prevTrend, val)
        // diff = prevTrend - prevPrevTrend
        // val = FMA(_halfAlpha, diff, val)
        
        double val = _oneMinusAlpha * price;
        val = Math.FusedMultiplyAdd(_alpha, prevTrend, val);
        val = Math.FusedMultiplyAdd(_halfAlpha, prevTrend - prevPrevTrend, val);
        
        double currentTrend = val;
        
        if (isNew)
        {
            _state.PrevTrend = prevTrend; // Shift old trend to prevPrevTrend position effectively? 
            // Wait, logic:
            // hp_trend[0] = currentTrend
            // hp_trend[1] = prevTrend
            // hp_trend[2] = prevPrevTrend
            
            // In next step:
            // prevTrend will be currentTrend
            // prevPrevTrend will be prevTrend
            
            _state.PrevTrend = prevTrend; // Storing hp_trend[1] for next iteration's hp_trend[2] use
            // Actually, for next iteration:
            // next_prevTrend = currentTrend
            // next_prevPrevTrend = prevTrend (which is current _state.Trend before update)
            
            // So we need to store currentTrend as Trend
            // And store prevTrend (the old Trend) as PrevTrend
            
            _state.PrevTrend = prevTrend;
            _state.Trend = currentTrend;
            _state.PrevPrice = price;
        }
        else
        {
             // For non-new updates, we don't update state permanently, handled by rollback
             // But we need to update 'Trend' in _state so Last works?
             // Actually structure is: _state is modified in place.
             // If isNew=false, we restored _state from _p_state at start.
             
             // So here we just update _state.Trend to current result
             _state.Trend = currentTrend;
             // _state.PrevTrend remains what it was in _p_state (correct, as it's history)
        }

        Last = new TValue(input.Time, currentTrend);
        PubEvent(Last, isNew);
        return Last;
    }
    
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        var resultValues = new double[source.Count];
        Calculate(source.Values, resultValues, _lambda);

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

        return result;
    }
    
    /// <summary>
    /// Static calculation of HP Filter on a span.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double lambda)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output spans must be of equal length.", nameof(output));
        }
        
        if (source.Length == 0) return;

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
}
