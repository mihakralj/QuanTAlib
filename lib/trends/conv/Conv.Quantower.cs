using System;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class ConvIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Weights (comma separated)", sortIndex: 1)]
    public string WeightsInput { get; set; } = "0.1, 0.2, 0.3, 0.4";

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Conv? _conv;
    private int _warmupBarIndex = -1;
    protected LineSeries? Series;
    protected string? SourceName;

    public int MinHistoryDepths => _conv != null ? WeightsInput.Split(',').Length : 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"CONV:{SourceName}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/trends/conv/Conv.Quantower.cs";

    public ConvIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        SourceName = Source.ToString();
        Name = "CONV - Convolution";
        Description = "Convolution with custom kernel";
        Series = new(name: "CONV", color: IndicatorExtensions.Averages, width: 2, style: LineStyle.Solid);
        AddLineSeries(Series);
    }

    protected override void OnInit()
    {
        try
        {
            var weights = WeightsInput.Split(',')
                .Select(s => double.Parse(s.Trim()))
                .ToArray();

            if (weights.Length == 0)
                throw new ArgumentException("Weights cannot be empty");

            _conv = new Conv(weights);
        }
        catch (FormatException)
        {
            _conv = new Conv([1.0]);
        }
        catch (ArgumentException)
        {
            _conv = new Conv([1.0]);
        }

        _warmupBarIndex = -1;
        SourceName = Source.ToString();
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;
        TValue result = _conv!.Update(input, isNew);
        if (_warmupBarIndex < 0 && _conv!.IsHot)
            _warmupBarIndex = Count;
        Series!.SetValue(result.Value);
        Series!.SetMarker(0, Color.Transparent); //OnPaintChart draws the line, hidden here
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);
        this.PaintSmoothCurve(args, Series!, _warmupBarIndex, showColdValues: ShowColdValues, tension: 0.2);
    }
}
