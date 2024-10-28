using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CMF: Chaikin Money Flow
/// A volume-weighted technical indicator that measures the amount of Money Flow Volume (MFV)
/// over a specific period. Unlike ADL which is cumulative, CMF averages the Money Flow
/// Volume over a specified period.
/// </summary>
/// <remarks>
/// The CMF calculation process:
/// 1. Calculates Money Flow Multiplier (MFM):
///    MFM = ((Close - Low) - (High - Close)) / (High - Low)
/// 2. Calculates Money Flow Volume (MFV):
///    MFV = MFM × Volume
/// 3. CMF = Sum(MFV) / Sum(Volume) over N periods
///
/// Key characteristics:
/// - Oscillator between -1 and +1
/// - Volume-weighted measure
/// - Non-cumulative indicator
/// - Default period is 20 days
///
/// Formula:
/// MFM = ((Close - Low) - (High - Close)) / (High - Low)
/// MFV = MFM × Volume
/// CMF = Sum(MFV over N periods) / Sum(Volume over N periods)
///
/// Market Applications:
/// - Trend confirmation
/// - Volume analysis
/// - Price/volume divergence
/// - Support/resistance levels
/// - Market participation
///
/// Sources:
///     Marc Chaikin - Original development
///     https://www.investopedia.com/terms/c/chaikinmoneyflow.asp
///
/// Note: Values above zero indicate buying pressure, while values below zero indicate selling pressure
/// </remarks>

[SkipLocalsInit]
public sealed class Cmf : AbstractBase
{
    private readonly int _period;
    private readonly double[] _mfv;
    private readonly double[] _volume;
    private int _position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmf(int period = 20)
    {
        _period = period;
        WarmupPeriod = period;
        Name = $"CMF({_period})";
        _mfv = new double[period];
        _volume = new double[period];
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmf(object source, int period = 20) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _position = 0;
        Array.Clear(_mfv, 0, _mfv.Length);
        Array.Clear(_volume, 0, _volume.Length);
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
    private static double CalculateMoneyFlowMultiplier(double close, double high, double low)
    {
        double range = high - low;
        if (range > 0)
        {
            return ((close - low) - (high - close)) / range;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate Money Flow Multiplier
        double mfm = CalculateMoneyFlowMultiplier(BarInput.Close, BarInput.High, BarInput.Low);

        // Calculate Money Flow Volume
        double currentMfv = mfm * BarInput.Volume;

        // Update circular buffers
        _mfv[_position] = currentMfv;
        _volume[_position] = BarInput.Volume;
        _position = (_position + 1) % _period;

        // Calculate CMF
        double sumMfv = 0;
        double sumVolume = 0;
        for (int i = 0; i < _period; i++)
        {
            sumMfv += _mfv[i];
            sumVolume += _volume[i];
        }

        double cmf = Math.Abs(sumVolume) > double.Epsilon ? sumMfv / sumVolume : 0;
        IsHot = _index >= WarmupPeriod;
        return cmf;
    }
}
