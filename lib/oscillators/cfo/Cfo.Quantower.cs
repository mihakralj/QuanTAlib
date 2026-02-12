using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CfoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cfo _cfo = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CFO ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/cfo/Cfo.Quantower.cs";

    public CfoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CFO - Chande Forecast Oscillator";
        Description = "Percentage difference between price and linear regression forecast";

        _series = new LineSeries("CFO", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _cfo = new Cfo(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _cfo.Update(input, args.IsNewBar());

        if (!_cfo.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
