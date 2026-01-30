using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class TviIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Minimum tick size", sortIndex: 10, minimum: 0.0, maximum: 100.0, increment: 0.001, decimalPlaces: 4)]
    public double MinTick { get; set; } = 0.125;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Tvi _tvi = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 2;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 2;

    public override string ShortName => "TVI";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/tvi/Tvi.Quantower.cs";

    public TviIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "TVI - Trade Volume Index";
        Description = "Trade Volume Index accumulates volume with a directional bias, where direction is determined by price changes exceeding a minimum tick threshold";

        _series = new LineSeries(name: "TVI", color: Color.DarkCyan, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _tvi = new Tvi(MinTick);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _tvi.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _tvi.IsHot, ShowColdValues);
    }
}