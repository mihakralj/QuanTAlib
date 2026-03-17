using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RrsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Smooth Length", sortIndex: 1, 1, 500, 1, 0)]
    public int SmoothLength { get; set; } = 10;

    [InputParameter("RSI Length", sortIndex: 2, 1, 500, 1, 0)]
    public int RsiLength { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput(sortIndex: 3)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rrsi _rrsi = null!;
    private readonly LineSeries _rrsiLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RRSI ({SmoothLength},{RsiLength})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/rrsi/Rrsi.Quantower.cs";

    public RrsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "RRSI - Rocket RSI (Ehlers)";
        Description = "Fisher Transform of Super Smoother–filtered RSI for cyclic reversal signals";

        _rrsiLine = new LineSeries("RocketRSI", Color.DodgerBlue, 2, LineStyle.Solid);
        AddLineSeries(_rrsiLine);

        AddLineLevel(0, "Zero", Color.Gray, 1, LineStyle.Dash);
        AddLineLevel(2, "Overbought", Color.Red, 1, LineStyle.Dash);
        AddLineLevel(-2, "Oversold", Color.Green, 1, LineStyle.Dash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _rrsi = new Rrsi(SmoothLength, RsiLength);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _rrsi.Update(input, args.IsNewBar());

        if (!_rrsi.IsHot && !ShowColdValues)
        {
            return;
        }

        _rrsiLine.SetValue(result.Value);
    }
}
