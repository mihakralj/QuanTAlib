using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class FsiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fundamental Cycle Period", sortIndex: 1, minimum: 6, maximum: 500, increment: 1, decimalPlaces: 0)]
    public int Period { get; set; } = 20;

    [InputParameter("Bandwidth", sortIndex: 2, minimum: 0.001, maximum: 1.0, increment: 0.01, decimalPlaces: 3)]
    public double Bandwidth { get; set; } = 0.1;

    [IndicatorExtensions.DataSourceInput(sortIndex: 3)]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Fsi _fsi = null!;
    private readonly LineSeries _fsiLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"FSI ({Period},{Bandwidth:F2})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/cycles/fsi/Fsi.Quantower.cs";

    public FsiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "FSI - Ehlers Fourier Series Indicator";
        Description = "Fourier series bandpass decomposition reconstructing a waveshape from the first three harmonics of price cycles.";

        _fsiLine = new LineSeries("FSI", Color.Yellow, 2, LineStyle.Solid);
        AddLineSeries(_fsiLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _fsi = new Fsi(Period, Bandwidth);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var priceSelector = Source.GetPriceSelector();
        var item = HistoricalData[0, SeekOriginHistory.End];
        double price = priceSelector(item);

        TValue input = new(item.TimeLeft, price);
        TValue result = _fsi.Update(input, args.IsNewBar());

        _fsiLine.SetValue(result.Value, _fsi.IsHot, ShowColdValues);
    }
}
