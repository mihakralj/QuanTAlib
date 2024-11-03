using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// TSI: True Strength Index
/// A momentum indicator that shows both trend direction and overbought/oversold conditions
/// by using two smoothing steps on price changes.
/// </summary>
/// <remarks>
/// The TSI calculation process:
/// 1. Calculate price change (PC):
///    PC = Close - Previous Close
/// 2. Calculate absolute price change (APC):
///    APC = |PC|
/// 3. Double smooth both PC and APC using EMA:
///    First PC EMA = EMA(PC, firstPeriod)
///    Second PC EMA = EMA(First PC EMA, secondPeriod)
///    First APC EMA = EMA(APC, firstPeriod)
///    Second APC EMA = EMA(First APC EMA, secondPeriod)
/// 4. Calculate TSI:
///    TSI = (Second PC EMA / Second APC EMA) * 100
///
/// Key characteristics:
/// - Double smoothed momentum indicator
/// - Oscillates between +100 and -100
/// - Default periods are 25 and 13
/// - Shows trend direction
/// - Identifies overbought/oversold
///
/// Formula:
/// TSI = (EMA(EMA(PC, r), s) / EMA(EMA(|PC|, r), s)) * 100
/// where:
/// PC = Close - Previous Close
/// r = first period (default 25)
/// s = second period (default 13)
///
/// Market Applications:
/// - Trend direction
/// - Overbought/Oversold levels
/// - Centerline crossovers
/// - Divergence analysis
/// - Signal line crossovers
///
/// Sources:
///     William Blau - Original development (1991)
///     https://www.investopedia.com/terms/t/tsi.asp
///
/// Note: Values above +25 indicate overbought conditions, while values below -25 indicate oversold conditions
/// </remarks>
[SkipLocalsInit]
public sealed class Tsi : AbstractBase
{
    private readonly int _firstPeriod;
    private double _prevClose;
    private double _pcFirstEma;
    private double _pcSecondEma;
    private double _apcFirstEma;
    private double _apcSecondEma;
    private readonly double _firstAlpha;
    private readonly double _secondAlpha;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tsi(int firstPeriod = 25, int secondPeriod = 13)
    {
        _firstPeriod = firstPeriod;
        WarmupPeriod = firstPeriod + secondPeriod;
        Name = $"TSI({_firstPeriod},{secondPeriod})";
        _firstAlpha = 2.0 / (firstPeriod + 1);
        _secondAlpha = 2.0 / (secondPeriod + 1);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tsi(object source, int firstPeriod = 25, int secondPeriod = 13) : this(firstPeriod, secondPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _pcFirstEma = 0;
        _pcSecondEma = 0;
        _apcFirstEma = 0;
        _apcSecondEma = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Skip first period to establish previous close
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate price changes
        double pc = BarInput.Close - _prevClose;
        double apc = Math.Abs(pc);

        // Initialize or update EMAs
        if (_index <= _firstPeriod)
        {
            _pcFirstEma = pc;
            _apcFirstEma = apc;
        }
        else
        {
            _pcFirstEma = (_firstAlpha * pc) + ((1 - _firstAlpha) * _pcFirstEma);
            _apcFirstEma = (_firstAlpha * apc) + ((1 - _firstAlpha) * _apcFirstEma);
        }

        if (_index <= WarmupPeriod)
        {
            _pcSecondEma = _pcFirstEma;
            _apcSecondEma = _apcFirstEma;
        }
        else
        {
            _pcSecondEma = (_secondAlpha * _pcFirstEma) + ((1 - _secondAlpha) * _pcSecondEma);
            _apcSecondEma = (_secondAlpha * _apcFirstEma) + ((1 - _secondAlpha) * _apcSecondEma);
        }

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        // Calculate TSI
        double tsi = Math.Abs(_apcSecondEma) > double.Epsilon ? (_pcSecondEma / _apcSecondEma) * 100 : 0;

        IsHot = _index >= WarmupPeriod;
        return tsi;
    }
}
