using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class BopIndicator : Indicator, IWatchlistIndicator
{
    private Bop? _bop;
    protected LineSeries? BopSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "BOP";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/bop/Bop.Quantower.cs";

    public BopIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "BOP - Balance of Power";
        Description = "Measures the strength of buyers vs sellers";

        BopSeries = new(name: "BOP", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(BopSeries);
    }

    protected override void OnInit()
    {
        _bop = new Bop();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);

        TValue result = _bop!.Update(bar, isNew);

        BopSeries!.SetValue(result.Value);
    }
}
