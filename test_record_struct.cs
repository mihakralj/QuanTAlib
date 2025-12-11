using System;

namespace QuanTAlib;

public class RecordStructTest
{
    private record struct State(double Val, bool Flag);

    public static void Run()
    {
        var s1 = new State(1.0, true);
        var s2 = new State(1.0, true);
        var s3 = new State(2.0, false);

        Console.WriteLine($"s1 == s2: {s1 == s2}"); // Should be True
        Console.WriteLine($"s1 == s3: {s1 == s3}"); // Should be False
        Console.WriteLine($"s1 equals s2: {s1.Equals(s2)}"); // Should be True
        
        // Verify mutability
        s1.Val = 3.0;
        Console.WriteLine($"s1 modified: {s1.Val}");
    }
}
