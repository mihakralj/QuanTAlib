using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AdoscIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int FastPeriod { get; set; } = 3;

    [InputParameter("Slow Period", sortIndex: 2, 1, 1000, 1, 0)]
    public int SlowPeriod { get; set; } = 10;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Adosc? _adosc;
    protected LineSeries? Series;

    public int MinHistoryDepths => SlowPeriod;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ADOSC {FastPeriod}:{SlowPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/adosc/Adosc.Quantower.cs";

    public AdoscIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADOSC - Accumulation/Distribution Oscillator";
        Description = "Momentum indicator for the Accumulation/Distribution Line";

        Series = new(name: "ADOSC", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _adosc = new Adosc(FastPeriod, SlowPeriod);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);
        TValue result = _adosc!.Update(bar, isNew);

        if (!_adosc.IsHot && !ShowColdValues)
        {
            return;
        }

        Series!.SetValue(result.Value);
    }
}
