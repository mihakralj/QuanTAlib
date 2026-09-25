using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SrsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("EMA Length", sortIndex: 1, 1, 1000, 1, 0)]
    public int EmaLength { get; set; } = 6;

    [InputParameter("RSI Length", sortIndex: 2, 1, 1000, 1, 0)]
    public int RsiLength { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Srsi _ma = null!;
    private readonly LineSeries _series;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SRSI {EmaLength},{RsiLength}:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/srsi/Srsi.Quantower.cs";

    public SrsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "SRSI - Apirine Slow RSI";
        Description = "RSI measuring price changes relative to EMA movements";
        _series = new LineSeries(name: $"SRSI {EmaLength},{RsiLength}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _ma = new Srsi(EmaLength, RsiLength);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        TValue result = _ma.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew: args.IsNewBar());
        _series.SetValue(result.Value, _ma.IsHot, ShowColdValues);
    }
}
