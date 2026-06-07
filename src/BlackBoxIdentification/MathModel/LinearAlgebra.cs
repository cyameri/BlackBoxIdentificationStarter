using System;
using MathNet.Numerics.LinearAlgebra;

namespace BlackBoxIdentification.MathModel;

public static class LinearAlgebra
{
    public static double[] SolveLeastSquares(double[,] a, double[] b, double regularization = 1e-8)
    {
        return SolveRegularizedLeastSquares(a, b, regularization);
    }

    public static double[] SolveSquareSystem(double[,] a, double[] b, double regularization = 1e-6)
    {
        // Для коллокации матрица часто бывает плохо обусловлена. Поэтому вместо прямого
        // решения Ax=b используем стабилизированную форму с малой регуляризацией.
        return SolveRegularizedLeastSquares(a, b, regularization);
    }

    public static double[] SolveRegularizedLeastSquares(double[,] a, double[] b, double regularization)
    {
        if (a.GetLength(0) != b.Length)
            throw new ArgumentException("Число строк матрицы должно совпадать с длиной правой части.");

        MathValidation.EnsureFinite(a, "матрица системы");
        MathValidation.EnsureFinite(b, "правая часть системы");

        int rows = a.GetLength(0);
        int cols = a.GetLength(1);

        if (rows == 0 || cols == 0)
            throw new InvalidOperationException("Матрица системы пуста.");

        var original = Matrix<double>.Build.DenseOfArray(a);
        var target = Vector<double>.Build.DenseOfArray(b);

        // Масштабирование столбцов уменьшает риск численной неустойчивости,
        // особенно для квадратичных признаков gamma_ij.
        var scale = new double[cols];
        var matrix = original.Clone();

        for (int j = 0; j < cols; j++)
        {
            double norm = matrix.Column(j).L2Norm();
            if (norm < 1e-12 || double.IsNaN(norm) || double.IsInfinity(norm))
                norm = 1.0;

            scale[j] = norm;
            for (int i = 0; i < rows; i++)
                matrix[i, j] /= norm;
        }

        double lambda = BuildRegularizationValue(matrix, regularization);
        var lhs = matrix.TransposeThisAndMultiply(matrix);
        var rhs = matrix.TransposeThisAndMultiply(target);

        for (int i = 0; i < cols; i++)
            lhs[i, i] += lambda;

        Vector<double> scaledSolution;

        try
        {
            scaledSolution = lhs.Cholesky().Solve(rhs);
        }
        catch
        {
            try
            {
                scaledSolution = lhs.QR().Solve(rhs);
            }
            catch
            {
                scaledSolution = lhs.Svd(true).Solve(rhs);
            }
        }

        var result = new double[cols];
        for (int j = 0; j < cols; j++)
            result[j] = scaledSolution[j] / scale[j];

        MathValidation.EnsureFinite(result, "вектор решения");
        return result;
    }

    private static double BuildRegularizationValue(Matrix<double> matrix, double regularization)
    {
        double trace = 0.0;
        int n = Math.Min(matrix.RowCount, matrix.ColumnCount);

        var gram = matrix.TransposeThisAndMultiply(matrix);
        for (int i = 0; i < gram.RowCount; i++)
            trace += gram[i, i];

        double meanDiagonal = gram.RowCount > 0 ? trace / gram.RowCount : 1.0;
        if (meanDiagonal <= 0 || double.IsNaN(meanDiagonal) || double.IsInfinity(meanDiagonal))
            meanDiagonal = 1.0;

        return Math.Max(1e-12, regularization * meanDiagonal);
    }
}
