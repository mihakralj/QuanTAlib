namespace QuanTAlib.Tests;

/// <summary>
/// Tests for MonotonicDeque — O(1) amortized sliding window min/max data structure.
/// Covers: constructor validation, PushMax, PushMin, GetExtremum, Reset,
/// RebuildMax, RebuildMin, FrontIndex, Count, window expiration,
/// edge cases (equal values, descending/ascending sequences).
/// </summary>
public class MonotonicDequeTests
{
    // ═══════════════════════════════ Constructor ═══════════════════════════════

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonotonicDeque(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonotonicDeque(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var deque = new MonotonicDeque(5);
        Assert.NotNull(deque);
    }

    [Fact]
    public void Constructor_PeriodOne_IsValid()
    {
        var deque = new MonotonicDeque(1);
        Assert.Equal(0, deque.Count);
    }

    // ═══════════════════════════════ Initial State ════════════════════════════

    [Fact]
    public void Count_InitiallyZero()
    {
        var deque = new MonotonicDeque(5);
        Assert.Equal(0, deque.Count);
    }

    [Fact]
    public void FrontIndex_InitiallyNegativeOne()
    {
        var deque = new MonotonicDeque(5);
        Assert.Equal(-1, deque.FrontIndex);
    }

    [Fact]
    public void GetExtremum_Empty_ReturnsNaN()
    {
        var deque = new MonotonicDeque(5);
        double[] buffer = new double[5];
        Assert.True(double.IsNaN(deque.GetExtremum(buffer)));
    }

    // ═══════════════════════════════ PushMax ═══════════════════════════════════

    [Fact]
    public void PushMax_SingleValue_Tracked()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 100.0;
        deque.PushMax(0, 100.0, buffer);

        Assert.Equal(1, deque.Count);
        Assert.Equal(100.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMax_AscendingValues_TracksMaximum()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 5; i++)
        {
            buffer[i % period] = i * 10.0;
            deque.PushMax(i, i * 10.0, buffer);
        }

        // Maximum should be 40 (last value in ascending sequence)
        Assert.Equal(40.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMax_DescendingValues_TracksMaximum()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 5; i++)
        {
            double val = (4 - i) * 10.0;
            buffer[i % period] = val;
            deque.PushMax(i, val, buffer);
        }

        // Maximum should be 40 (first value in descending sequence)
        Assert.Equal(40.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMax_EqualValues_TracksCorrectly()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 5; i++)
        {
            buffer[i % period] = 50.0;
            deque.PushMax(i, 50.0, buffer);
        }

        Assert.Equal(50.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMax_WindowExpiration_DropsOldMax()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        // Push: 100, 50, 30, then 20 (window slides past 100)
        buffer[0 % period] = 100.0;
        deque.PushMax(0, 100.0, buffer);
        Assert.Equal(100.0, deque.GetExtremum(buffer));

        buffer[1 % period] = 50.0;
        deque.PushMax(1, 50.0, buffer);
        Assert.Equal(100.0, deque.GetExtremum(buffer));

        buffer[2 % period] = 30.0;
        deque.PushMax(2, 30.0, buffer);
        Assert.Equal(100.0, deque.GetExtremum(buffer));

        // Index 3 — window now [1,2,3], so 100 (index 0) expires
        buffer[3 % period] = 20.0;
        deque.PushMax(3, 20.0, buffer);
        Assert.Equal(50.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMax_NewMaxReplacesAll()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0 % period] = 10.0;
        deque.PushMax(0, 10.0, buffer);
        buffer[1 % period] = 20.0;
        deque.PushMax(1, 20.0, buffer);
        buffer[2 % period] = 30.0;
        deque.PushMax(2, 30.0, buffer);

        // New value 100 should replace all (they're all <=)
        buffer[3 % period] = 100.0;
        deque.PushMax(3, 100.0, buffer);
        Assert.Equal(100.0, deque.GetExtremum(buffer));
        Assert.Equal(1, deque.Count);
    }

    // ═══════════════════════════════ PushMin ═══════════════════════════════════

    [Fact]
    public void PushMin_SingleValue_Tracked()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 100.0;
        deque.PushMin(0, 100.0, buffer);

        Assert.Equal(1, deque.Count);
        Assert.Equal(100.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMin_DescendingValues_TracksMinimum()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 5; i++)
        {
            double val = (4 - i) * 10.0;
            buffer[i % period] = val;
            deque.PushMin(i, val, buffer);
        }

        // Minimum should be 0 (last value in descending sequence)
        Assert.Equal(0.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMin_AscendingValues_TracksMinimum()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 5; i++)
        {
            buffer[i % period] = i * 10.0;
            deque.PushMin(i, i * 10.0, buffer);
        }

        // Minimum should be 0 (first value in ascending sequence)
        Assert.Equal(0.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMin_EqualValues_TracksCorrectly()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 5; i++)
        {
            buffer[i % period] = 50.0;
            deque.PushMin(i, 50.0, buffer);
        }

        Assert.Equal(50.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMin_WindowExpiration_DropsOldMin()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        // Push: 10, 50, 80, then 90 (window slides past 10)
        buffer[0 % period] = 10.0;
        deque.PushMin(0, 10.0, buffer);
        Assert.Equal(10.0, deque.GetExtremum(buffer));

        buffer[1 % period] = 50.0;
        deque.PushMin(1, 50.0, buffer);
        Assert.Equal(10.0, deque.GetExtremum(buffer));

        buffer[2 % period] = 80.0;
        deque.PushMin(2, 80.0, buffer);
        Assert.Equal(10.0, deque.GetExtremum(buffer));

        // Index 3 — window now [1,2,3], so 10 (index 0) expires
        buffer[3 % period] = 90.0;
        deque.PushMin(3, 90.0, buffer);
        Assert.Equal(50.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMin_NewMinReplacesAll()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0 % period] = 100.0;
        deque.PushMin(0, 100.0, buffer);
        buffer[1 % period] = 80.0;
        deque.PushMin(1, 80.0, buffer);
        buffer[2 % period] = 60.0;
        deque.PushMin(2, 60.0, buffer);

        // New value 5 should replace all (they're all >=)
        buffer[3 % period] = 5.0;
        deque.PushMin(3, 5.0, buffer);
        Assert.Equal(5.0, deque.GetExtremum(buffer));
        Assert.Equal(1, deque.Count);
    }

    // ═══════════════════════════════ FrontIndex ═══════════════════════════════

    [Fact]
    public void FrontIndex_TracksCurrentExtremumIndex()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 100.0;
        deque.PushMax(0, 100.0, buffer);
        Assert.Equal(0, deque.FrontIndex);

        buffer[1 % period] = 50.0;
        deque.PushMax(1, 50.0, buffer);
        Assert.Equal(0, deque.FrontIndex); // 100 is still max

        buffer[2 % period] = 200.0;
        deque.PushMax(2, 200.0, buffer);
        Assert.Equal(2, deque.FrontIndex); // 200 is new max
    }

    // ═══════════════════════════════ Reset ════════════════════════════════════

    [Fact]
    public void Reset_ClearsCount()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 3; i++)
        {
            buffer[i] = i * 10.0;
            deque.PushMax(i, i * 10.0, buffer);
        }
        Assert.True(deque.Count > 0);

        deque.Reset();
        Assert.Equal(0, deque.Count);
    }

    [Fact]
    public void Reset_FrontIndexBecomesNegativeOne()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 50.0;
        deque.PushMax(0, 50.0, buffer);
        deque.Reset();
        Assert.Equal(-1, deque.FrontIndex);
    }

    [Fact]
    public void Reset_GetExtremumReturnsNaN()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 50.0;
        deque.PushMax(0, 50.0, buffer);
        deque.Reset();
        Assert.True(double.IsNaN(deque.GetExtremum(buffer)));
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 100.0;
        deque.PushMax(0, 100.0, buffer);
        deque.Reset();

        buffer[0] = 50.0;
        deque.PushMax(0, 50.0, buffer);
        Assert.Equal(50.0, deque.GetExtremum(buffer));
        Assert.Equal(1, deque.Count);
    }

    // ═══════════════════════════════ RebuildMax ═══════════════════════════════

    [Fact]
    public void RebuildMax_EmptyBuffer_NoOp()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        deque.RebuildMax(buffer, 0, 0);
        Assert.Equal(0, deque.Count);
    }

    [Fact]
    public void RebuildMax_RebuildsCorrectly()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        // Fill buffer: [10, 50, 30, 20, 40]
        buffer[0] = 10.0;
        buffer[1] = 50.0;
        buffer[2] = 30.0;
        buffer[3] = 20.0;
        buffer[4] = 40.0;

        deque.RebuildMax(buffer, 4, 5);

        // Maximum should be 50 (at index 1)
        Assert.Equal(50.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void RebuildMax_SingleElement()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 42.0;
        deque.RebuildMax(buffer, 0, 1);

        Assert.Equal(42.0, deque.GetExtremum(buffer));
        Assert.Equal(1, deque.Count);
    }

    [Fact]
    public void RebuildMax_AfterPreviousData_Resets()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        // First push some data
        buffer[0] = 100.0;
        deque.PushMax(0, 100.0, buffer);
        buffer[1] = 200.0;
        deque.PushMax(1, 200.0, buffer);

        // Rebuild with different data
        buffer[0] = 10.0;
        buffer[1] = 20.0;
        buffer[2] = 15.0;
        deque.RebuildMax(buffer, 2, 3);

        Assert.Equal(20.0, deque.GetExtremum(buffer));
    }

    // ═══════════════════════════════ RebuildMin ═══════════════════════════════

    [Fact]
    public void RebuildMin_EmptyBuffer_NoOp()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        deque.RebuildMin(buffer, 0, 0);
        Assert.Equal(0, deque.Count);
    }

    [Fact]
    public void RebuildMin_RebuildsCorrectly()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        // Fill buffer: [10, 50, 30, 20, 40]
        buffer[0] = 10.0;
        buffer[1] = 50.0;
        buffer[2] = 30.0;
        buffer[3] = 20.0;
        buffer[4] = 40.0;

        deque.RebuildMin(buffer, 4, 5);

        // Minimum should be 10 (at index 0)
        Assert.Equal(10.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void RebuildMin_SingleElement()
    {
        int period = 5;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 42.0;
        deque.RebuildMin(buffer, 0, 1);

        Assert.Equal(42.0, deque.GetExtremum(buffer));
        Assert.Equal(1, deque.Count);
    }

    [Fact]
    public void RebuildMin_AfterPreviousData_Resets()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        // First push some data
        buffer[0] = 5.0;
        deque.PushMin(0, 5.0, buffer);
        buffer[1] = 3.0;
        deque.PushMin(1, 3.0, buffer);

        // Rebuild with different data
        buffer[0] = 100.0;
        buffer[1] = 200.0;
        buffer[2] = 150.0;
        deque.RebuildMin(buffer, 2, 3);

        Assert.Equal(100.0, deque.GetExtremum(buffer));
    }

    // ═══════════════════════════════ Sliding Window Scenarios ═════════════════

    [Fact]
    public void PushMax_LongSequence_TracksRollingMax()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];
        double[] values = [10, 20, 30, 15, 25, 5, 35, 10, 40, 20];
        double[] expectedMax = [10, 20, 30, 30, 30, 25, 35, 35, 40, 40];

        for (int i = 0; i < values.Length; i++)
        {
            buffer[i % period] = values[i];
            deque.PushMax(i, values[i], buffer);
            double actual = deque.GetExtremum(buffer);
            Assert.True(actual == expectedMax[i],
                $"Max mismatch at index {i}: expected {expectedMax[i]}, got {actual}");
        }
    }

    [Fact]
    public void PushMin_LongSequence_TracksRollingMin()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];
        double[] values = [50, 40, 30, 45, 35, 55, 25, 60, 20, 50];
        double[] expectedMin = [50, 40, 30, 30, 30, 35, 25, 25, 20, 20];

        for (int i = 0; i < values.Length; i++)
        {
            buffer[i % period] = values[i];
            deque.PushMin(i, values[i], buffer);
            double actual = deque.GetExtremum(buffer);
            Assert.True(actual == expectedMin[i],
                $"Min mismatch at index {i}: expected {expectedMin[i]}, got {actual}");
        }
    }

    // ═══════════════════════════════ Period 1 ═════════════════════════════════

    [Fact]
    public void PushMax_PeriodOne_AlwaysLatestValue()
    {
        int period = 1;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 100.0;
        deque.PushMax(0, 100.0, buffer);
        Assert.Equal(100.0, deque.GetExtremum(buffer));

        buffer[0] = 50.0;
        deque.PushMax(1, 50.0, buffer);
        Assert.Equal(50.0, deque.GetExtremum(buffer));

        buffer[0] = 200.0;
        deque.PushMax(2, 200.0, buffer);
        Assert.Equal(200.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMin_PeriodOne_AlwaysLatestValue()
    {
        int period = 1;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 100.0;
        deque.PushMin(0, 100.0, buffer);
        Assert.Equal(100.0, deque.GetExtremum(buffer));

        buffer[0] = 50.0;
        deque.PushMin(1, 50.0, buffer);
        Assert.Equal(50.0, deque.GetExtremum(buffer));
    }

    // ═══════════════════════════════ Edge Cases ═══════════════════════════════

    [Fact]
    public void PushMax_NegativeValues_TracksCorrectly()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = -30.0;
        deque.PushMax(0, -30.0, buffer);
        buffer[1 % period] = -10.0;
        deque.PushMax(1, -10.0, buffer);
        buffer[2 % period] = -20.0;
        deque.PushMax(2, -20.0, buffer);

        Assert.Equal(-10.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMin_NegativeValues_TracksCorrectly()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = -10.0;
        deque.PushMin(0, -10.0, buffer);
        buffer[1 % period] = -30.0;
        deque.PushMin(1, -30.0, buffer);
        buffer[2 % period] = -20.0;
        deque.PushMin(2, -20.0, buffer);

        Assert.Equal(-30.0, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMax_VeryLargeValues_NoOverflow()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        buffer[0] = 1e300;
        deque.PushMax(0, 1e300, buffer);
        Assert.Equal(1e300, deque.GetExtremum(buffer));
    }

    [Fact]
    public void PushMax_ZeroValues_TracksCorrectly()
    {
        int period = 3;
        var deque = new MonotonicDeque(period);
        double[] buffer = new double[period];

        for (int i = 0; i < 3; i++)
        {
            buffer[i] = 0.0;
            deque.PushMax(i, 0.0, buffer);
        }

        Assert.Equal(0.0, deque.GetExtremum(buffer));
    }
}
