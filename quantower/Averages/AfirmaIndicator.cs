using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AfirmaIndicator : Indicator, IWatchlistIndicator
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

    [InputParameter("Data source", sortIndex: 4, variants: [
        "Open", SourceType.Open,
            "High", SourceType.High,
            "Low", SourceType.Low,
            "Close", SourceType.Close,
            "HL/2 (Median)", SourceType.HL2,
            "OC/2 (Midpoint)", SourceType.OC2,
            "OHL/3 (Mean)", SourceType.OHL3,
            "HLC/3 (Typical)", SourceType.HLC3,
            "OHLC/4 (Average)", SourceType.OHLC4,
            "HLCC/4 (Weighted)", SourceType.HLCC4
    ])]
    public SourceType Source { get; set; } = SourceType.Close;

    private Afirma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Periods + Taps;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public AfirmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "AFIRMA - Adaptive Finite Impulse Response Moving Average";
        Description = "Adaptive Finite Impulse Response Moving Average with ARMA component";
        Series = new(name: $"AFIRMA {Taps}:{Periods}:{Window}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Afirma(periods: Periods, taps: Taps, window: Window);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"AFIRMA {Taps}:{Periods}:{Window}:{SourceName}";
}

