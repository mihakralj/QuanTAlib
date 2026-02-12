using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SmiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("K Period", sortIndex: 1, 1, 500, 1, 0)]
    public int KPeriod { get; set; } = 10;

    [InputParameter("K Smooth", sortIndex: 2, 1, 100, 1, 0)]
    public int KSmooth { get; set; } = 3;

    [InputParameter("D Smooth", sortIndex: 3, 1, 100, 1, 0)]
    public int DSmooth { get; set; } = 3;

    [InputParameter("Use Blau method", sortIndex: 4)]
    public bool Blau { get; set; } = true;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Smi _smi = null!;
    private readonly LineSeries _kSeries;
    private readonly LineSeries _dSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SMI {KPeriod},{KSmooth},{DSmooth}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/smi/Smi.Quantower.cs";

    public SmiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SMI";
        Description = "Stochastic Momentum Index with K and D lines";

        _kSeries = new LineSeries(name: "K", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _dSeries = new LineSeries(name: "D", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(_kSeries);
        AddLineSeries(_dSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _smi = new Smi(KPeriod, KSmooth, DSmooth, Blau);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _smi.Update(this.GetInputBar(args), args.IsNewBar());

        _kSeries.SetValue(_smi.K.Value, _smi.IsHot, ShowColdValues);
        _dSeries.SetValue(_smi.D.Value, _smi.IsHot, ShowColdValues);
    }
}
