using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CviIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("ROC Length", sortIndex: 1, 1, 1000, 1, 0)]
    public int RocLength { get; set; } = 10;

    [InputParameter("Smooth Length", sortIndex: 2, 1, 1000, 1, 0)]
    public int SmoothLength { get; set; } = 10;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cvi _cvi = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CVI {RocLength},{SmoothLength}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/volatility/cvi/Cvi.Quantower.cs";

    public CviIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CVI - Chaikin's Volatility";
        Description = "Chaikin's Volatility measures the rate of change of the EMA-smoothed high-low range, identifying periods of expanding or contracting volatility";

        _series = new LineSeries(name: "CVI", color: IndicatorExtensions.Volatility, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _cvi = new Cvi(RocLength, SmoothLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TBar bar = this.GetInputBar(args);
        TValue result = _cvi.Update(bar, isNew: args.IsNewBar());
        _series.SetValue(result.Value, _cvi.IsHot, ShowColdValues);
    }
}