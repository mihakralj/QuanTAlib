// Mock types for TradingPlatform.BusinessLayer.Chart to enable testing
// These are minimal implementations for unit testing purposes only

namespace TradingPlatform.BusinessLayer.Chart;

/// <summary>
/// Coordinates converter interface
/// </summary>
public interface IChartWindowCoordinatesConverter
{
    DateTime GetTime(int x);
    double GetChartX(DateTime time);
    double GetChartY(double value);
}
