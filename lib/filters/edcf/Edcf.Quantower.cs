using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class EdcfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Length", sortIndex: 1, 2, 100, 1, 0)]
    public int Length { get; set; } = 15;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Edcf _edcf = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"EDCF({Length}):{Source}";

    public EdcfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "EDCF - Ehlers Distance Coefficient Filter";
        Description = "Nonlinear adaptive filter with distance-based coefficients";
        _series = new LineSeries(name: $"EDCF {Length}", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _edcf = new Edcf(Length);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _edcf.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _series.SetValue(value, _edcf.IsHot, ShowColdValues);
    }
}
