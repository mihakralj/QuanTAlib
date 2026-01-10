using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class NotchValidationTests
{
    [Fact]
    public void Verify_Manual_Calc_Period4_Q0_5()
    {
        // For Period=4, Q=0.5:
        // omega = pi/2
        // alpha = sin(pi/2)/(2*0.5) = 1
        // a0 = 2
        // b0 = 0.5, b1 = 0, b2 = 0.5
        // a1 = 0, a2 = 0
        // Equation: y[n] = 0.5*x[n] + 0.5*x[n-2]

        var notch = new Notch(period: 4, q: 0.5);

        // Input: 1, 0, -1, 0 (Sine at period 4)
        var input = new TValue[]
        {
            new(DateTime.UtcNow, 1),
            new(DateTime.UtcNow, 0),
            new(DateTime.UtcNow, -1),
            new(DateTime.UtcNow, 0),
            new(DateTime.UtcNow, 1),
            new(DateTime.UtcNow, 0),
        };

        // Expected Output:
        // y[0] = 0.5(1) = 0.5
        // y[1] = 0.5(0) = 0
        // y[2] = 0.5(-1) + 0.5(1) = 0
        // y[3] = 0.5(0) + 0.5(0) = 0
        // y[4] = 0.5(1) + 0.5(-1) = 0
        // y[5] = 0

        double[] expected = { 0.5, 0.0, 0.0, 0.0, 0.0, 0.0 };

        for(int i=0; i<input.Length; i++)
        {
            double val = notch.Update(input[i]).Value;
            Assert.Equal(expected[i], val, precision: 9);
        }
    }
}