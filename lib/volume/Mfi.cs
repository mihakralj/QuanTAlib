using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MFI: Money Flow Index
/// A volume-weighted momentum indicator that measures the inflow and outflow of money into an asset
/// over a specific period of time. It's sometimes referred to as volume-weighted RSI.
/// </summary>
/// <remarks>
/// The MFI calculation process:
/// 1. Calculate Typical Price:
///    TP = (High + Low + Close) / 3
/// 2. Calculate Raw Money Flow:
///    RMF = TP * Volume
/// 3. Determine Positive/Negative Money Flow:
///    If TP > Previous TP: Positive Money Flow
///    If TP < Previous TP: Negative Money Flow
/// 4. Calculate Money Flow Ratio:
///    MFR = (14-period Positive Money Flow Sum) / (14-period Negative Money Flow Sum)
/// 5. Calculate Money Flow Index:
///    MFI = 100 - (100 / (1 + MFR))
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Default period is 14 days
/// - Overbought level typically at 80
/// - Oversold level typically at 20
/// - Volume-weighted measure
///
/// Formula:
/// TP = (High + Low + Close) / 3
/// RMF = TP * Volume
/// MFR = ΣPositive Money Flow / ΣNegative Money Flow
/// MFI = 100 - (100 / (1 + MFR))
///
/// Market Applications:
/// - Overbought/Oversold conditions
/// - Divergence analysis
/// - Trend confirmation
/// - Price reversals
/// - Volume flow analysis
///
/// Sources:
///     Gene Quong and Avrum Soudack - Original development
///     https://www.investopedia.com/terms/m/mfi.asp
///
/// Note: Values above 80 indicate overbought conditions, while values below 20 indicate oversold conditions
/// </remarks>
[SkipLocalsInit]
public sealed class Mfi : AbstractBase
{
    private readonly CircularBuffer _posMf;
    private readonly CircularBuffer _negMf;
    private double _prevTp;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mfi(int period = 14)
    {
        WarmupPeriod = period + 1;  // Need one extra period for previous TP
        Name = $"MFI({period})";
        _posMf = new CircularBuffer(period);
        _negMf = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mfi(object source, int period = 14) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevTp = 0;
        _posMf.Clear();
        _negMf.Clear();
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

        // Calculate Typical Price
        double tp = (BarInput.High + BarInput.Low + BarInput.Close) / 3;

        // Skip first period to establish previous TP
        if (_index == 1)
        {
            _prevTp = tp;
            return 0;
        }

        // Calculate Raw Money Flow
        double rmf = tp * BarInput.Volume;

        // Determine Positive/Negative Money Flow
        if (tp > _prevTp)
        {
            _posMf.Add(rmf);
            _negMf.Add(0);
        }
        else if (tp < _prevTp)
        {
            _posMf.Add(0);
            _negMf.Add(rmf);
        }
        else
        {
            _posMf.Add(0);
            _negMf.Add(0);
        }

        // Store current TP for next calculation
        _prevTp = tp;

        // Calculate Money Flow Ratio and Index
        double posMfSum = _posMf.Sum();
        double negMfSum = _negMf.Sum();

        double mfi = Math.Abs(negMfSum) < double.Epsilon ? 100 : 100 - (100 / (1 + (posMfSum / negMfSum)));

        IsHot = _index >= WarmupPeriod;
        return mfi;
    }
}
