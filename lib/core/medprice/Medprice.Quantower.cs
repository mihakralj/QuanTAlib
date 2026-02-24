using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MedpriceIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Medprice _medprice = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "MEDPRICE";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/core/medprice/Medprice.Quantower.cs";

    public MedpriceIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "MEDPRICE - Median Price";
        Description = "Midpoint of High and Low prices: (H+L)/2.";

        _series = new LineSeries(name: "MEDPRICE", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _medprice = new Medprice();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _medprice.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _medprice.IsHot, ShowColdValues);
    }
}
