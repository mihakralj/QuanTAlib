using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class NviIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Start Value", sortIndex: 10, 1, 10000, 1, 0)]
    public double StartValue { get; set; } = 100;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Nvi _nvi = null!;
    private readonly LineSeries _series;

#pragma warning disable S2325 // Instance property required by Quantower indicator interface
    public int MinHistoryDepths => 2;
#pragma warning restore S2325
    int IWatchlistIndicator.MinHistoryDepths => 2;

    public override string ShortName => $"NVI({StartValue})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/nvi/Nvi.Quantower.cs";

    public NviIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "NVI - Negative Volume Index";
        Description = "Negative Volume Index tracks price changes on days when volume decreases, reflecting smart money activity";

        _series = new LineSeries(name: "NVI", color: Color.DarkCyan, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _nvi = new Nvi(StartValue);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _nvi.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _nvi.IsHot, ShowColdValues);
    }
}