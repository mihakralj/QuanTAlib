using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class TrimaTests
{
    [Fact]
    public void StateRestoration_IsCorrect()
    {
        // Arrange
        int period = 4;
        var trimaStreaming = new Trima(period);
        var trimaBatch = new Trima(period);
        
        // Generate enough data to fill the buffers and have some history
        int count = 50;
        var data = new TSeries();
        for (int i = 0; i < count; i++)
        {
            data.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        // Act
        // 1. Feed streaming instance
        Assert.True(data.Count > 0);
        for (int i = 0; i < data.Count; i++)
        {
            trimaStreaming.Update(data[i]);
        }

        // 2. Feed batch instance with all but the last point first, then the last point
        // Actually, the Update(TSeries) method is supposed to handle the whole series and leave the state ready for the NEXT point.
        // So let's feed the whole series to batch instance.
        trimaBatch.Update(data);

        // 3. Now feed one NEW point to both
        var newPoint = new TValue(DateTime.UtcNow.AddMinutes(count), 200);
        var resultStreaming = trimaStreaming.Update(newPoint);
        var resultBatch = trimaBatch.Update(newPoint);

        // Assert
        Assert.Equal(resultStreaming.Value, resultBatch.Value, precision: 9);
    }
}
