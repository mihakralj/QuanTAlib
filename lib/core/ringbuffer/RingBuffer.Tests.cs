
namespace QuanTAlib.Tests;

public class RingBufferTests
{
    [Fact]
    public void Constructor_ValidCapacity_CreatesBuffer()
    {
        var buffer = new RingBuffer(10);

        Assert.Equal(10, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
        Assert.False(buffer.IsFull);
        Assert.Equal(0, buffer.Sum);
        Assert.Equal(0, buffer.Average);
    }

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RingBuffer(0));
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RingBuffer(-1));
    }

    [Fact]
    public void Add_SingleValue_UpdatesState()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(10.0, buffer.Sum);
        Assert.Equal(10.0, buffer.Average);
        Assert.Equal(10.0, buffer.Newest);
        Assert.Equal(10.0, buffer.Oldest);
        Assert.False(buffer.IsFull);
    }

    [Fact]
    public void Add_MultipleValues_UpdatesState()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        Assert.Equal(3, buffer.Count);
        Assert.Equal(60.0, buffer.Sum);
        Assert.Equal(20.0, buffer.Average);
        Assert.Equal(30.0, buffer.Newest);
        Assert.Equal(10.0, buffer.Oldest);
        Assert.False(buffer.IsFull);
    }

    [Fact]
    public void Add_FillBuffer_BecomesFullAndWraps()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.IsFull);
        Assert.Equal(60.0, buffer.Sum);
        Assert.Equal(20.0, buffer.Average);

        // Add one more - should remove 10.0
        double removed = buffer.Add(40.0);

        Assert.Equal(10.0, removed);
        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.IsFull);
        Assert.Equal(90.0, buffer.Sum); // 20 + 30 + 40
        Assert.Equal(30.0, buffer.Average);
        Assert.Equal(40.0, buffer.Newest);
        Assert.Equal(20.0, buffer.Oldest);
    }

    [Fact]
    public void Add_MultipleWraps_MaintainsCorrectState()
    {
        var buffer = new RingBuffer(3);

        // Fill and wrap multiple times
        for (int i = 1; i <= 10; i++)
        {
            buffer.Add(i * 10.0);
        }

        // Should contain: 80, 90, 100
        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.IsFull);
        Assert.Equal(270.0, buffer.Sum); // 80 + 90 + 100
        Assert.Equal(90.0, buffer.Average);
        Assert.Equal(100.0, buffer.Newest);
        Assert.Equal(80.0, buffer.Oldest);
    }

    [Fact]
    public void UpdateNewest_ModifiesLastValue()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        Assert.Equal(60.0, buffer.Sum);

        buffer.UpdateNewest(35.0);

        Assert.Equal(65.0, buffer.Sum); // 10 + 20 + 35
        Assert.Equal(35.0, buffer.Newest);
        Assert.Equal(3, buffer.Count);
    }

    [Fact]
    public void UpdateNewest_EmptyBuffer_DoesNothing()
    {
        var buffer = new RingBuffer(5);

        buffer.UpdateNewest(100.0); // Should not throw

        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.Sum);
    }

    [Fact]
    public void Indexer_AccessesCorrectValues()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        // Index 0 = oldest, Index 2 = newest
        Assert.Equal(10.0, buffer[0]);
        Assert.Equal(20.0, buffer[1]);
        Assert.Equal(30.0, buffer[2]);
    }

    [Fact]
    public void Indexer_AfterWrap_AccessesCorrectValues()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);
        buffer.Add(40.0); // Wraps, removes 10

        // Should contain: 20, 30, 40
        Assert.Equal(20.0, buffer[0]);
        Assert.Equal(30.0, buffer[1]);
        Assert.Equal(40.0, buffer[2]);
    }

    [Fact]
    public void Indexer_OutOfRange_ThrowsException()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);

        // Valid indices are 0 and 1 (2 elements)
        // Index 2 should throw ArgumentOutOfRangeException
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[(Index)2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[(Index)10]);
    }

    [Fact]
    public void Clear_ResetsState()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.Sum);
        Assert.Equal(0, buffer.Average);
        Assert.False(buffer.IsFull);
    }

    [Fact]
    public void Clear_AllowsReuse()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);
        buffer.Clear();

        buffer.Add(100.0);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(100.0, buffer.Sum);
        Assert.Equal(100.0, buffer.Newest);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        var clone = buffer.Clone();

        // Verify clone has same state
        Assert.Equal(buffer.Count, clone.Count);
        Assert.Equal(buffer.Sum, clone.Sum);
        Assert.Equal(buffer.Newest, clone.Newest);
        Assert.Equal(buffer.Oldest, clone.Oldest);

        // Modify original - clone should be unaffected
        buffer.Add(40.0);

        Assert.Equal(4, buffer.Count);
        Assert.Equal(3, clone.Count);
        Assert.Equal(100.0, buffer.Sum);
        Assert.Equal(60.0, clone.Sum);
    }

    [Fact]
    public void CopyFrom_CopiesState()
    {
        var source = new RingBuffer(5);
        var target = new RingBuffer(5);

        source.Add(10.0);
        source.Add(20.0);
        source.Add(30.0);

        target.Add(100.0); // Different initial state

        target.CopyFrom(source);

        Assert.Equal(source.Count, target.Count);
        Assert.Equal(source.Sum, target.Sum);
        Assert.Equal(source.Newest, target.Newest);
        Assert.Equal(source.Oldest, target.Oldest);

        // Verify independence after copy
        source.Add(40.0);
        Assert.NotEqual(source.Sum, target.Sum);
    }

    [Fact]
    public void CopyFrom_DifferentCapacity_ThrowsException()
    {
        var source = new RingBuffer(5);
        var target = new RingBuffer(10);

        Assert.Throws<ArgumentException>(() => target.CopyFrom(source));
    }

    [Fact]
    public void GetSpan_ReturnsChronologicalOrder()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        var span = buffer.GetSpan();

        Assert.Equal(3, span.Length);
        Assert.Equal(10.0, span[0]);
        Assert.Equal(20.0, span[1]);
        Assert.Equal(30.0, span[2]);
    }

    [Fact]
    public void GetSpan_AfterWrap_ReturnsChronologicalOrder()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);
        buffer.Add(40.0);
        buffer.Add(50.0);

        var span = buffer.GetSpan();

        // Should be: 30, 40, 50
        Assert.Equal(3, span.Length);
        Assert.Equal(30.0, span[0]);
        Assert.Equal(40.0, span[1]);
        Assert.Equal(50.0, span[2]);
    }

    [Fact]
    public void GetSpan_EmptyBuffer_ReturnsEmpty()
    {
        var buffer = new RingBuffer(5);

        var span = buffer.GetSpan();

        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void Min_ReturnsMinimumValue()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(30.0);
        buffer.Add(10.0);
        buffer.Add(50.0);
        buffer.Add(20.0);
        buffer.Add(40.0);

        Assert.Equal(10.0, buffer.Min());
    }

    [Fact]
    public void Max_ReturnsMaximumValue()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(30.0);
        buffer.Add(10.0);
        buffer.Add(50.0);
        buffer.Add(20.0);
        buffer.Add(40.0);

        Assert.Equal(50.0, buffer.Max());
    }

    [Fact]
    public void Min_EmptyBuffer_ReturnsNaN()
    {
        var buffer = new RingBuffer(5);

        Assert.True(double.IsNaN(buffer.Min()));
    }

    [Fact]
    public void Max_EmptyBuffer_ReturnsNaN()
    {
        var buffer = new RingBuffer(5);

        Assert.True(double.IsNaN(buffer.Max()));
    }

    [Fact]
    public void Enumerator_IteratesInChronologicalOrder()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        var values = new List<double>();
        foreach (var v in buffer)
        {
            values.Add(v);
        }

        Assert.Equal(3, values.Count);
        Assert.Equal(10.0, values[0]);
        Assert.Equal(20.0, values[1]);
        Assert.Equal(30.0, values[2]);
    }

    [Fact]
    public void Enumerator_AfterWrap_IteratesInChronologicalOrder()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);
        buffer.Add(40.0);
        buffer.Add(50.0);

        var values = new List<double>();
        foreach (var v in buffer)
        {
            values.Add(v);
        }

        // Should be: 30, 40, 50
        Assert.Equal(3, values.Count);
        Assert.Equal(30.0, values[0]);
        Assert.Equal(40.0, values[1]);
        Assert.Equal(50.0, values[2]);
    }

    [Fact]
    public void Add_WithIsNew_WorksCorrectly()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0, isNew: true);
        buffer.Add(20.0, isNew: true);
        buffer.Add(25.0, isNew: false); // Should update 20.0 to 25.0

        Assert.Equal(2, buffer.Count);
        Assert.Equal(25.0, buffer.Newest);
        Assert.Equal(35.0, buffer.Sum); // 10 + 25
    }

    [Fact]
    public void Indexer_WithIndexType_SupportsFromEnd()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        Assert.Equal(30.0, buffer[^1]); // Newest
        Assert.Equal(20.0, buffer[^2]);
        Assert.Equal(10.0, buffer[^3]); // Oldest
    }

    [Fact]
    public void Newest_EmptyBuffer_ReturnsNaN()
    {
        var buffer = new RingBuffer(5);

        Assert.True(double.IsNaN(buffer.Newest));
    }

    [Fact]
    public void Oldest_EmptyBuffer_ReturnsNaN()
    {
        var buffer = new RingBuffer(5);

        Assert.True(double.IsNaN(buffer.Oldest));
    }

    [Fact]
    public void Average_EmptyBuffer_ReturnsZero()
    {
        var buffer = new RingBuffer(5);

        Assert.Equal(0, buffer.Average);
    }

    [Fact]
    public void GetInternalSpan_ReturnsFullBuffer()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);

        var span = buffer.GetInternalSpan();

        Assert.Equal(5, span.Length); // Full capacity, not count
    }

    [Fact]
    public void ToArray_AfterWrap_ReturnsChronologicalOrder()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);
        buffer.Add(40.0); // Wraps

        var arr = buffer.ToArray();

        Assert.Equal(3, arr.Length);
        Assert.Equal(20.0, arr[0]);
        Assert.Equal(30.0, arr[1]);
        Assert.Equal(40.0, arr[2]);
    }

    [Fact]
    public void CopyTo_AfterWrap_CopiesInChronologicalOrder()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);
        buffer.Add(40.0); // Wraps

        var dest = new double[5];
        buffer.CopyTo(dest, 1);

        Assert.Equal(0, dest[0]); // Untouched
        Assert.Equal(20.0, dest[1]);
        Assert.Equal(30.0, dest[2]);
        Assert.Equal(40.0, dest[3]);
        Assert.Equal(0, dest[4]); // Untouched
    }

    [Fact]
    public void Indexer_Set_UpdatesValueAndSum()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        Assert.Equal(60.0, buffer.Sum);

        buffer[(Index)1] = 25.0; // Change 20.0 to 25.0

        Assert.Equal(65.0, buffer.Sum);
        Assert.Equal(25.0, buffer[1]);
    }

    [Fact]
    public void Indexer_SetFromEnd_UpdatesValueAndSum()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);

        buffer[^1] = 35.0; // Change newest (30.0) to 35.0

        Assert.Equal(65.0, buffer.Sum);
        Assert.Equal(35.0, buffer[^1]);
    }

    [Fact]
    public void Min_LargeBuffer_UsesSimd()
    {
        var buffer = new RingBuffer(100);

        for (int i = 0; i < 100; i++)
        {
            buffer.Add(i + 1); // 1 to 100
        }

        Assert.Equal(1.0, buffer.Min());
    }

    [Fact]
    public void Max_LargeBuffer_UsesSimd()
    {
        var buffer = new RingBuffer(100);

        for (int i = 0; i < 100; i++)
        {
            buffer.Add(i + 1); // 1 to 100
        }

        Assert.Equal(100.0, buffer.Max());
    }

    [Fact]
    public void ToArray_EmptyBuffer_ReturnsEmpty()
    {
        var buffer = new RingBuffer(5);

        var arr = buffer.ToArray();

        Assert.Empty(arr);
    }

    [Fact]
    public void CopyTo_EmptyBuffer_DoesNothing()
    {
        var buffer = new RingBuffer(5);
        double[] dest = [1.0, 2.0, 3.0];

        buffer.CopyTo(dest, 0);

        Assert.Equal(1.0, dest[0]);
        Assert.Equal(2.0, dest[1]);
        Assert.Equal(3.0, dest[2]);
    }

    [Fact]
    public void InternalBuffer_ReturnsSpan()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0);

        var span = buffer.InternalBuffer;

        Assert.Equal(5, span.Length);
        Assert.Equal(10.0, span[0]);
    }

    [Fact]
    public void Enumerator_Reset_AllowsReIteration()
    {
        var buffer = new RingBuffer(3);
        buffer.Add(10.0);
        buffer.Add(20.0);

        var enumerator = buffer.GetEnumerator();

        // First iteration
        Assert.True(enumerator.MoveNext());
        Assert.Equal(10.0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20.0, enumerator.Current);
        Assert.False(enumerator.MoveNext());

        // Reset and iterate again
        enumerator.Reset();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(10.0, enumerator.Current);

        enumerator.Dispose(); // Coverage for Dispose
    }

    [Fact]
    public void IEnumerable_GetEnumerator_Works()
    {
        var buffer = new RingBuffer(3);
        buffer.Add(10.0);
        buffer.Add(20.0);

        IEnumerable<double> enumerable = buffer;
        var values = new List<double>();
        foreach (var v in enumerable)
        {
            values.Add(v);
        }

        Assert.Equal(2, values.Count);
        Assert.Equal(10.0, values[0]);
        Assert.Equal(20.0, values[1]);
    }

    [Fact]
    public void IEnumerable_NonGeneric_GetEnumerator_Works()
    {
        var buffer = new RingBuffer(3);
        buffer.Add(10.0);
        buffer.Add(20.0);

        IEnumerable enumerable = buffer;
        var values = new List<double>();
        foreach (var v in enumerable)
        {
            values.Add((double)v);
        }

        Assert.Equal(2, values.Count);
    }

    [Fact]
    public void Indexer_Set_AfterWrap_UpdatesCorrectly()
    {
        var buffer = new RingBuffer(3);

        buffer.Add(10.0);
        buffer.Add(20.0);
        buffer.Add(30.0);
        buffer.Add(40.0); // Wraps - now has 20, 30, 40

        buffer[(Index)0] = 25.0; // Change oldest (20.0) to 25.0

        Assert.Equal(95.0, buffer.Sum); // 25 + 30 + 40
        Assert.Equal(25.0, buffer[0]);
    }

    [Fact]
    public void Add_WithIsNew_EmptyBuffer_AddsValue()
    {
        var buffer = new RingBuffer(5);

        buffer.Add(10.0, isNew: false); // isNew=false but buffer empty, should still add

        Assert.Equal(1, buffer.Count);
        Assert.Equal(10.0, buffer.Newest);
    }

    [Fact]
    public void BarCorrection_Workflow()
    {
        // Simulate bar correction (isNew=false) workflow
        var buffer = new RingBuffer(3);
        var backup = new RingBuffer(3);

        // Add values as new bars
        buffer.Add(10.0);
        backup.CopyFrom(buffer);

        buffer.Add(20.0);
        backup.CopyFrom(buffer);

        buffer.Add(30.0);
        backup.CopyFrom(buffer);

        double avgBeforeCorrection = buffer.Average;

        // Simulate correction (isNew=false)
        buffer.CopyFrom(backup); // Restore previous state
        buffer.UpdateNewest(35.0); // Update with corrected value

        // Average should reflect the correction
        Assert.Equal(21.666666666666668, buffer.Average, 1e-10);
        Assert.NotEqual(avgBeforeCorrection, buffer.Average);
    }
}
