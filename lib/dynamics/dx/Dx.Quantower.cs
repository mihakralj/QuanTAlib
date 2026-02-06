using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class DxIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Dx _dx = null!;
    private readonly LineSeries _dxSeries;
    private readonly LineSeries _diPlusSeries;
    private readonly LineSeries _diMinusSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"DX {Period}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/dx/Dx.Quantower.cs";

    public DxIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "DX - Directional Movement Index";
        Description = "Measures the strength of directional movement (unsmoothed)";

        _dxSeries = new LineSeries(name: "DX", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        _diPlusSeries = new LineSeries(name: "+DI", color: Color.Green, width: 1, style: LineStyle.Solid);
        _diMinusSeries = new LineSeries(name: "-DI", color: Color.Red, width: 1, style: LineStyle.Solid);

        AddLineSeries(_dxSeries);
        AddLineSeries(_diPlusSeries);
        AddLineSeries(_diMinusSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _dx = new Dx(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _dx.Update(this.GetInputBar(args), args.IsNewBar());

        _dxSeries.SetValue(result.Value, _dx.IsHot, ShowColdValues);
        _diPlusSeries.SetValue(_dx.DiPlus.Value, _dx.IsHot, ShowColdValues);
        _diMinusSeries.SetValue(_dx.DiMinus.Value, _dx.IsHot, ShowColdValues);
    }
}
