using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AdlIndicator : Indicator, IWatchlistIndicator
{
    private Adl? _adl;
    protected LineSeries? AdlSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "ADL";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/adl/Adl.Quantower.cs";

    public AdlIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ADL - Accumulation/Distribution Line";
        Description = "Accumulation/Distribution Line";

        AdlSeries = new(name: "ADL", color: Color.Blue, width: 2, style: LineStyle.Solid);
        AddLineSeries(AdlSeries);
    }

    protected override void OnInit()
    {
        _adl = new Adl();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);
        TValue result = _adl!.Update(bar, isNew);

        AdlSeries!.SetValue(result.Value);
    }
}
