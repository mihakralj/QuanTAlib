using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AfirmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Taps (number of weights)", sortIndex: 1, 1, 2000, 1, 0)]
    public int Taps { get; set; } = 6;

    [InputParameter("Period for lowpass cutoff", sortIndex: 2, 1, 2000, 1, 0)]
    public int Period { get; set; } = 6;

    [InputParameter("Window Type", sortIndex: 3, variants: [
        "Rectangular", Afirma.WindowType.Rectangular,
            "Hanning", Afirma.WindowType.Hanning1,
            "Hamming", Afirma.WindowType.Hanning2,
            "Blackman", Afirma.WindowType.Blackman,
            "Blackman-Harris", Afirma.WindowType.BlackmanHarris
    ])]
    public Afirma.WindowType Window { get; set; } = Afirma.WindowType.Hanning1;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;
    private Afirma? ma;
    protected LineSeries? Series;
    protected string? SourceName;
    public int MinHistoryDepths => Period + Taps;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public AfirmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "AFIRMA - Adaptive Finite Impulse Response Moving Average";
        Description = "Adaptive Finite Impulse Response Moving Average with ARMA component";

        Series = new(name: $"AFIRMA {Taps}:{Period}:{Window}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        ma = new Afirma(periods: Period, taps: Taps, window: Window);
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);

        Series!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
        Series!.SetValue(result.Value);
    }

    public override string ShortName => $"AFIRMA {Taps}:{Period}:{Window}:{SourceName}";

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, ma!.WarmupPeriod, showColdValues: ShowColdValues, tension: 0.2);
    }
}
