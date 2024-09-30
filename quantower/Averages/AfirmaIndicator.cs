using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class AfirmaIndicator : IndicatorBase
{
    [InputParameter("Taps (number of weights)", sortIndex: 1, 1, 2000, 1, 0)]
    public int Taps { get; set; } = 6;

    [InputParameter("Periods for lowpass cutoff", sortIndex: 2, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 6;



    [InputParameter("Window Type", sortIndex: 3, variants: [
        "Rectangular", Afirma.WindowType.Rectangular,
        "Hanning", Afirma.WindowType.Hanning1,
        "Hamming", Afirma.WindowType.Hanning2,
        "Blackman", Afirma.WindowType.Blackman,
        "Blackman-Harris", Afirma.WindowType.BlackmanHarris
    ])]
    public Afirma.WindowType Window { get; set; } = Afirma.WindowType.Hanning1;

    private Afirma? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"AFIRMA {Taps}:{Periods}:{Window} : {SourceName}";

    public AfirmaIndicator()
    {
        Name = "AFIRMA - Adaptive Finite Impulse Response Moving Average";
        Description = "Adaptive Finite Impulse Response Moving Average with ARMA component";
    }

    protected override void InitIndicator()
    {
        ma = new Afirma(periods: Periods, taps: Taps, window: Window);
    }
}