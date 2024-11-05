using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// EFI: Elder Ray's Force Index
/// A volume-based oscillator that measures the strength of price movements using volume.
/// It helps identify potential trend reversals and confirm price movements.
/// </summary>
/// <remarks>
/// The EFI calculation process:
/// 1. Calculate the difference between the current close and the previous close
/// 2. Multiply the difference by the current volume
/// 3. Apply an exponential moving average (EMA) to smooth the result
///
/// Key characteristics:
/// - Oscillates above and below zero
/// - Positive values indicate buying pressure
/// - Negative values indicate selling pressure
/// - Crosses above zero suggest buying opportunities
/// - Crosses below zero suggest selling opportunities
///
/// Formula:
/// EFI = EMA((Close - Close[1]) * Volume, period)
///
/// Sources:
///     Alexander Elder - "Trading for a Living" (1993)
///     https://www.investopedia.com/terms/f/force-index.asp
///
/// Note: Default period is 13
/// </remarks>
[SkipLocalsInit]
public sealed class Efi : AbstractBase
{
    private readonly Ema _ema;
    private double _prevClose;
    private double _p_prevClose;
    private const int DefaultPeriod = 13;

    /// <param name="period">The smoothing period for EMA calculation (default 13).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Efi(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _ema = new(period);
        WarmupPeriod = period + 1;
        Name = $"EFI({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The smoothing period for EMA calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Efi(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _ema.Init();
        _prevClose = double.NaN;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevClose = _prevClose;
        }
        else
        {
            _prevClose = _p_prevClose;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate raw force index
        double priceChange = BarInput.Close - _prevClose;
        double forceIndex = priceChange * BarInput.Volume;

        // Update previous close
        _prevClose = BarInput.Close;

        // Apply EMA smoothing
        return _ema.Calc(forceIndex, BarInput.IsNew);
    }
}
