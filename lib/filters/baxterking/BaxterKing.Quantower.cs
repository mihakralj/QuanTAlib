using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class BaxterKingIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Low Period", sortIndex: 1, 2, 500, 1, 0)]
    public int PLow { get; set; } = 6;

    [InputParameter("High Period", sortIndex: 2, 3, 500, 1, 0)]
    public int PHigh { get; set; } = 32;

    [InputParameter("Half-Length K", sortIndex: 3, 1, 100, 1, 0)]
    public int K { get; set; } = 12;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private BaxterKing _bk = null!;
    private readonly LineSeries _bkSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"BK {PLow}:{PHigh}:{K}:{_sourceName}";

    public BaxterKingIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "BK - Baxter-King Band-Pass Filter";
        Description = "Baxter-King symmetric FIR band-pass filter extracting cyclical components between pLow and pHigh bars";
        _bkSeries = new LineSeries(name: $"BK {PLow}:{PHigh}:{K}", color: Color.DodgerBlue, width: 2, style: LineStyle.Solid);
        AddLineSeries(_bkSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _bk = new BaxterKing(PLow, PHigh, K);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _bk.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _bkSeries.SetValue(value, _bk.IsHot, ShowColdValues);
    }
}
