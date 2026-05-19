using System;

namespace BlackBoxIdentification.MathModel;

public static class ChebyshevBasis
{
    public static double T(int n, double z)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        z = Math.Clamp(z, -1.0, 1.0);
        if (n == 0) return 1.0;
        if (n == 1) return z;
        double t0 = 1.0, t1 = z;
        for (int k = 2; k <= n; k++)
        {
            double t2 = 2.0 * z * t1 - t0;
            t0 = t1; t1 = t2;
        }
        return t1;
    }
    public static double MapToMinusOneOne(double s, double memoryLength) => memoryLength <= 0 ? 0 : 2.0 * s / memoryLength - 1.0;
}
