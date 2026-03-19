using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public class TbfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, minimum: 2, maximum: 1000, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Bandwidth", sortIndex: 2, minimum: 0.001, maximum: 1.0, increment: 0.01, decimalPlaces: 3)]
    public double Bandwidth { get; set; } = 0.1;

    [InputParameter("Length", sortIndex: 3, minimum: 1, maximum: 200, increment: 1, decimalPlaces: 0)]
    public int Length { get; set; } = 10;

    [IndicatorExtensions.DataSourceInput(sortIndex: 4)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Tbf? tbf;
    protected LineSeries? TbfSeries;
    protected LineSeries? BpSeries;
    protected LineSeries? ZeroSeries;

    public int MinHistoryDepths => Length + 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"TBF ({Period},{Bandwidth:F2},{Length})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/filters/tbf/Tbf.cs";

    public TbfIndicator()
    {
        Name = "TBF - Ehlers Truncated Bandpass Filter";
        Description = "Modified bandpass filter with truncated IIR memory for improved time-domain response";

        TbfSeries = new("TBF", Color.Blue, 2, LineStyle.Solid);
        BpSeries = new("BP", Color.Red, 1, LineStyle.Solid);
        ZeroSeries = new("Zero", Color.Gray, 1, LineStyle.Dot);

        AddLineSeries(TbfSeries);
        AddLineSeries(BpSeries);
        AddLineSeries(ZeroSeries);

        SeparateWindow = true;
    }

    protected override void OnInit()
    {
        tbf = new(Period, Bandwidth, Length);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = tbf!.Update(input, args.IsNewBar());

        TbfSeries!.SetValue(result.Value, tbf.IsHot, ShowColdValues);
        BpSeries!.SetValue(tbf.Bp.Value, tbf.IsHot, ShowColdValues);
        ZeroSeries!.SetValue(0.0, true, true);
    }
}
