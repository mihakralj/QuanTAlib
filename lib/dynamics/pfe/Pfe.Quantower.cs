using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class PfeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 2, 200, 1, 0)]
    public int Period { get; set; } = 10;

    [InputParameter("Smooth Period", sortIndex: 2, 1, 100, 1, 0)]
    public int SmoothPeriod { get; set; } = 5;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Pfe _pfe = null!;
    private readonly LineSeries _pfeSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PFE {Period},{SmoothPeriod}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/dynamics/pfe/Pfe.Quantower.cs";

    public PfeIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "PFE - Polarized Fractal Efficiency";
        Description = "Measures trend efficiency as straight-line / fractal-path distance, EMA-smoothed";

        _pfeSeries = new LineSeries(name: "PFE", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_pfeSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _pfe = new Pfe(Period, SmoothPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _pfe.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _pfeSeries.SetValue(value, _pfe.IsHot, ShowColdValues);
    }
}
