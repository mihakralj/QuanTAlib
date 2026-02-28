using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MidbodyIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Midbody _midbody = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 1;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "MIDBODY";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/core/midbody/Midbody.Quantower.cs";

    public MidbodyIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "MIDBODY - Open-Close Average";
        Description = "Midpoint of Open and Close prices: (O+C)/2.";

        _series = new LineSeries(name: "MIDBODY", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _midbody = new Midbody();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _midbody.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _midbody.IsHot, ShowColdValues);
    }
}










