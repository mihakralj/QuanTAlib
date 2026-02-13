using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

/// <summary>
/// TTM Wave: Multi-period MACD Composite - Quantower Indicator Adapter
/// Displays six Fibonacci-period MACD histograms grouped into A, B, C waves.
/// Matching thinkorswim TTM_Wave color conventions.
/// </summary>
[SkipLocalsInit]
public sealed class TtmWaveIndicator : Indicator, IWatchlistIndicator
{
    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private TtmWave _wave = null!;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    // Wave A: green/yellow tones (short-term)
    private readonly LineSeries _waveA1Series;
    private readonly LineSeries _waveA2Series;

    // Wave B: pink/magenta tones (medium-term)
    private readonly LineSeries _waveB1Series;
    private readonly LineSeries _waveB2Series;

    // Wave C: red/dark red tones (long-term)
    private readonly LineSeries _waveC1Series;
    private readonly LineSeries _waveC2Series;

    // Zero line
    private readonly LineSeries _zeroLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"TTM_Wave:{_sourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/ttm_wave/TtmWave.cs";

    public TtmWaveIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        _sourceName = Source.ToString();
        Name = "TTM Wave";
        Description = "John Carter's TTM Wave - Multi-period MACD composite using Fibonacci EMA periods (A/B/C waves)";

        // Wave A (short-term momentum) — yellow/green histograms
        _waveA1Series = new LineSeries("Wave A1", Color.FromArgb(0, 200, 0), 2, LineStyle.Histogramm);
        _waveA2Series = new LineSeries("Wave A2", Color.FromArgb(200, 200, 0), 2, LineStyle.Histogramm);

        // Wave B (medium-term momentum) — magenta/pink histograms
        _waveB1Series = new LineSeries("Wave B1", Color.FromArgb(200, 0, 200), 2, LineStyle.Histogramm);
        _waveB2Series = new LineSeries("Wave B2", Color.FromArgb(128, 128, 255), 2, LineStyle.Histogramm);

        // Wave C (long-term momentum) — red/orange histograms
        _waveC1Series = new LineSeries("Wave C1", Color.FromArgb(200, 0, 0), 2, LineStyle.Histogramm);
        _waveC2Series = new LineSeries("Wave C2", Color.FromArgb(255, 128, 0), 2, LineStyle.Histogramm);

        // Zero line
        _zeroLine = new LineSeries("Zero", Color.Gray, 1, LineStyle.Dash);

        AddLineSeries(_waveC1Series);
        AddLineSeries(_waveC2Series);
        AddLineSeries(_waveB1Series);
        AddLineSeries(_waveB2Series);
        AddLineSeries(_waveA1Series);
        AddLineSeries(_waveA2Series);
        AddLineSeries(_zeroLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _wave = new TtmWave();
        _sourceName = Source.ToString();
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var bar = this.GetInputBar(args);
        double price = _priceSelector(HistoricalData[Count - 1, SeekOriginHistory.Begin]);
        _ = _wave.Update(new TValue(bar.Time, price), args.IsNewBar());

        bool isHot = _wave.IsHot;

        _waveA1Series.SetValue(_wave.WaveA1.Value, isHot, ShowColdValues);
        _waveA2Series.SetValue(_wave.WaveA2.Value, isHot, ShowColdValues);
        _waveB1Series.SetValue(_wave.WaveB1.Value, isHot, ShowColdValues);
        _waveB2Series.SetValue(_wave.WaveB2.Value, isHot, ShowColdValues);
        _waveC1Series.SetValue(_wave.WaveC1.Value, isHot, ShowColdValues);
        _waveC2Series.SetValue(_wave.WaveC2.Value, isHot, ShowColdValues);
        _zeroLine.SetValue(0, isHot, ShowColdValues);
    }
}
