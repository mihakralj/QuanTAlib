using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AlmaIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 9;

    [InputParameter("Offset", sortIndex: 2, 0.0, 1.0, 0.01, 2)]
    public double Offset { get; set; } = 0.85;

    [InputParameter("Sigma", sortIndex: 3, 0.1, 100.0, 0.1, 1)]
    public double Sigma { get; set; } = 6.0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Alma _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ALMA {Period}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends_FIR/alma/Alma.Quantower.cs";

    public AlmaIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        _sourceName = Source.ToString();
        Name = "ALMA - Arnaud Legoux Moving Average";
        Description = "Arnaud Legoux Moving Average with Gaussian weighting";
        _series = new LineSeries(name: $"ALMA {Period}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ma = new Alma(Period, Offset, Sigma);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar && args.Reason != UpdateReason.NewTick)
        {
            return;
        }

        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), args.IsNewBar());

        _series.SetValue(result.Value, _ma.IsHot, ShowColdValues);
        _series.SetMarker(0, Color.Transparent);
    }
}
