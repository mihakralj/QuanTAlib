namespace QuanTAlib.Tests;

public class TValueEventArgsTests
{
    [Fact]
    public void Constructor_WithValueAndIsNew_SetsPropertiesCorrectly()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, 123.45);
        const bool isNew = true;

        var eventArgs = new TValueEventArgs { Value = tValue, IsNew = isNew };

        Assert.Equal(tValue, eventArgs.Value);
        Assert.True(eventArgs.IsNew);
    }

    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        var eventArgs = new TValueEventArgs();

        Assert.Equal(default(TValue), eventArgs.Value);
        Assert.False(eventArgs.IsNew);
    }

    [Fact]
    public void Constructor_WithNaNValue_PreservesNaN()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.NaN);

        var eventArgs = new TValueEventArgs { Value = tValue, IsNew = false };

        Assert.True(double.IsNaN(eventArgs.Value.Value));
    }

    [Fact]
    public void Constructor_WithInfinityValue_PreservesInfinity()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.PositiveInfinity);

        var eventArgs = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.True(double.IsPositiveInfinity(eventArgs.Value.Value));
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var tValue = new TValue(12345, 100.0);
        var args1 = new TValueEventArgs { Value = tValue, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.True(args1.Equals(args2));
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var tValue1 = new TValue(12345, 100.0);
        var tValue2 = new TValue(12345, 101.0);
        var args1 = new TValueEventArgs { Value = tValue1, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue2, IsNew = true };

        Assert.False(args1.Equals(args2));
    }

    [Fact]
    public void Equals_DifferentTime_ReturnsFalse()
    {
        var tValue1 = new TValue(12345, 100.0);
        var tValue2 = new TValue(12346, 100.0);
        var args1 = new TValueEventArgs { Value = tValue1, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue2, IsNew = true };

        Assert.False(args1.Equals(args2));
    }

    [Fact]
    public void Equals_DifferentIsNew_ReturnsFalse()
    {
        var tValue = new TValue(12345, 100.0);
        var args1 = new TValueEventArgs { Value = tValue, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue, IsNew = false };

        Assert.False(args1.Equals(args2));
    }

    [Fact]
    public void Equals_Object_SameTValueEventArgs_ReturnsTrue()
    {
        var tValue = new TValue(12345, 100.0);
        var args1 = new TValueEventArgs { Value = tValue, IsNew = true };
        object args2 = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.True(args1.Equals(args2));
    }

    [Fact]
    public void Equals_Object_DifferentType_ReturnsFalse()
    {
        var args = new TValueEventArgs { Value = new TValue(12345, 100.0), IsNew = true };
        object other = "not a TValueEventArgs";

        Assert.False(args.Equals(other));
    }

    [Fact]
    public void Equals_Object_Null_ReturnsFalse()
    {
        var args = new TValueEventArgs { Value = new TValue(12345, 100.0), IsNew = true };

        Assert.False(args.Equals(null));
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHashCode()
    {
        var tValue = new TValue(12345, 100.0);
        var args1 = new TValueEventArgs { Value = tValue, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.Equal(args1.GetHashCode(), args2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHashCode()
    {
        var tValue1 = new TValue(12345, 100.0);
        var tValue2 = new TValue(12346, 100.0);
        var args1 = new TValueEventArgs { Value = tValue1, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue2, IsNew = true };

        Assert.NotEqual(args1.GetHashCode(), args2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentIsNew_ReturnsDifferentHashCode()
    {
        var tValue = new TValue(12345, 100.0);
        var args1 = new TValueEventArgs { Value = tValue, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue, IsNew = false };

        Assert.NotEqual(args1.GetHashCode(), args2.GetHashCode());
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        var tValue = new TValue(12345, 100.0);
        var args1 = new TValueEventArgs { Value = tValue, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.True(args1 == args2);
    }

    [Fact]
    public void EqualityOperator_DifferentValues_ReturnsFalse()
    {
        var tValue1 = new TValue(12345, 100.0);
        var tValue2 = new TValue(12346, 100.0);
        var args1 = new TValueEventArgs { Value = tValue1, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue2, IsNew = true };

        Assert.False(args1 == args2);
    }

    [Fact]
    public void InequalityOperator_SameValues_ReturnsFalse()
    {
        var tValue = new TValue(12345, 100.0);
        var args1 = new TValueEventArgs { Value = tValue, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.False(args1 != args2);
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        var tValue1 = new TValue(12345, 100.0);
        var tValue2 = new TValue(12346, 100.0);
        var args1 = new TValueEventArgs { Value = tValue1, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue2, IsNew = true };

        Assert.True(args1 != args2);
    }

    [Fact]
    public void Equals_WithNaN_BothNaN_ReturnsTrue()
    {
        var tValue1 = new TValue(12345, double.NaN);
        var tValue2 = new TValue(12345, double.NaN);
        var args1 = new TValueEventArgs { Value = tValue1, IsNew = true };
        var args2 = new TValueEventArgs { Value = tValue2, IsNew = true };

        Assert.True(args1.Equals(args2));
    }

    [Fact]
    public void GetHashCode_WithNaN_DoesNotThrow()
    {
        var tValue = new TValue(12345, double.NaN);
        var args = new TValueEventArgs { Value = tValue, IsNew = true };

        var hash = args.GetHashCode();

        Assert.True(hash != 0 || hash == 0); // Just verify it doesn't throw
    }

    [Fact]
    public void Constructor_WithZeroTime_Allowed()
    {
        var tValue = new TValue(0, 100.0);
        var args = new TValueEventArgs { Value = tValue, IsNew = false };

        Assert.Equal(0, args.Value.Time);
        Assert.Equal(100.0, args.Value.Value);
        Assert.False(args.IsNew);
    }

    [Fact]
    public void Constructor_WithNegativeTime_Allowed()
    {
        var tValue = new TValue(-12345, 100.0);
        var args = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.Equal(-12345, args.Value.Time);
        Assert.Equal(100.0, args.Value.Value);
        Assert.True(args.IsNew);
    }

    [Fact]
    public void Constructor_WithMaxLongTime_Allowed()
    {
        var tValue = new TValue(long.MaxValue, 100.0);
        var args = new TValueEventArgs { Value = tValue, IsNew = false };

        Assert.Equal(long.MaxValue, args.Value.Time);
        Assert.Equal(100.0, args.Value.Value);
        Assert.False(args.IsNew);
    }

    [Fact]
    public void Constructor_WithMaxDoubleValue_Allowed()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.MaxValue);
        var args = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.Equal(double.MaxValue, args.Value.Value);
        Assert.True(args.IsNew);
    }

    [Fact]
    public void Constructor_WithMinDoubleValue_Allowed()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.MinValue);
        var args = new TValueEventArgs { Value = tValue, IsNew = false };

        Assert.Equal(double.MinValue, args.Value.Value);
        Assert.False(args.IsNew);
    }

    [Fact]
    public void Constructor_WithEpsilonValue_Allowed()
    {
        var tValue = new TValue(DateTime.UtcNow.Ticks, double.Epsilon);
        var args = new TValueEventArgs { Value = tValue, IsNew = true };

        Assert.Equal(double.Epsilon, args.Value.Value);
        Assert.True(args.IsNew);
    }
}

public class TValuePublishedHandlerTests
{
    private class MockPublisher : ITValuePublisher
    {
#pragma warning disable CS0067 // Event is never used - intentional for delegate testing
        public event TValuePublishedHandler? Pub;
#pragma warning restore CS0067
    }

    [Fact]
    public void Delegate_CanBeAssigned()
    {
        TValuePublishedHandler handler = (_, in _) => { };

        Assert.NotNull(handler);
    }

    [Fact]
    public void Delegate_CanBeInvoked()
    {
        bool wasCalled = false;
        TValuePublishedHandler handler = (object? sender, in TValueEventArgs args) => wasCalled = true;

        var args = new TValueEventArgs { Value = new TValue(12345, 100.0), IsNew = true };
        handler(null, args);

        Assert.True(wasCalled);
    }

    [Fact]
    public void Delegate_ReceivesCorrectArguments()
    {
        object? receivedSender = null;
        TValueEventArgs receivedArgs = default;

        TValuePublishedHandler handler = (object? sender, in TValueEventArgs args) =>
        {
            receivedSender = sender;
            receivedArgs = args;
        };

        var publisher = new MockPublisher();
        var expectedArgs = new TValueEventArgs { Value = new TValue(12345, 100.0), IsNew = true };

        handler(publisher, expectedArgs);

        Assert.Equal(publisher, receivedSender);
        Assert.Equal(expectedArgs, receivedArgs);
    }

    [Fact]
    public void Delegate_CanBeNull()
    {
        TValuePublishedHandler? handler = null;

        var exception = Record.Exception(() => handler?.Invoke(null, new TValueEventArgs()));
        Assert.Null(exception);
    }
}

public class ITValuePublisherTests
{
    private class MockPublisher : ITValuePublisher
    {
        public event TValuePublishedHandler? Pub;

        public void RaiseEvent(TValue value, bool isNew = true)
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });
        }
    }

    [Fact]
    public void Interface_CanBeImplemented()
    {
        ITValuePublisher publisher = new MockPublisher();

        Assert.NotNull(publisher);
    }

    [Fact]
    public void Pub_Event_CanBeSubscribed()
    {
        var publisher = new MockPublisher();
        bool eventRaised = false;

        publisher.Pub += (object? sender, in TValueEventArgs args) => eventRaised = true;

        publisher.RaiseEvent(new TValue(12345, 100.0));

        Assert.True(eventRaised);
    }

    [Fact]
    public void Pub_Event_CanBeUnsubscribed()
    {
        var publisher = new MockPublisher();
        bool eventRaised = false;

        TValuePublishedHandler handler = (object? sender, in TValueEventArgs args) => eventRaised = true;
        publisher.Pub += handler;

        publisher.RaiseEvent(new TValue(12345, 100.0));
        Assert.True(eventRaised);

        eventRaised = false;
        publisher.Pub -= handler;

        publisher.RaiseEvent(new TValue(12346, 101.0));
        Assert.False(eventRaised);
    }

    [Fact]
    public void Pub_Event_MultipleSubscribers_AllReceiveEvent()
    {
        var publisher = new MockPublisher();
        bool event1Raised = false;
        bool event2Raised = false;

        publisher.Pub += (object? sender, in TValueEventArgs args) => event1Raised = true;
        publisher.Pub += (object? sender, in TValueEventArgs args) => event2Raised = true;

        publisher.RaiseEvent(new TValue(12345, 100.0));

        Assert.True(event1Raised);
        Assert.True(event2Raised);
    }

    [Fact]
    public void Pub_Event_NoSubscribers_DoesNotThrow()
    {
        var publisher = new MockPublisher();

        var exception = Record.Exception(() => publisher.RaiseEvent(new TValue(12345, 100.0)));
        Assert.Null(exception);
    }

    [Fact]
    public void Pub_Event_ReceivesCorrectSender()
    {
        var publisher = new MockPublisher();
        object? receivedSender = null;

        publisher.Pub += (object? sender, in TValueEventArgs args) => receivedSender = sender;

        publisher.RaiseEvent(new TValue(12345, 100.0));

        Assert.Equal(publisher, receivedSender);
    }

    [Fact]
    public void Pub_Event_ReceivesCorrectEventArgs()
    {
        var publisher = new MockPublisher();
        TValueEventArgs receivedArgs = default;

        publisher.Pub += (object? sender, in TValueEventArgs args) => receivedArgs = args;

        var expectedValue = new TValue(12345, 100.0);
        publisher.RaiseEvent(expectedValue, isNew: true);

        Assert.Equal(expectedValue, receivedArgs.Value);
        Assert.True(receivedArgs.IsNew);
    }

    [Fact]
    public void Pub_Event_IsNew_False_ReceivedCorrectly()
    {
        var publisher = new MockPublisher();
        TValueEventArgs receivedArgs = default;

        publisher.Pub += (object? sender, in TValueEventArgs args) => receivedArgs = args;

        var expectedValue = new TValue(12345, 100.0);
        publisher.RaiseEvent(expectedValue, isNew: false);

        Assert.Equal(expectedValue, receivedArgs.Value);
        Assert.False(receivedArgs.IsNew);
    }

    [Fact]
    public void Pub_Event_HandlerThrows_ExceptionPropagates()
    {
        var publisher = new MockPublisher();

        publisher.Pub += (object? sender, in TValueEventArgs args) =>
        {
            throw new InvalidOperationException("Test exception");
        };

        Assert.Throws<InvalidOperationException>(() =>
            publisher.RaiseEvent(new TValue(12345, 100.0)));
    }

    [Fact]
    public void Pub_Event_HandlerWithNaNValue_ReceivesNaN()
    {
        var publisher = new MockPublisher();
        double receivedValue = 0;

        publisher.Pub += (object? sender, in TValueEventArgs args) => receivedValue = args.Value.Value;

        publisher.RaiseEvent(new TValue(12345, double.NaN));

        Assert.True(double.IsNaN(receivedValue));
    }

    [Fact]
    public void Pub_Event_HandlerWithInfinityValue_ReceivesInfinity()
    {
        var publisher = new MockPublisher();
        double receivedValue = 0;

        publisher.Pub += (object? sender, in TValueEventArgs args) => receivedValue = args.Value.Value;

        publisher.RaiseEvent(new TValue(12345, double.PositiveInfinity));

        Assert.True(double.IsPositiveInfinity(receivedValue));
    }

    [Fact]
    public void Pub_Event_MultipleEvents_AllReceived()
    {
        var publisher = new MockPublisher();
        var receivedValues = new List<double>();

        publisher.Pub += (object? sender, in TValueEventArgs args) => receivedValues.Add(args.Value.Value);

        publisher.RaiseEvent(new TValue(12345, 100.0));
        publisher.RaiseEvent(new TValue(12346, 200.0));
        publisher.RaiseEvent(new TValue(12347, 300.0));

        Assert.Equal(3, receivedValues.Count);
        Assert.Equal(100.0, receivedValues[0]);
        Assert.Equal(200.0, receivedValues[1]);
        Assert.Equal(300.0, receivedValues[2]);
    }

    [Fact]
    public void Pub_Event_SubscribeUnsubscribeMultipleTimes_Works()
    {
        var publisher = new MockPublisher();
        int callCount = 0;

        TValuePublishedHandler handler = (object? sender, in TValueEventArgs args) => callCount++;

        // Subscribe multiple times
        publisher.Pub += handler;
        publisher.Pub += handler;

        publisher.RaiseEvent(new TValue(12345, 100.0));
        Assert.Equal(2, callCount); // Called twice

        callCount = 0;
        // Unsubscribe once
        publisher.Pub -= handler;

        publisher.RaiseEvent(new TValue(12346, 200.0));
        Assert.Equal(1, callCount); // Called once

        callCount = 0;
        // Unsubscribe remaining
        publisher.Pub -= handler;

        publisher.RaiseEvent(new TValue(12347, 300.0));
        Assert.Equal(0, callCount); // Not called
    }
}
