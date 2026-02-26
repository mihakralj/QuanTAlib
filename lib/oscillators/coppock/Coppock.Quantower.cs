using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CoppockIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Long ROC Period", sortIndex: 1, 1, 500, 1, 0)]
    public int LongRoc { get; set; } = 14;

    [InputParameter("Short ROC Period", sortIndex: 2, 1, 500, 1, 0)]
    public int ShortRoc { get; set; } = 11;

    [InputParameter("WMA Period", sortIndex: 3, 1, 500, 1, 0)]
    public int WmaPeriod { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput(sortIndex: 4)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Coppock _coppock = null!;
    private readonly LineSeries _coppockSeries;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"COPPOCK ({LongRoc},{ShortRoc},{WmaPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/coppock/Coppock.Quantower.cs";

    public CoppockIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "COPPOCK - Coppock Curve";
        Description = "WMA of the sum of two Rate-of-Change values (long and short lookback periods)";

        _coppockSeries = new LineSeries(name: "Coppock", color: Color.Yellow, width: 2, style: LineStyle.Solid);

        AddLineSeries(_coppockSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _coppock = new Coppock(LongRoc, ShortRoc, WmaPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        _ = _coppock.Update(new TValue(item.TimeLeft, price), args.IsNewBar());

        _coppockSeries.SetValue(_coppock.Last.Value, _coppock.IsHot, ShowColdValues);
    }
}
