using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// PMO (Price Momentum Oscillator) Quantower indicator.
/// Double-smoothed rate of change measuring momentum with reduced noise.
/// Formula: ROC% → EMA → EMA
/// </summary>
[SkipLocalsInit]
public sealed class PmoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("ROC Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int RocPeriod { get; set; } = 35;

    [InputParameter("Smooth1 Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int Smooth1Period { get; set; } = 20;

    [InputParameter("Smooth2 Period", sortIndex: 3, 1, 2000, 1, 0)]
    public int Smooth2Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pmo _pmo = null!;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PMO({RocPeriod},{Smooth1Period},{Smooth2Period}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/pmo/Pmo.Quantower.cs";

    public PmoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "PMO - Price Momentum Oscillator";
        Description = "Double-smoothed rate of change for momentum analysis";

        AddLineSeries(new LineSeries(name: "PMO", color: Color.Blue, width: 2, style: LineStyle.Solid));
        AddLineSeries(new LineSeries(name: "Zero", color: Color.Gray, width: 1, style: LineStyle.Dot));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _pmo = new Pmo(RocPeriod, Smooth1Period, Smooth2Period);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _pmo.Update(new TValue(this.GetInputBar(args).Time, _priceSelector(HistoricalData[Count - 1, SeekOriginHistory.Begin])), args.IsNewBar());

        LinesSeries[0].SetValue(result.Value, _pmo.IsHot, ShowColdValues);
        LinesSeries[1].SetValue(0);
    }
}
