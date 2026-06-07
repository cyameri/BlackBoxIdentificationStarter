using System;

namespace BlackBoxIdentification.MathModel;

public static class ChebyshevBasis
{
    /// <summary>
    /// Chebyshev polynomial of the first kind T_n(z), where z belongs to [-1; 1].
    /// </summary>
    public static double T(int n, double z)
    {
        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n));

        z = Math.Clamp(z, -1.0, 1.0);

        if (n == 0)
            return 1.0;

        if (n == 1)
            return z;

        double t0 = 1.0;
        double t1 = z;

        for (int k = 2; k <= n; k++)
        {
            double t2 = 2.0 * z * t1 - t0;
            t0 = t1;
            t1 = t2;
        }

        return t1;
    }

    /// <summary>
    /// Maps s from [0; intervalLength] to z from [-1; 1].
    /// </summary>
    public static double MapToMinusOneOne(double s, double intervalLength)
    {
        if (intervalLength <= 0)
            return 0.0;

        return 2.0 * s / intervalLength - 1.0;
    }
}
