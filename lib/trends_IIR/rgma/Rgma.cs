using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RGMA: Recursive Gaussian Moving Average
/// </summary>
/// <remarks>
/// RGMA approximates Gaussian smoothing by applying the same 1-pole exponential
/// filter multiple times (passes). More passes push the impulse response toward
/// a Gaussian-like shape while keeping O(passes) per update (passes is small).
///
/// Pine reference:
/// alpha = 2 / (period / sqrt(passes) + 1)
/// filter0 = ema(source)
/// filteri = ema(filter{i-1})
/// output = filter{passes-1}
/// </remarks>
[SkipLocalsInit]
public sealed class Rgma : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(double E, bool IsHot, bool IsInitialized, int TickCount)
    {
        public static State New() => new() { E = 1.0, IsHot = false, IsInitialized = false, TickCount = 0 };
    }

    private readonly int _passes;
    private readonly double _alpha;
    private readonly double _decay;

    private State _state = State.New();
    private State _p_state = State.New();

    private readonly double[] _filters;
    private readonly double[] _p_filters;

    private double _lastValidValue;
    private double _p_lastValidValue;

    private ITValuePublisher? _publisher;
    private bool _disposed;

    private const double COVERAGE_THRESHOLD = 0.05;
    private const int ResyncInterval = 10000;
    private const int StackAllocThreshold = 512;
    public override bool IsHot => _state.IsHot;

    /// <summary>
    /// Creates RGMA with specified period and passes.
    /// </summary>
    /// <param name="period">Effective smoothing period (must be &gt; 0)</param>
    /// <param name="passes">Number of recursive passes (must be &gt; 0)</param>
    public Rgma(int period, int passes = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(passes);

        _passes = passes;

        _alpha = 2.0 / (period / Math.Sqrt(passes) + 1.0);
        _decay = 1.0 - _alpha;

        _filters = new double[_passes];
        _p_filters = new double[_passes];
        Array.Fill(_filters, double.NaN);
        Array.Fill(_p_filters, double.NaN);

        Name = $"Rgma({period},{passes})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Creates RGMA with specified source and parameters.
    /// Subscribes to source.Pub event.
    /// </summary>
    public Rgma(ITValuePublisher source, int period, int passes = 3) : this(period, passes)
    {
        _publisher = source;
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates RGMA from TSeries source with auto-subscription.
    /// </summary>
    public Rgma(TSeries source, int period, int passes = 3) : this(period, passes)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _publisher = source;
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _state = State.New();
        _p_state = State.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Array.Fill(_filters, double.NaN);
        Array.Fill(_p_filters, double.NaN);

        int len = source.Length;

        bool foundValid = false;
        for (int k = 0; k < len; k++)
        {
            if (double.IsFinite(source[k]))
            {
                _lastValidValue = source[k];
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            Last = new TValue(DateTime.MinValue, double.NaN);
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
            return;
        }

        double[]? rented = len > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> tempOutput = rented != null
            ? rented.AsSpan(0, len)
            : stackalloc double[len];

        double[]? filtersRented = _passes > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(_passes) : null;
        Span<double> tempFilters = filtersRented != null
            ? filtersRented.AsSpan(0, _passes)
            : stackalloc double[_passes];

        try
        {
            tempFilters.Fill(double.NaN);

            State state = _state;
            double lastValid = _lastValidValue;

            CalculateCore(source, tempOutput, _alpha, _decay, tempFilters, ref state, ref lastValid);

            _state = state;
            _lastValidValue = lastValid;
            tempFilters.CopyTo(_filters);

            Last = new TValue(DateTime.MinValue, tempOutput[len - 1]);
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
            Array.Copy(_filters, _p_filters, _passes);
        }
        finally
        {
            if (filtersRented != null)
            {
                ArrayPool<double>.Shared.Return(filtersRented);
            }

            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
            if (_passes <= 8)
            {
                for (int i = 0; i < _passes; i++)
                {
                    _p_filters[i] = _filters[i];
                }
            }
            else
            {
                Array.Copy(_filters, _p_filters, _passes);
            }
        }
        else
        {
            _state = _p_state;
            _lastValidValue = _p_lastValidValue;
            if (_passes <= 8)
            {
                for (int i = 0; i < _passes; i++)
                {
                    _filters[i] = _p_filters[i];
                }
            }
            else
            {
                Array.Copy(_p_filters, _filters, _passes);
            }
        }

        double x = GetValidValue(input.Value);
        double y = Compute(x, _alpha, _decay, _filters, ref _state);
        Last = new TValue(input.Time, y);
        PubEvent(Last, isNew);
        return Last;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        State state = _state;
        double lastValidValue = _lastValidValue;

        CalculateCore(sourceValues, vSpan, _alpha, _decay, _filters, ref state, ref lastValidValue);

        _state = state;
        _lastValidValue = lastValidValue;

        sourceTimes.CopyTo(tSpan);

        _p_state = _state;
        _p_lastValidValue = _lastValidValue;
        Array.Copy(_filters, _p_filters, _passes);
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double alpha, double decay, Span<double> filters, ref State state)
    {
        if (!state.IsInitialized)
        {
            filters.Fill(input);
            state.IsInitialized = true;
            state.TickCount = 1;
            state.E *= decay;
            if (state.E <= COVERAGE_THRESHOLD)
            {
                state.IsHot = true;
            }

            return input;
        }

        // Stage 0
        filters[0] = Math.FusedMultiplyAdd(alpha, input - filters[0], filters[0]);
        for (int i = 1; i < filters.Length; i++)
        {
            filters[i] = Math.FusedMultiplyAdd(alpha, filters[i - 1] - filters[i], filters[i]);
        }

        state.TickCount++;
        state.E *= decay;
        if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
        {
            state.IsHot = true;
        }

        if (state.TickCount >= ResyncInterval)
        {
            state.TickCount = 0;
        }

        return filters[^1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(
        ReadOnlySpan<double> source,
        Span<double> output,
        double alpha,
        double decay,
        Span<double> filters,
        ref State state,
        ref double lastValid)
    {
        ref double outRef = ref MemoryMarshal.GetReference(output);
        for (int i = 0; i < source.Length; i++)
        {
            double x = source[i];
            if (double.IsFinite(x))
            {
                lastValid = x;
            }
            else
            {
                x = lastValid;
            }

            double y;
            if (!state.IsInitialized)
            {
                filters.Fill(x);
                state.IsInitialized = true;
                state.TickCount = 1;
                state.E *= decay;
                if (state.E <= COVERAGE_THRESHOLD)
                {
                    state.IsHot = true;
                }

                y = x;
            }
            else
            {
                filters[0] = Math.FusedMultiplyAdd(alpha, x - filters[0], filters[0]);
                for (int p = 1; p < filters.Length; p++)
                {
                    filters[p] = Math.FusedMultiplyAdd(alpha, filters[p - 1] - filters[p], filters[p]);
                }

                state.TickCount++;
                state.E *= decay;
                if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
                {
                    state.IsHot = true;
                }

                if (state.TickCount >= ResyncInterval)
                {
                    state.TickCount = 0;
                }

                y = filters[^1];
            }

            Unsafe.Add(ref outRef, i) = y;
        }
    }

    /// <summary>
    /// Runs a high-performance batch calculation and returns a hot RGMA instance.
    /// </summary>

    /// <summary>
    /// Calculates RGMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period, int passes = 3)
    {
        var rgma = new Rgma(period, passes);
        return rgma.Update(source);
    }

    /// <summary>
    /// Calculates RGMA in-place using period and passes, writing results to a pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, int passes = 3)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (passes <= 0)
        {
            throw new ArgumentException("Passes must be greater than 0", nameof(passes));
        }

        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        double alpha = 2.0 / (period / Math.Sqrt(passes) + 1.0);
        double decay = 1.0 - alpha;

        var state = State.New();
        double lastValid = 0;
        bool foundValid = false;

        for (int k = 0; k < source.Length; k++)
        {
            if (double.IsFinite(source[k]))
            {
                lastValid = source[k];
                foundValid = true;
                break;
            }
        }

        if (!foundValid)
        {
            output.Fill(double.NaN);
            return;
        }

        double[]? rented = passes > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(passes) : null;
        Span<double> filters = rented != null
            ? rented.AsSpan(0, passes)
            : stackalloc double[passes];

        try
        {
            filters.Fill(double.NaN);
            CalculateCore(source, output, alpha, decay, filters, ref state, ref lastValid);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }
    public static (TSeries Results, Rgma Indicator) Calculate(TSeries source, int period, int passes = 3)
    {
        var rgma = new Rgma(period, passes);
        TSeries results = rgma.Update(source);
        return (results, rgma);
    }
    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Array.Fill(_filters, double.NaN);
        Array.Fill(_p_filters, double.NaN);
        Last = default;
    }
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _publisher != null)
            {
                _publisher.Pub -= Handle;
                _publisher = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}