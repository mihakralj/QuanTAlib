using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// PPO (Percentage Price Oscillator) Quantower indicator.
/// Measures the percentage difference between fast and slow EMAs.
/// Formula: PPO = 100 × (FastEMA - SlowEMA) / SlowEMA
/// </summary>
[SkipLocalsInit]
public sealed class PpoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int FastPeriod { get; set; } = 12;

    [InputParameter("Slow Period", sortIndex: 2, 1, 2000, 1, 0)]
    public int SlowPeriod { get; set; } = 26;

    [InputParameter("Signal Period", sortIndex: 3, 1, 2000, 1, 0)]
    public int SignalPeriod { get; set; } = 9;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ppo _ppo = null!;
    private readonly LineSeries _ppoSeries;
    private readonly LineSeries _signalSeries;
    private readonly LineSeries _histSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"PPO({FastPeriod},{SlowPeriod},{SignalPeriod}):{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/ppo/Ppo.Quantower.cs";

    public PpoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "PPO - Percentage Price Oscillator";
        Description = "Percentage difference between fast and slow EMAs";

        _ppoSeries = new LineSeries(name: "PPO", color: Color.Blue, width: 2, style: LineStyle.Solid);
        _signalSeries = new LineSeries(name: "Signal", color: Color.Red, width: 2, style: LineStyle.Solid);
        _histSeries = new LineSeries(name: "Histogram", color: Color.Green, width: 2, style: LineStyle.Solid);

        AddLineSeries(_ppoSeries);
        AddLineSeries(_signalSeries);
        AddLineSeries(_histSeries);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _ppo = new Ppo(FastPeriod, SlowPeriod, SignalPeriod);
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue result = _ppo.Update(new TValue(this.GetInputBar(args).Time, _priceSelector(HistoricalData[Count - 1, SeekOriginHistory.Begin])), args.IsNewBar());

        _ppoSeries.SetValue(result.Value, _ppo.IsHot, ShowColdValues);
        _signalSeries.SetValue(_ppo.Signal.Value, _ppo.IsHot, ShowColdValues);
        _histSeries.SetValue(_ppo.Histogram.Value, _ppo.IsHot, ShowColdValues);
    }
}
