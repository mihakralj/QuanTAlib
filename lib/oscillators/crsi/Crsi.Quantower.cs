using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CrsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("RSI Period", sortIndex: 1, 1, 500, 1, 0)]
    public int RsiPeriod { get; set; } = 3;

    [InputParameter("Streak RSI Period", sortIndex: 2, 1, 500, 1, 0)]
    public int StreakPeriod { get; set; } = 2;

    [InputParameter("Percent Rank Period", sortIndex: 3, 1, 1000, 1, 0)]
    public int RankPeriod { get; set; } = 100;

    [IndicatorExtensions.DataSourceInput(sortIndex: 4)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Crsi _crsi = null!;
    private readonly LineSeries _series;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CRSI ({RsiPeriod},{StreakPeriod},{RankPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/crsi/Crsi.Quantower.cs";

    public CrsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CRSI - Connors RSI";
        Description = "Composite momentum oscillator combining price RSI, streak RSI, and percent rank of ROC";

        _series = new LineSeries("CRSI", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _crsi = new Crsi(RsiPeriod, StreakPeriod, RankPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _crsi.Update(input, args.IsNewBar());

        if (!_crsi.IsHot && !ShowColdValues)
        {
            return;
        }

        _series.SetValue(result.Value);
    }
}
