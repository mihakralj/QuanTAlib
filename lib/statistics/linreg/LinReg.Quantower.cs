using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class LinRegIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [InputParameter("Offset", sortIndex: 2, -2000, 2000, 1, 0)]
    public int Offset { get; set; } = 0;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private LinReg _linreg = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LinReg({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/linreg/LinReg.Quantower.cs";

    public LinRegIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "LinReg - Linear Regression Curve";
        Description = "Plots the end point of the linear regression line for each bar.";

        _series = new LineSeries(name: "LinReg", color: IndicatorExtensions.Statistics, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _linreg = new LinReg(Period, Offset);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        TValue result = _linreg.Update(input, args.IsNewBar());

        _series.SetValue(result.Value, _linreg.IsHot, ShowColdValues);
    }
}

[SkipLocalsInit]
public sealed class LinRegSlopeIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private LinReg _linreg = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LinRegSlope({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/linreg/LinReg.Quantower.cs";

    public LinRegSlopeIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "LinReg Slope";
        Description = "Plots the slope of the linear regression line.";

        _series = new LineSeries(name: "Slope", color: IndicatorExtensions.Momentum, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _linreg = new LinReg(Period);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        _linreg.Update(input, args.IsNewBar());

        _series.SetValue(_linreg.Slope, _linreg.IsHot, ShowColdValues);
    }
}

[SkipLocalsInit]
public sealed class LinRegInterceptIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private LinReg _linreg = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LinRegIntercept({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/linreg/LinReg.Quantower.cs";

    public LinRegInterceptIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "LinReg Intercept";
        Description = "Plots the intercept of the linear regression line.";

        _series = new LineSeries(name: "Intercept", color: IndicatorExtensions.Experiments, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _linreg = new LinReg(Period);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        _linreg.Update(input, args.IsNewBar());

        _series.SetValue(_linreg.Intercept, _linreg.IsHot, ShowColdValues);
    }
}

[SkipLocalsInit]
public sealed class LinRegRSquaredIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private LinReg _linreg = null!;
    private readonly LineSeries _series;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"LinRegR2({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/statistics/linreg/LinReg.Quantower.cs";

    public LinRegRSquaredIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "LinReg R-Squared";
        Description = "Plots the R-Squared (coefficient of determination) of the linear regression line.";

        _series = new LineSeries(name: "RSquared", color: IndicatorExtensions.Oscillators, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _linreg = new LinReg(Period);
        _priceSelector = Source.GetPriceSelector();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        var item = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
        double value = _priceSelector(item);
        var time = this.HistoricalData.Time();

        var input = new TValue(time, value);
        _linreg.Update(input, args.IsNewBar());

        _series.SetValue(_linreg.RSquared, _linreg.IsHot, ShowColdValues);
    }
}
