using MathNet.Numerics.LinearAlgebra;

namespace BlackBoxIdentification.MathModel;

public static class LinearAlgebra
{
    public static double[] SolveLeastSquares(double[,] a, double[] b)
    {
        var matrix = Matrix<double>.Build.DenseOfArray(a);
        var vector = Vector<double>.Build.DenseOfArray(b);
        return matrix.QR().Solve(vector).ToArray();
    }
}
