using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EomIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Volume Scale", sortIndex: 11, 1, 1000000, 1, 0)]
    public double VolumeScale { get; set; } = 10000;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Eom _eom = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period + 1;
    int IWatchlistIndicator.MinHistoryDepths => Period + 1;

    public override string ShortName => $"EOM({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/eom/Eom.Quantower.cs";

    public EomIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "EOM - Ease of Movement";
        Description = "Ease of Movement measures the relationship between price change and volume, indicating how easily price moves";

        _series = new LineSeries(name: "EOM", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _eom = new Eom(Period, VolumeScale);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _eom.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _eom.IsHot, ShowColdValues);
    }
}