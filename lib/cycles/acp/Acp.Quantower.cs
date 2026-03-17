using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class AcpIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Min Period", sortIndex: 1, 3, 100, 1, 0)]
    public int MinPeriod { get; set; } = 8;

    [InputParameter("Max Period", sortIndex: 2, 4, 500, 1, 0)]
    public int MaxPeriod { get; set; } = 48;

    [InputParameter("Avg Length", sortIndex: 3, 0, 100, 1, 0)]
    public int AvgLength { get; set; } = 3;

    [InputParameter("Enhance", sortIndex: 4)]
    public bool Enhance { get; set; } = true;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Acp _acp = null!;
    private readonly LineSeries _cycleSeries;
    private readonly LineSeries _powerSeries;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"ACP ({MinPeriod},{MaxPeriod})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/acp/Acp.Quantower.cs";

    public AcpIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "ACP - Ehlers Autocorrelation Periodogram";
        Description = "Ehlers' Autocorrelation Periodogram estimates the dominant cycle period using autocorrelation and spectral analysis";

        _cycleSeries = new LineSeries(name: "Cycle", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        _powerSeries = new LineSeries(name: "Power", color: Color.Orange, width: 1, style: LineStyle.Dot);
        AddLineSeries(_cycleSeries);
        AddLineSeries(_powerSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _acp = new Acp(MinPeriod, MaxPeriod, AvgLength, Enhance);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
        {
            return;
        }

        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        TValue result = _acp.Update(input, args.IsNewBar());

        _cycleSeries.SetValue(result.Value, _acp.IsHot, ShowColdValues);
        _powerSeries.SetValue(_acp.NormalizedPower * MaxPeriod, _acp.IsHot, ShowColdValues);
    }
}