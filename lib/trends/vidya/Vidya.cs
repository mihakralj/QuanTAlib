using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VIDYA: Variable Index Dynamic Average
/// </summary>
/// <remarks>
/// VIDYA is an adaptive moving average developed by Tushar Chande.
/// It adjusts the smoothing constant of an Exponential Moving Average (EMA) based on a volatility index.
/// The volatility index used is the Chande Momentum Oscillator (CMO).
///
/// Formula:
/// alpha = 2 / (period + 1)
/// CMO = (Sum(Up) - Sum(Down)) / (Sum(Up) + Sum(Down))
/// VI = Abs(CMO)
/// DynamicAlpha = alpha * VI
/// VIDYA = DynamicAlpha * Price + (1 - DynamicAlpha) * VIDYA_prev
///
/// Key characteristics:
/// - Adapts to market volatility
/// - Flattens in ranging markets (low volatility)
/// - Reacts quickly in trending markets (high volatility)
/// </remarks>
[SkipLocalsInit]
public sealed class Vidya : ITValuePublisher
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly RingBuffer _ups;
    private readonly RingBuffer _downs;
    
    private double _prevClose;
    private double _lastVidya;
    private double _currentClose;
    private double _currentVidya;
    private bool _isInitialized;
    private int _barCount;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    public TValue Last { get; private set; }

    /// <summary>
    /// Creates VIDYA with specified period.
    /// </summary>
    /// <param name="period">Period for calculation (must be > 0)</param>
    public Vidya(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _alpha = 2.0 / (period + 1);
        _ups = new RingBuffer(period);
        _downs = new RingBuffer(period);
        Name = $"Vidya({period})";
    }

    /// <summary>
    /// Creates VIDYA with specified source and period.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for calculation</param>
    public Vidya(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _barCount++;
            if (_isInitialized)
            {
                _prevClose = _currentClose;
                _lastVidya = _currentVidya;
            }
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            // Handle NaN/Infinity by using the last known valid values
            // If not initialized, we can't do much, just return input
            if (!_isInitialized) return input;
            price = _currentClose; // Use last valid close
        }

        if (_barCount <= 1)
        {
            _prevClose = price;
            _lastVidya = price;
            _currentClose = price;
            _currentVidya = price;
            _isInitialized = true;
            _ups.Add(0, isNew);
            _downs.Add(0, isNew);
            Last = new TValue(input.Time, _currentVidya);
            Pub?.Invoke(Last);
            return Last;
        }

        double change = price - _prevClose;
        double up = change > 0 ? change : 0;
        double down = change < 0 ? -change : 0;

        _ups.Add(up, isNew);
        _downs.Add(down, isNew);

        double sumUp = _ups.Sum;
        double sumDown = _downs.Sum;
        double sum = sumUp + sumDown;

        double vi = 0;
        if (sum > double.Epsilon)
        {
            vi = Math.Abs(sumUp - sumDown) / sum;
        }

        double dynamicAlpha = _alpha * vi;
        _currentVidya = dynamicAlpha * price + (1.0 - dynamicAlpha) * _lastVidya;
        _currentClose = price;

        Last = new TValue(input.Time, _currentVidya);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        // We can't easily use a static Calculate here because of the complex state (RingBuffers)
        // So we'll iterate and use the instance Update logic, but optimized for series
        // Actually, we can implement a static Calculate that uses temporary buffers
        
        Calculate(sourceValues, vSpan, _period);

        sourceTimes.CopyTo(tSpan);
        
        // Update internal state to match the end of the series
        // This is tricky because Calculate is static and doesn't update instance state.
        // To support "Update(TSeries)", we should probably just run the instance update loop.
        // But for performance, we want to use the static method if possible.
        // The standard pattern in this library seems to be:
        // 1. Call static Calculate to fill the output
        // 2. Re-run the last N updates on the instance to sync state
        
        // Re-sync state
        // We need to feed at least 'period' bars to fill the buffers
        // But since VIDYA is recursive, we really need the whole history to match exactly.
        // So for VIDYA, it's safer to just reset and run the update loop.
        
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(sourceTimes[i], sourceValues[i]), true);
        }
        
        // Overwrite the vSpan with the results we just calculated? 
        // Or just trust the loop we just ran.
        // Since we ran the loop, 'v' is already populated? No, Update(TValue) updates 'Last', not a list.
        // So we need to populate 'v'.
        
        // Let's do this:
        // 1. Reset
        // 2. Loop and populate
        
        Reset();
        for (int i = 0; i < len; i++)
        {
            var val = Update(new TValue(sourceTimes[i], sourceValues[i]), true);
            vSpan[i] = val.Value;
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates VIDYA for the entire series.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");

        if (source.Length == 0) return;

        double alpha = 2.0 / (period + 1);
        
        // We need buffers for Up and Down sums
        // Since we can't allocate RingBuffers on the stack easily for dynamic period,
        // and we want to avoid heap allocations in the hot path if possible.
        // But for a static Calculate with a large span, a few allocations are acceptable.
        // Or we can use a circular buffer logic with a stackalloc array if period is small,
        // but period can be large.
        
        // Let's use a simple array for the circular buffer logic
        double[] ups = new double[period];
        double[] downs = new double[period];
        int head = 0;
        double sumUp = 0;
        double sumDown = 0;
        
        double prevClose = source[0];
        double lastVidya = source[0];
        
        // Initialize first element
        output[0] = source[0];
        
        // Fill buffers with 0 initially (already done by new double[])
        
        for (int i = 1; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price))
            {
                price = prevClose;
            }
            
            double change = price - prevClose;
            double up = change > 0 ? change : 0;
            double down = change < 0 ? -change : 0;
            
            // Update sums: remove old, add new
            sumUp -= ups[head];
            sumDown -= downs[head];
            
            ups[head] = up;
            downs[head] = down;
            
            sumUp += up;
            sumDown += down;
            
            head = (head + 1) % period;
            
            double sum = sumUp + sumDown;
            double vi = 0;
            if (sum > double.Epsilon)
            {
                vi = Math.Abs(sumUp - sumDown) / sum;
            }
            
            double dynamicAlpha = alpha * vi;
            double currentVidya = dynamicAlpha * price + (1.0 - dynamicAlpha) * lastVidya;
            
            output[i] = currentVidya;
            
            prevClose = price;
            lastVidya = currentVidya;
        }
    }

    public void Reset()
    {
        _ups.Clear();
        _downs.Clear();
        _prevClose = 0;
        _lastVidya = 0;
        _currentClose = 0;
        _currentVidya = 0;
        _isInitialized = false;
        _barCount = 0;
        
        Last = default;
    }
}
