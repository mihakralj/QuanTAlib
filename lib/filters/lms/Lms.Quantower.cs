using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class LmsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Filter Order (taps)", sortIndex: 1, 2, 128, 1, 0)]
    public int Order { get; set; } = 16;

    [InputParameter("Learning Rate (mu)", sortIndex: 2, 0.01, 1.99, 0.05, 2)]
    public double Mu { get; set; } = 0.5;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Lms _lms = null!;
    private readonly LineSeries _lmsSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LMS {Order}:{Mu:F2}:{_sourceName}";

    public LmsIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "LMS - Least Mean Squares Adaptive Filter";
        Description = "Widrow-Hoff adaptive FIR filter with NLMS weight update for price prediction";
        _lmsSeries = new LineSeries(name: $"LMS {Order}", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_lmsSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _lms = new Lms(Order, Mu);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _lms.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _lmsSeries.SetValue(value, _lms.IsHot, ShowColdValues);
    }
}
