using System;
using System.Collections.Generic;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.IO;

public static class DemoDataGenerator
{
    public static SignalData Generate(int count = 250)
    {
        var points = new List<SignalPoint>(count);
        for (int n = 0; n < count; n++)
        {
            double t = n / 20.0;
            double x = Math.Sin(t) + 0.35 * Math.Sin(2.7 * t);
            double y = 0.7 * Math.Sin(t - 0.4) + 0.18 * Math.Sin(2.7 * t - 0.8) + 0.12 * x * x + 0.04 * Math.Cos(0.5 * t);
            points.Add(new SignalPoint(t, x, y));
        }
        return new SignalData(points);
    }
}
