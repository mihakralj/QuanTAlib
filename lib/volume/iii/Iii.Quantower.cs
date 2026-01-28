using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class IiiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 21;

    [InputParameter("Cumulative Mode", sortIndex: 11)]
    public bool Cumulative { get; set; }

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Iii _iii = null!;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => Period;

    public override string ShortName => $"III({Period}{(Cumulative ? ",Cum" : "")})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volume/iii/Iii.Quantower.cs";

    public IiiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "III - Intraday Intensity Index";
        Description = "Intraday Intensity Index measures buying/selling pressure using the position of the close within the day's range, weighted by volume";

        _series = new LineSeries(name: "III", color: Color.Cyan, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _iii = new Iii(Period, Cumulative);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _iii.Update(bar, args.IsNewBar());

        _series.SetValue(result.Value, _iii.IsHot, ShowColdValues);
    }
}