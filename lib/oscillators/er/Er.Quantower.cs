using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class ErIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 500, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Er _er = null!;
    private readonly LineSeries _erLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ER ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/er/Er.Quantower.cs";

    public ErIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ER - Kaufman Efficiency Ratio";
        Description = "Measures signal-to-noise ratio: 1 = trending, 0 = choppy";

        _erLine = new LineSeries("ER", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_erLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _er = new Er(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _er.Update(input, args.IsNewBar());

        if (!_er.IsHot && !ShowColdValues)
        {
            return;
        }

        _erLine.SetValue(result.Value);
    }
}
