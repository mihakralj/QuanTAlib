
namespace QuanTAlib.Tests
{
    public class TValueTests
    {
        [Fact]
        public void Constructor_WithLongTime_SetsPropertiesCorrectly()
        {
            long time = DateTime.UtcNow.Ticks;
            double value = 123.45;

            var tValue = new TValue(time, value);

            Assert.Equal(time, tValue.Time);
            Assert.Equal(value, tValue.Value);
        }

        [Fact]
        public void Constructor_WithDateTime_SetsPropertiesCorrectly()
        {
            var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            double value = 123.45;

            var tValue = new TValue(dateTime, value);

            Assert.Equal(dateTime.Ticks, tValue.Time);
            Assert.Equal(value, tValue.Value);
        }

        [Fact]
        public void AsDateTime_ReturnsCorrectDateTime()
        {
            var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var tValue = new TValue(dt.Ticks, 100.0);

            Assert.Equal(dt, tValue.AsDateTime);
            Assert.Equal(DateTimeKind.Utc, tValue.AsDateTime.Kind);
        }

        [Fact]
        public void ToString_FormatsCorrectly()
        {
            var dt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var tValue = new TValue(dt.Ticks, 123.456);

            string result = tValue.ToString();

            Assert.Contains("2023-01-01", result);
            Assert.Contains("12:00:00", result);
            Assert.Contains("123.46", result);
        }

        [Fact]
        public void ImplicitConversion_ToDouble_ReturnsValue()
        {
            var tValue = new TValue(DateTime.UtcNow.Ticks, 42.0);
            
            double val = tValue;
            
            Assert.Equal(42.0, val);
        }

        [Fact]
        public void ImplicitConversion_ToDateTime_ReturnsCorrectDateTime()
        {
            var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var tValue = new TValue(dateTime.Ticks, 100.0);

            DateTime result = tValue;

            Assert.Equal(dateTime, result);
            Assert.Equal(DateTimeKind.Utc, result.Kind);
        }

        [Fact]
        public void Equals_TValue_SameValues_ReturnsTrue()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12345, 100.0);

            Assert.True(tv1.Equals(tv2));
        }

        [Fact]
        public void Equals_TValue_DifferentTime_ReturnsFalse()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12346, 100.0);

            Assert.False(tv1.Equals(tv2));
        }

        [Fact]
        public void Equals_TValue_DifferentValue_ReturnsFalse()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12345, 101.0);

            Assert.False(tv1.Equals(tv2));
        }

        [Fact]
        public void Equals_Object_SameTValue_ReturnsTrue()
        {
            var tv1 = new TValue(12345, 100.0);
            object tv2 = new TValue(12345, 100.0);

            Assert.True(tv1.Equals(tv2));
        }

        [Fact]
        public void Equals_Object_DifferentType_ReturnsFalse()
        {
            var tv = new TValue(12345, 100.0);
            object other = "not a TValue";

            Assert.False(tv.Equals(other));
        }

        [Fact]
        public void Equals_Object_Null_ReturnsFalse()
        {
            var tv = new TValue(12345, 100.0);

            Assert.False(tv.Equals(null));
        }

        [Fact]
        public void GetHashCode_SameValues_ReturnsSameHashCode()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12345, 100.0);

            Assert.Equal(tv1.GetHashCode(), tv2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentValues_ReturnsDifferentHashCode()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12346, 100.0);

            Assert.NotEqual(tv1.GetHashCode(), tv2.GetHashCode());
        }

        [Fact]
        public void EqualityOperator_SameValues_ReturnsTrue()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12345, 100.0);

            Assert.True(tv1 == tv2);
        }

        [Fact]
        public void EqualityOperator_DifferentValues_ReturnsFalse()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12346, 100.0);

            Assert.False(tv1 == tv2);
        }

        [Fact]
        public void InequalityOperator_SameValues_ReturnsFalse()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12345, 100.0);

            Assert.False(tv1 != tv2);
        }

        [Fact]
        public void InequalityOperator_DifferentValues_ReturnsTrue()
        {
            var tv1 = new TValue(12345, 100.0);
            var tv2 = new TValue(12346, 100.0);

            Assert.True(tv1 != tv2);
        }
    }
}
