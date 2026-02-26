using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// DWT (Discrete Wavelet Transform) Quantower indicator.
/// Decomposes the input series using the à trous stationary Haar wavelet,
/// outputting either the approximation (trend) or a detail coefficient (cycles/noise).
/// </summary>
public class DwtIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Decomposition Levels", sortIndex: 0, minimum: 1, maximum: 8, increment: 1, decimalPlaces: 0)]
    public int Levels { get; set; } = 4;

    [InputParameter("Output Component (0=approx, 1..levels=detail)", sortIndex: 1, minimum: 0, maximum: 8, increment: 1, decimalPlaces: 0)]
    public int OutputComponent { get; set; } = 0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Dwt? _dwt;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => 1 << Levels; // 2^Levels
    public override string ShortName => $"DWT({Levels},{OutputComponent})";

    public DwtIndicator()
    {
        Name = "DWT - Discrete Wavelet Transform";
        Description = "À trous stationary Haar DWT — approximation (trend) or detail (cycles/noise) at selected level";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        int clampedOutput = Math.Clamp(OutputComponent, 0, Levels);
        _dwt = new Dwt(Levels, clampedOutput);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("DWT Component", Color.Yellow, 2, LineStyle.Solid));
        // Reference level at 0 (baseline for detail components)
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_dwt == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _dwt.Update(input, isNew);

        bool isHot = _dwt.IsHot;

        LinesSeries[0].SetValue(_dwt.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.0, isHot, ShowColdValues);
    }
}
