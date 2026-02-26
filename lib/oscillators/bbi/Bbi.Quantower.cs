using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BbiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period 1 (Ultra-Short)", sortIndex: 1, 1, 5000, 1, 0)]
    public int Period1 { get; set; } = 3;

    [InputParameter("Period 2 (Short)", sortIndex: 2, 1, 5000, 1, 0)]
    public int Period2 { get; set; } = 6;

    [InputParameter("Period 3 (Medium)", sortIndex: 3, 1, 5000, 1, 0)]
    public int Period3 { get; set; } = 12;

    [InputParameter("Period 4 (Long)", sortIndex: 4, 1, 5000, 1, 0)]
    public int Period4 { get; set; } = 24;

    [IndicatorExtensions.DataSourceInput(sortIndex: 5)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Bbi _bbi = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BBI ({Period1},{Period2},{Period3},{Period4})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/bbi/Bbi.Quantower.cs";

    public BbiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "BBI - Bulls Bears Index";
        Description = "Arithmetic mean of four SMAs across geometrically spaced periods";

        _series = new LineSeries("BBI", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _bbi = new Bbi(Period1, Period2, Period3, Period4);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _bbi.Update(input, args.IsNewBar());

        if (!_bbi.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
