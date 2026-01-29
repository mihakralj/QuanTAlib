using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ObvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Obv _obv = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 2;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 2;

    public override string ShortName => "OBV";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/obv/Obv.Quantower.cs";

    public ObvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "OBV - On Balance Volume";
        Description = "On Balance Volume tracks cumulative buying/selling pressure by adding volume on up days and subtracting on down days";

        _series = new LineSeries(name: "OBV", color: Color.DarkGreen, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _obv = new Obv();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _obv.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _obv.IsHot, ShowColdValues);
    }
}