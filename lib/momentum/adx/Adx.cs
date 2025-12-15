using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// ADX: Average Directional Index
/// </summary>
/// <remarks>
/// ADX measures the strength of a trend, regardless of its direction.
/// It is derived from the Smoothed Directional Movement Index (DX).
///
/// Calculation:
/// 1. Calculate True Range (TR), +DM, and -DM
/// 2. Smooth TR, +DM, -DM using RMA (Wilder's Moving Average)
///    - First value is SMA of first Period values
///    - Subsequent values: Previous + (Input - Previous) / Period
/// 3. Calculate +DI = (+DM_smooth / TR_smooth) * 100
/// 4. Calculate -DI = (-DM_smooth / TR_smooth) * 100
/// 5. Calculate DX = |(+DI - -DI) / (+DI + -DI)| * 100
/// 6. ADX = RMA(DX)
///    - First value is SMA of first Period DX values
///    - Subsequent values: Previous + (Input - Previous) / Period
///
/// Sources:
/// https://www.investopedia.com/terms/a/adx.asp
/// "New Concepts in Technical Trading Systems" by J. Welles Wilder
/// </remarks>
[SkipLocalsInit]
public sealed class Adx : ITValuePublisher
{
    private readonly int _period;
    private TBar _prevBar;
    private TBar _p_prevBar;
    private bool _isInitialized;

    // State for TR, +DM, -DM smoothing
    private double _trSum, _dmPlusSum, _dmMinusSum;
    private double _p_trSum, _p_dmPlusSum, _p_dmMinusSum;
    private int _samples;
    private int _p_samples;
    
    private double _trSmooth, _dmPlusSmooth, _dmMinusSmooth;
    private double _p_trSmooth, _p_dmPlusSmooth, _p_dmMinusSmooth;

    // State for ADX smoothing
    private double _dxSum;
    private double _p_dxSum;
    private int _dxSamples;
    private int _p_dxSamples;
    
    private double _adx;
    private double _p_adx;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Current ADX value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current +DI value.
    /// </summary>
    public TValue DiPlus { get; private set; }

    /// <summary>
    /// Current -DI value.
    /// </summary>
    public TValue DiMinus { get; private set; }

    /// <summary>
    /// True if the ADX has warmed up and is providing valid results.
    /// </summary>
    public bool IsHot => _dxSamples >= _period;

    /// <summary>
    /// Creates ADX with specified period.
    /// </summary>
    /// <param name="period">Period for ADX calculation (must be > 0)</param>
    public Adx(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        Name = $"Adx({period})";
        _isInitialized = false;
    }

    /// <summary>
    /// Resets the ADX state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _prevBar = default;
        _p_prevBar = default;
        _isInitialized = false;
        
        _trSum = _dmPlusSum = _dmMinusSum = 0;
        _p_trSum = _p_dmPlusSum = _p_dmMinusSum = 0;
        _samples = _p_samples = 0;
        
        _trSmooth = _dmPlusSmooth = _dmMinusSmooth = 0;
        _p_trSmooth = _p_dmPlusSmooth = _p_dmMinusSmooth = 0;
        
        _dxSum = _p_dxSum = 0;
        _dxSamples = _p_dxSamples = 0;
        
        _adx = _p_adx = 0;
        
        Last = default;
        DiPlus = default;
        DiMinus = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_prevBar = _prevBar;
            _p_trSum = _trSum;
            _p_dmPlusSum = _dmPlusSum;
            _p_dmMinusSum = _dmMinusSum;
            _p_samples = _samples;
            _p_trSmooth = _trSmooth;
            _p_dmPlusSmooth = _dmPlusSmooth;
            _p_dmMinusSmooth = _dmMinusSmooth;
            _p_dxSum = _dxSum;
            _p_dxSamples = _dxSamples;
            _p_adx = _adx;
        }
        else
        {
            _prevBar = _p_prevBar;
            _trSum = _p_trSum;
            _dmPlusSum = _p_dmPlusSum;
            _dmMinusSum = _p_dmMinusSum;
            _samples = _p_samples;
            _trSmooth = _p_trSmooth;
            _dmPlusSmooth = _p_dmPlusSmooth;
            _dmMinusSmooth = _p_dmMinusSmooth;
            _dxSum = _p_dxSum;
            _dxSamples = _p_dxSamples;
            _adx = _p_adx;
        }

        if (!_isInitialized)
        {
            if (isNew)
            {
                _prevBar = input;
                _isInitialized = true;
            }
            return new TValue(input.Time, 0);
        }

        // Calculate TR
        double hl = input.High - input.Low;
        double hpc = Math.Abs(input.High - _prevBar.Close);
        double lpc = Math.Abs(input.Low - _prevBar.Close);
        double tr = Math.Max(hl, Math.Max(hpc, lpc));

        // Calculate DM
        double dmPlus = 0;
        double dmMinus = 0;
        double upMove = input.High - _prevBar.High;
        double downMove = _prevBar.Low - input.Low;

        if (upMove > downMove && upMove > 0)
            dmPlus = upMove;
        
        if (downMove > upMove && downMove > 0)
            dmMinus = downMove;

        if (isNew)
        {
            _prevBar = input;
        }

        // Smooth TR, +DM, -DM
        if (_samples < _period)
        {
            _trSum += tr;
            _dmPlusSum += dmPlus;
            _dmMinusSum += dmMinus;
            _samples++;

            if (_samples == _period)
            {
                // Wilder's initialization for TR, +DM, and -DM uses the un-averaged sum (scaled sum).
                // Since +DI and -DI are ratios (+DM/TR and -DM/TR), the scaling factor (1/Period)
                // cancels out mathematically. This differs from the ADX smoothing later, which
                // explicitly uses a true SMA (sum / Period) for its initialization.
                _trSmooth = _trSum;
                _dmPlusSmooth = _dmPlusSum;
                _dmMinusSmooth = _dmMinusSum;
            }
        }
        else
        {
            // RMA: Previous + (Input - Previous) / Period
            // Or: Previous * (1 - 1/Period) + Input * (1/Period)
            // Or: (Previous * (Period - 1) + Input) / Period
            // Wilder uses sums, but effectively it's RMA.
            // Standard formula:
            // Smooth = Smooth - (Smooth / Period) + Input
            
            _trSmooth = _trSmooth - (_trSmooth / _period) + tr;
            _dmPlusSmooth = _dmPlusSmooth - (_dmPlusSmooth / _period) + dmPlus;
            _dmMinusSmooth = _dmMinusSmooth - (_dmMinusSmooth / _period) + dmMinus;
        }

        // Calculate DI and DX
        double diPlus = 0;
        double diMinus = 0;
        double dx = 0;

        if (_samples >= _period)
        {
            if (_trSmooth > 1e-10)
            {
                diPlus = (_dmPlusSmooth / _trSmooth) * 100.0;
                diMinus = (_dmMinusSmooth / _trSmooth) * 100.0;
            }

            double diSum = diPlus + diMinus;
            if (diSum > 1e-10)
            {
                dx = (Math.Abs(diPlus - diMinus) / diSum) * 100.0;
            }
            
            // Smooth DX to get ADX
            if (_dxSamples < _period)
            {
                _dxSum += dx;
                _dxSamples++;
                
                if (_dxSamples == _period)
                {
                    _adx = _dxSum / _period; // First ADX is SMA of DX
                }
            }
            else
            {
                // ADX = (Prior ADX * (Period - 1) + Current DX) / Period
                _adx = ((_adx * (_period - 1)) + dx) / _period;
            }
        }

        DiPlus = new TValue(input.Time, diPlus);
        DiMinus = new TValue(input.Time, diMinus);
        Last = new TValue(input.Time, _adx);
        
        Pub?.Invoke(Last);
        return Last;
    }

    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    public static TSeries Calculate(TBarSeries source, int period)
    {
        var adx = new Adx(period);
        return adx.Update(source);
    }
}
