// Mock types for TradingPlatform.BusinessLayer to enable testing
// These are minimal implementations for unit testing purposes only

using System.Drawing;

namespace TradingPlatform.BusinessLayer;

#region Enums

/// <summary>
/// Specifies the style of indicator line.
/// </summary>
public enum LineStyle
{
    Solid,
    Dash,
    Dot,
    DashDot,
    Histogramm,
    Points,
    Columns,
    StepLine
}

/// <summary>
/// Price data types
/// </summary>
public enum PriceType
{
    Open,
    High,
    Low,
    Close,
    Median,
    Typical,
    Weighted,
    Bid,
    BidSize,
    Ask,
    AskSize,
    Last,
    Volume,
    Ticks,
    AggressorFlag,
    TickDirection,
    BidTickDirection,
    AskTickDirection,
    OpenInterest,
    Mark,
    FundingRate,
    QuoteAssetVolume
}

/// <summary>
/// Seek origin for historical data
/// </summary>
public enum SeekOriginHistory
{
    Begin,
    End
}

/// <summary>
/// Update reason for indicator
/// </summary>
public enum UpdateReason
{
    Unknown,
    HistoricalBar,
    NewTick,
    NewBar
}

#endregion

#region Attributes

/// <summary>
/// Attribute for input parameters
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class InputParameterAttribute : Attribute
{
    public string Name { get; }
    public int SortIndex { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Increment { get; }
    public int DecimalPlaces { get; }
    public IComparable[]? Variants { get; }

    public InputParameterAttribute(
        string name = "",
        int sortIndex = 0,
        double minimum = int.MinValue,
        double maximum = int.MaxValue,
        double increment = 0.01,
        int decimalPlaces = 2,
        object[]? variants = null)
    {
        Name = name;
        SortIndex = sortIndex;
        Minimum = minimum;
        Maximum = maximum;
        Increment = increment;
        DecimalPlaces = decimalPlaces;
        Variants = variants?.Cast<IComparable>().ToArray();
    }
}

#endregion

#region History Item

/// <summary>
/// History item interface
/// </summary>
public interface IHistoryItem
{
    DateTime TimeLeft { get; }
    long TicksLeft { get; set; }
    long TicksRight { get; set; }
    double this[PriceType priceType] { get; }
}

/// <summary>
/// Mock history item for testing
/// </summary>
public class MockHistoryItem : IHistoryItem
{
    public DateTime TimeLeft { get; set; }
    public long TicksLeft { get; set; }
    public long TicksRight { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }

    public double this[PriceType priceType] => priceType switch
    {
        PriceType.Open => Open,
        PriceType.High => High,
        PriceType.Low => Low,
        PriceType.Close => Close,
        PriceType.Volume => Volume,
        PriceType.Median => (High + Low) / 2,
        PriceType.Typical => (High + Low + Close) / 3,
        PriceType.Weighted => (High + Low + Close + Close) / 4,
        _ => Close
    };
}

#endregion

#region Historical Data

/// <summary>
/// Mock historical data for testing
/// </summary>
public class HistoricalData
{
    private readonly List<IHistoryItem> _items = new();

    public int Count => _items.Count;

    public IHistoryItem this[int offset, SeekOriginHistory origin = SeekOriginHistory.End]
    {
        get
        {
            int index = origin == SeekOriginHistory.End
                ? Count - 1 - offset
                : offset;
            return _items[index];
        }
    }

    public DateTime Time(int offset = 0, SeekOriginHistory origin = SeekOriginHistory.End)
    {
        return this[offset, origin].TimeLeft;
    }

    public long GetIndexByTime(long ticks)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].TicksLeft == ticks)
                return i;
        }
        return -1;
    }

    public void Add(IHistoryItem item)
    {
        _items.Add(item);
    }

    public void AddBar(DateTime time, double open, double high, double low, double close, double volume = 0)
    {
        _items.Add(new MockHistoryItem
        {
            TimeLeft = time,
            TicksLeft = time.Ticks,
            TicksRight = time.Ticks,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume
        });
    }

    public void Clear() => _items.Clear();
}

#endregion

#region Update Args

/// <summary>
/// Update arguments for indicator
/// </summary>
public class UpdateArgs
{
    public UpdateReason Reason { get; }

    public UpdateArgs(UpdateReason reason)
    {
        Reason = reason;
    }
}

#endregion

#region Line Series

/// <summary>
/// Base class for lines
/// </summary>
public abstract class Line
{
    public string Name { get; set; }
    public Color Color { get; set; }
    public int Width { get; set; }
    public LineStyle Style { get; set; }
    public bool Visible { get; set; } = true;

    protected Line(string name, Color color, int width, LineStyle style)
    {
        Name = name;
        Color = color;
        Width = width;
        Style = style;
    }
}

/// <summary>
/// Line series for indicator output
/// </summary>
public class LineSeries : Line
{
    private readonly List<double> _values = new();
    private readonly List<Color> _markers = new();

    public int TimeShift { get; set; }
    public int DrawBegin { get; set; }
    public bool ShowLineMarker { get; set; } = true;

    public LineSeries(string name, Color color, int width, LineStyle style)
        : base(name, color, width, style)
    {
    }

    public double this[int offset = 0, SeekOriginHistory origin = SeekOriginHistory.End]
    {
        get => GetValue(offset, origin);
        set => SetValue(value, offset, origin);
    }

    public double GetValue(int offset = 0, SeekOriginHistory origin = SeekOriginHistory.End)
    {
        if (_values.Count == 0)
            return double.NaN;

        int index = origin == SeekOriginHistory.End
            ? _values.Count - 1 - offset
            : offset;

        if (index < 0 || index >= _values.Count)
            return double.NaN;

        return _values[index];
    }

    public void SetValue(double value, int offset = 0, SeekOriginHistory origin = SeekOriginHistory.End)
    {
        EnsureCapacity(offset + 1);
        int index = origin == SeekOriginHistory.End
            ? _values.Count - 1 - offset
            : offset;
        _values[index] = value;
    }

    public void SetMarker(int offset, Color color)
    {
        EnsureMarkerCapacity(offset + 1);
        int index = _markers.Count - 1 - offset;
        if (index >= 0 && index < _markers.Count)
            _markers[index] = color;
    }

    internal void AddValue()
    {
        _values.Add(double.NaN);
        _markers.Add(Color.Transparent);
    }

    private void EnsureCapacity(int count)
    {
        while (_values.Count < count)
            _values.Add(double.NaN);
    }

    private void EnsureMarkerCapacity(int count)
    {
        while (_markers.Count < count)
            _markers.Add(Color.Transparent);
    }

    public int Count => _values.Count;
    public IReadOnlyList<double> Values => _values;
}

#endregion

#region Paint Chart Event Args

/// <summary>
/// Paint chart event arguments
/// </summary>
public class PaintChartEventArgs : EventArgs
{
    public Graphics Graphics { get; }
    public Rectangle ClipRectangle { get; }
    public int WindowIndex { get; }

    public PaintChartEventArgs(Graphics graphics, Rectangle clipRectangle, int windowIndex = 0)
    {
        Graphics = graphics;
        ClipRectangle = clipRectangle;
        WindowIndex = windowIndex;
    }
}

#endregion

#region Chart

/// <summary>
/// Chart interface
/// </summary>
public interface IChart
{
    ChartWindow MainWindow { get; }
    ChartWindow[] Windows { get; }
    int BarsWidth { get; }
}

/// <summary>
/// Chart window
/// </summary>
public class ChartWindow
{
    public Rectangle ClientRectangle { get; set; }
    public ICoordinatesConverter CoordinatesConverter { get; set; } = new MockCoordinatesConverter();
}

/// <summary>
/// Coordinates converter interface
/// </summary>
public interface ICoordinatesConverter
{
    DateTime GetTime(int x);
    double GetChartX(DateTime time);
    double GetChartY(double value);
}

/// <summary>
/// Mock coordinates converter
/// </summary>
public class MockCoordinatesConverter : ICoordinatesConverter
{
    public DateTime GetTime(int x) => DateTime.UtcNow;
    public double GetChartX(DateTime time) => 0;
    public double GetChartY(double value) => 0;
}

/// <summary>
/// Mock chart for testing
/// </summary>
public class MockChart : IChart
{
    public ChartWindow MainWindow { get; } = new();
    public ChartWindow[] Windows { get; } = new[] { new ChartWindow() };
    public int BarsWidth { get; set; } = 10;
}

#endregion

#region Indicator Base

/// <summary>
/// Watchlist indicator interface
/// </summary>
public interface IWatchlistIndicator
{
    int MinHistoryDepths { get; }
}

/// <summary>
/// Base class for indicators
/// </summary>
public abstract class Indicator
{
    private readonly List<LineSeries> _lineSeries = new();

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public virtual string ShortName => Name;
    public virtual string SourceCodeLink => string.Empty;

    public bool SeparateWindow { get; set; }
    public bool OnBackGround { get; set; }

    public HistoricalData HistoricalData { get; set; } = new();
    public IChart? CurrentChart { get; set; }

    public int Count => HistoricalData.Count;

    public LineSeries[] LinesSeries => _lineSeries.ToArray();

    protected void AddLineSeries(LineSeries series)
    {
        _lineSeries.Add(series);
    }

    /// <summary>
    /// Called when indicator is initialized
    /// </summary>
    protected virtual void OnInit()
    {
    }

    /// <summary>
    /// Called on each update
    /// </summary>
    protected virtual void OnUpdate(UpdateArgs args)
    {
    }

    /// <summary>
    /// Called for chart painting
    /// </summary>
    public virtual void OnPaintChart(PaintChartEventArgs args)
    {
    }

    /// <summary>
    /// Initialize the indicator (for testing)
    /// </summary>
    public void Initialize()
    {
        OnInit();
    }

    /// <summary>
    /// Process an update (for testing)
    /// </summary>
    public void ProcessUpdate(UpdateArgs args)
    {
        // Ensure line series have capacity for new data
        foreach (var series in _lineSeries)
        {
            series.AddValue();
        }
        OnUpdate(args);
    }
}

#endregion
