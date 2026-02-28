using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Fisher04Indicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 500, 1, 0)]
    public int Period { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput(sortIndex: 2)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Fisher04 _fisher = null!;
    private readonly LineSeries _fisherLine;
    private readonly LineSeries _signalLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"Fisher04 ({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/fisher04/Fisher04.Quantower.cs";

    public Fisher04Indicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "FISHER04 - Ehlers Fisher Transform (2004)";
        Description = "Cybernetic Analysis Fisher Transform with gentler arctanh scaling for reversal detection";

        _fisherLine = new LineSeries("Fisher04", Color.Yellow, 2, LineStyle.Solid);
        _signalLine = new LineSeries("Signal", Color.Orange, 1, LineStyle.Solid);
        AddLineSeries(_fisherLine);
        AddLineSeries(_signalLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _fisher = new Fisher04(Period);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _fisher.Update(input, args.IsNewBar());

        if (!_fisher.IsHot && !ShowColdValues)
        {
            return;
        }

        _fisherLine.SetValue(result.Value);
        _signalLine.SetValue(_fisher.Signal);
    }
}
