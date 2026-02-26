using System.Drawing;
using TradingPlatform.BusinessLayer;
using static QuanTAlib.IndicatorExtensions;

namespace QuanTAlib;

/// <summary>
/// CWT (Continuous Wavelet Transform) Quantower indicator.
/// Computes the Morlet CWT magnitude at a specified scale, providing
/// time-localized frequency-band energy decomposition.
/// </summary>
public class CwtIndicator : Indicator, IWatchlistIndicator
{
    [DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Scale", sortIndex: 0, minimum: 0.5, maximum: 200.0, increment: 0.5, decimalPlaces: 1)]
    public double Scale { get; set; } = 10.0;

    [InputParameter("Omega0 (Central Frequency)", sortIndex: 1, minimum: 1.0, maximum: 20.0, increment: 0.5, decimalPlaces: 1)]
    public double Omega0 { get; set; } = 6.0;

    [InputParameter("Show Cold Values", sortIndex: 100)]
    public bool ShowColdValues { get; set; } = true;

    private Cwt? _cwt;
    private Func<IHistoryItem, double>? _selector;

    public int MinHistoryDepths => (int)(2 * Math.Round(3.0 * Scale) + 1);
    public override string ShortName => $"CWT({Scale:G},{Omega0:G})";

    public CwtIndicator()
    {
        Name = "CWT - Continuous Wavelet Transform";
        Description = "Morlet CWT magnitude at a specified scale — time-frequency decomposition";
        SeparateWindow = true;
        OnBackGround = true;
    }

    protected override void OnInit()
    {
        _cwt = new Cwt(Scale, Omega0);
        _selector = Source.GetPriceSelector();

        AddLineSeries(new LineSeries("CWT Magnitude", Color.Cyan, 2, LineStyle.Solid));
        // Reference level at 0 (baseline)
        AddLineSeries(new LineSeries("Zero", Color.Gray, 1, LineStyle.Dash));
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (_cwt == null || _selector == null)
        {
            return;
        }

        var item = HistoricalData[0, SeekOriginHistory.End];
        double value = _selector(item);
        bool isNew = args.IsNewBar();

        TValue input = new(item.TimeLeft, value);
        _cwt.Update(input, isNew);

        bool isHot = _cwt.IsHot;

        LinesSeries[0].SetValue(_cwt.Last.Value, isHot, ShowColdValues);
        LinesSeries[1].SetValue(0.0, isHot, ShowColdValues);
    }
}
