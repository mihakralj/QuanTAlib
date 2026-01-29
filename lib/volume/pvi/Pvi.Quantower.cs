using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PviIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Start Value", sortIndex: 10, 1, 10000, 1, 0)]
    public double StartValue { get; set; } = 100;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pvi _pvi = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 2;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 2;

    public override string ShortName => $"PVI({StartValue})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/pvi/Pvi.Quantower.cs";

    public PviIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PVI - Positive Volume Index";
        Description = "Positive Volume Index tracks price changes on days when volume increases, reflecting retail trader activity";

        _series = new LineSeries(name: "PVI", color: Color.DarkOrange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _pvi = new Pvi(StartValue);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _pvi.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _pvi.IsHot, ShowColdValues);
    }
}