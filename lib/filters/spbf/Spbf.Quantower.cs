using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class SpbfIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Short Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int ShortPeriod { get; set; } = 40;

    [InputParameter("Long Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int LongPeriod { get; set; } = 60;

    [InputParameter("RMS Period", sortIndex: 3, 1, 2000, 1, 0)]
    public int RmsPeriod { get; set; } = 50;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Spbf _spbf = null!;
    private readonly LineSeries _pbSeries;
    private readonly LineSeries _rmsPosSeries;
    private readonly LineSeries _rmsNegSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"SPBF {ShortPeriod}:{LongPeriod}:{RmsPeriod}:{_sourceName}";

    public SpbfIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "SPBF - Ehlers Super Passband Filter";
        Description = "Ehlers Super Passband Filter: wide-band bandpass via differenced z-transformed EMAs with RMS trigger envelope";
        _pbSeries = new LineSeries(name: $"SPBF {ShortPeriod}:{LongPeriod}", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _rmsPosSeries = new LineSeries(name: "+RMS", color: Color.Red, width: 1, style: LineStyle.Dash);
        _rmsNegSeries = new LineSeries(name: "-RMS", color: Color.Green, width: 1, style: LineStyle.Dash);
        AddLineSeries(_pbSeries);
        AddLineSeries(_rmsPosSeries);
        AddLineSeries(_rmsNegSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _spbf = new Spbf(ShortPeriod, LongPeriod, RmsPeriod);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _spbf.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        double rms = _spbf.Rms;
        _pbSeries.SetValue(value, _spbf.IsHot, ShowColdValues);
        _rmsPosSeries.SetValue(rms, _spbf.IsHot, ShowColdValues);
        _rmsNegSeries.SetValue(-rms, _spbf.IsHot, ShowColdValues);
    }
}
