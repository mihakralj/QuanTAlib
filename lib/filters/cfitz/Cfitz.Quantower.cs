using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class CfitzIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Min Period (pLow)", sortIndex: 1, 2, 100, 1, 0)]
    public int PLow { get; set; } = 6;

    [InputParameter("Max Period (pHigh)", sortIndex: 2, 3, 500, 1, 0)]
    public int PHigh { get; set; } = 32;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Cfitz _cf = null!;
    private readonly LineSeries _cfSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CF {PLow}:{PHigh}:{_sourceName}";

    public CfitzIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "CFITZ - Christiano-Fitzgerald Filter";
        Description = "Asymmetric full-sample band-pass filter optimal under random-walk assumption";
        _cfSeries = new LineSeries(name: $"CF {PLow}:{PHigh}", color: Color.Teal, width: 2, style: LineStyle.Solid);
        AddLineSeries(_cfSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _cf = new Cfitz(PLow, PHigh);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _cf.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _cfSeries.SetValue(value, _cf.IsHot, ShowColdValues);
    }
}
