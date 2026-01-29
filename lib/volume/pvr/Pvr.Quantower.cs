using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PvrIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pvr _pvr = null!;
    private readonly LineSeries _pvrSeries;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 1;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 1;

    public override string ShortName => "PVR";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/pvr/Pvr.Quantower.cs";

    public PvrIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PVR - Price Volume Rank";
        Description = "Price Volume Rank categorizes price-volume relationships into discrete states (0-4)";

        _pvrSeries = new LineSeries(name: "PVR", color: Color.Yellow, width: 2, style: LineStyle.Histogramm);
        AddLineSeries(_pvrSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _pvr = new Pvr();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _pvr.Update(bar, args.IsNewBar());

        _pvrSeries.SetValue(result.Value, _pvr.IsHot, ShowColdValues);
    }
}