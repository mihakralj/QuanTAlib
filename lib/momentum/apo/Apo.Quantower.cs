using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ApoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, 1, 1000, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Apo? _apo;
    protected LineSeries? Series;

    public int MinHistoryDepths => SlowPeriod;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"APO {FastPeriod}:{SlowPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/apo/Apo.Quantower.cs";

    public ApoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "APO - Absolute Price Oscillator";
        Description = "Momentum indicator showing the difference between two EMAs";

        Series = new(name: "APO", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        _apo = new Apo(FastPeriod, SlowPeriod);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);
        TValue result = _apo!.Update(bar, isNew);

        if (!_apo.IsHot && !ShowColdValues)
        {
            return;
        }

        Series!.SetValue(result.Value);
    }
}
