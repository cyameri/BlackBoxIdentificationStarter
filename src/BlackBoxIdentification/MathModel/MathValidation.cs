using System;
using System.Linq;

namespace BlackBoxIdentification.MathModel;

public static class MathValidation
{
    public static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public static bool IsFinite(double[] values)
    {
        return values.All(IsFinite);
    }

    public static bool IsFinite(double[,] values)
    {
        foreach (double value in values)
        {
            if (!IsFinite(value))
                return false;
        }

        return true;
    }

    public static void EnsureFinite(double[] values, string objectName)
    {
        if (!IsFinite(values))
            throw new InvalidOperationException($"В ходе вычислений получены некорректные значения в объекте: {objectName}.");
    }

    public static void EnsureFinite(double[,] values, string objectName)
    {
        if (!IsFinite(values))
            throw new InvalidOperationException($"В ходе вычислений получены некорректные значения в объекте: {objectName}.");
    }
}
