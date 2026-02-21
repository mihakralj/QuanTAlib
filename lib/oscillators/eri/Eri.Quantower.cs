using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EriIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 13;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Eri _eri = null!;
    private readonly LineSeries _bullSeries;
    private readonly LineSeries _bearSeries;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => Period;

    public override string ShortName => $"ERI({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/eri/Eri.Quantower.cs";

    public EriIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ERI - Elder Ray Index";
        Description = "Elder Ray Index measures buying and selling pressure as Bull Power (High − EMA) and Bear Power (Low − EMA)";

        _bullSeries = new LineSeries(name: "Bull Power", color: Color.Green, width: 2, style: LineStyle.Solid);
        _bearSeries = new LineSeries(name: "Bear Power", color: Color.Red, width: 2, style: LineStyle.Solid);
        AddLineSeries(_bullSeries);
        AddLineSeries(_bearSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _eri = new Eri(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();

        TBar bar = this.GetInputBar(args);

        TValue result = _eri.Update(bar, isNew);

        _bullSeries.SetValue(result.Value, _eri.IsHot, ShowColdValues);
        _bearSeries.SetValue(_eri.BearPower, _eri.IsHot, ShowColdValues);
    }
}
