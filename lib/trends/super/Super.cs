using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// SuperTrend Indicator
/// A trend-following indicator that uses ATR to define upper and lower bands.
/// </summary>
[SkipLocalsInit]
public sealed class Super : ITValuePublisher
{
    private readonly double _multiplier;
    private readonly int _period;
    private TBar _prevBar;
    private TBar _lastInput;
    private int _sampleCount;

    private record struct State
    {
        public bool IsBullish;
        public double UpperBand;
        public double LowerBand;
        public bool IsInitialized;
        public double Atr;
        public double SumTr;
    }

    private State _state;
    private State _p_state;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name => $"Super({_period},{_multiplier})";

    public event Action<TValue>? Pub;

    /// <summary>
    /// Current SuperTrend value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current Upper Band value.
    /// </summary>
    public TValue UpperBand { get; private set; }

    /// <summary>
    /// Current Lower Band value.
    /// </summary>
    public TValue LowerBand { get; private set; }

    /// <summary>
    /// True if the current trend is bullish.
    /// </summary>
    public bool IsBullish => _state.IsBullish;

    /// <summary>
    /// True if the indicator has enough data to be valid.
    /// </summary>
    public bool IsHot => _sampleCount > _period;

    public Super(int period = 10, double multiplier = 3.0)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0.");
        }
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be greater than 0.");
        }
        _period = period;
        _multiplier = multiplier;
        _state = new State { IsBullish = true, IsInitialized = false };
        _sampleCount = 0;
    }

    public void Reset()
    {
        _state = new State { IsBullish = true, IsInitialized = false };
        _p_state = default;
        _prevBar = default;
        _lastInput = default;
        _sampleCount = 0;
        Last = default;
        UpperBand = default;
        LowerBand = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            if (_sampleCount > 0)
            {
                _prevBar = _lastInput;
            }
            _sampleCount++;
        }
        else
        {
            _state = _p_state;
        }
        _lastInput = input;

        // Calculate True Range
        double tr;
        if (_sampleCount <= 1)
        {
            tr = input.High - input.Low;
        }
        else
        {
            double h_l = input.High - input.Low;
            double h_pc = Math.Abs(input.High - _prevBar.Close);
            double l_pc = Math.Abs(input.Low - _prevBar.Close);
            tr = Math.Max(h_l, Math.Max(h_pc, l_pc));
        }

        // Update ATR
        // Note: Skender's implementation skips the first bar's TR for the initial SMA calculation.
        // We replicate this to match values.
        double atr;
        if (_sampleCount == 1)
        {
            atr = 0;
        }
        else if (_sampleCount <= _period + 1)
        {
            _state.SumTr += tr;
            if (_sampleCount == _period + 1)
            {
                _state.Atr = _state.SumTr / _period;
            }
            atr = _state.Atr;
        }
        else
        {
            _state.Atr = (_state.Atr * (_period - 1) + tr) / _period;
            atr = _state.Atr;
        }

        double superTrend = double.NaN;
        double upperBand = double.NaN;
        double lowerBand = double.NaN;

        if (_sampleCount > _period)
        {
            double mid = (input.High + input.Low) * 0.5;
            double upperEval = mid + (_multiplier * atr);
            double lowerEval = mid - (_multiplier * atr);

            if (!_state.IsInitialized)
            {
                _state.IsBullish = true; // Skender seems to default to Bullish (or determines it dynamically)
                _state.UpperBand = upperEval;
                _state.LowerBand = lowerEval;
                _state.IsInitialized = true;
            }

            double prevUpperBand = _state.UpperBand;
            double prevLowerBand = _state.LowerBand;
            double prevClose = _prevBar.Close;

            // New upper band
            if (upperEval < prevUpperBand || prevClose > prevUpperBand)
            {
                _state.UpperBand = upperEval;
            }

            // New lower band
            if (lowerEval > prevLowerBand || prevClose < prevLowerBand)
            {
                _state.LowerBand = lowerEval;
            }

            // SuperTrend
            if (input.Close <= (_state.IsBullish ? _state.LowerBand : _state.UpperBand))
            {
                superTrend = _state.UpperBand;
                _state.IsBullish = false;
            }
            else
            {
                superTrend = _state.LowerBand;
                _state.IsBullish = true;
            }

            upperBand = _state.UpperBand;
            lowerBand = _state.LowerBand;
        }

        Last = new TValue(input.Time, superTrend);
        UpperBand = new TValue(input.Time, upperBand);
        LowerBand = new TValue(input.Time, lowerBand);
        
        Pub?.Invoke(Last);
        return Last;
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

    public static TSeries Calculate(TBarSeries source, int period = 10, double multiplier = 3.0)
    {
        var indicator = new Super(period, multiplier);
        return indicator.Update(source);
    }
}
