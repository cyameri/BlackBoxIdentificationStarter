using System;
using MathNet.Numerics.LinearAlgebra;

namespace BlackBoxIdentification.MathModel;

public static class LinearAlgebra
{
    public static double[] SolveSquareSystem(double[,] coefficients, double[] rightPart)
    {
        if (coefficients.GetLength(0) != coefficients.GetLength(1))
            throw new ArgumentException("Коллокационная матрица должна быть квадратной.");

        if (coefficients.GetLength(0) != rightPart.Length)
            throw new ArgumentException("Размер правой части не соответствует матрице.");

        MathValidation.EnsureFinite(coefficients, "коллокационная матрица");
        MathValidation.EnsureFinite(rightPart, "правая часть коллокационной системы");

        var original = Matrix<double>.Build.DenseOfArray(coefficients);
        var target = Vector<double>.Build.DenseOfArray(rightPart);
        var (scaledMatrix, columnScales) = ScaleColumns(original);

        // Матрица классической коллокационной системы для ядра второго порядка
        // может быть вырожденной: признаки beta_i*beta_j и beta_j*beta_i совпадают.
        // Поэтому обычный вызов SVD.Solve() способен делить на нулевые сингулярные
        // числа и возвращать NaN. Здесь явно строится псевдообратное решение
        // Мура–Пенроуза с отсечением малых сингулярных чисел. Оно выбирает
        // конечное решение минимальной евклидовой нормы среди всех допустимых.
        double[] scaledSolution;
        try
        {
            scaledSolution = SolveByTruncatedSvd(scaledMatrix, target);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Не удалось решить коллокационную систему. Попробуйте уменьшить число базисных функций.",
                exception);
        }

        return RestoreColumnScale(scaledSolution, columnScales);
    }

    public static double[] SolveLeastSquares(
        double[,] coefficients,
        double[] rightPart,
        double regularization = 1e-8)
    {
        if (coefficients.GetLength(0) != rightPart.Length)
            throw new ArgumentException("Число строк матрицы должно совпадать с длиной правой части.");

        MathValidation.EnsureFinite(coefficients, "матрица МНК");
        MathValidation.EnsureFinite(rightPart, "правая часть МНК");

        var original = Matrix<double>.Build.DenseOfArray(coefficients);
        var target = Vector<double>.Build.DenseOfArray(rightPart);
        var (scaledMatrix, columnScales) = ScaleColumns(original);

        Matrix<double> left = scaledMatrix.TransposeThisAndMultiply(scaledMatrix);
        Vector<double> right = scaledMatrix.TransposeThisAndMultiply(target);

        double lambda = BuildRegularizationValue(left, regularization);
        for (int i = 0; i < left.RowCount; i++)
            left[i, i] += lambda;

        Vector<double> scaledSolution;
        try
        {
            scaledSolution = left.Cholesky().Solve(right);
        }
        catch
        {
            try
            {
                scaledSolution = left.QR().Solve(right);
            }
            catch
            {
                scaledSolution = left.Svd(true).Solve(right);
            }
        }

        return RestoreColumnScale(scaledSolution.ToArray(), columnScales);
    }

    private static double[] SolveByTruncatedSvd(Matrix<double> matrix, Vector<double> target)
    {
        var svd = matrix.Svd(true);
        Vector<double> singularValues = svd.S;

        if (singularValues.Count == 0)
            throw new InvalidOperationException("Коллокационная матрица не содержит сингулярных чисел.");

        double maximumSingularValue = 0.0;
        for (int i = 0; i < singularValues.Count; i++)
            maximumSingularValue = Math.Max(maximumSingularValue, Math.Abs(singularValues[i]));

        if (!MathValidation.IsFinite(maximumSingularValue) || maximumSingularValue <= 0.0)
            throw new InvalidOperationException("Коллокационная матрица имеет нулевой ранг.");

        double tolerance = Math.Max(matrix.RowCount, matrix.ColumnCount)
            * maximumSingularValue
            * 1e-12;

        var weightedCoordinates = new double[singularValues.Count];
        int numericalRank = 0;

        // U^T * b с последующим делением только на надежные сингулярные числа.
        for (int singularIndex = 0; singularIndex < singularValues.Count; singularIndex++)
        {
            double singularValue = singularValues[singularIndex];
            double projection = 0.0;

            for (int row = 0; row < matrix.RowCount; row++)
                projection += svd.U[row, singularIndex] * target[row];

            if (singularValue > tolerance)
            {
                weightedCoordinates[singularIndex] = projection / singularValue;
                numericalRank++;
            }
            else
            {
                weightedCoordinates[singularIndex] = 0.0;
            }
        }

        if (numericalRank == 0)
            throw new InvalidOperationException("Коллокационная матрица имеет нулевой численный ранг.");

        // x = V * Sigma^+ * U^T * b, при этом V = (VT)^T.
        var solution = new double[matrix.ColumnCount];
        for (int column = 0; column < matrix.ColumnCount; column++)
        {
            double value = 0.0;
            for (int singularIndex = 0; singularIndex < singularValues.Count; singularIndex++)
                value += svd.VT[singularIndex, column] * weightedCoordinates[singularIndex];

            solution[column] = value;
        }

        MathValidation.EnsureFinite(solution, "вектор коэффициентов после псевдообращения");
        return solution;
    }

    private static (Matrix<double> Matrix, double[] Scales) ScaleColumns(Matrix<double> source)
    {
        Matrix<double> scaled = source.Clone();
        var scales = new double[source.ColumnCount];

        for (int column = 0; column < source.ColumnCount; column++)
        {
            double norm = source.Column(column).L2Norm();
            if (!MathValidation.IsFinite(norm) || norm < 1e-14)
                norm = 1.0;

            scales[column] = norm;

            for (int row = 0; row < source.RowCount; row++)
                scaled[row, column] /= norm;
        }

        return (scaled, scales);
    }

    private static double[] RestoreColumnScale(double[] scaledSolution, double[] scales)
    {
        if (scaledSolution.Length != scales.Length)
            throw new InvalidOperationException("Размер вектора решения не соответствует числу столбцов матрицы.");

        var result = new double[scaledSolution.Length];

        for (int i = 0; i < result.Length; i++)
            result[i] = scaledSolution[i] / scales[i];

        MathValidation.EnsureFinite(result, "вектор коэффициентов");
        return result;
    }

    private static double BuildRegularizationValue(Matrix<double> gramMatrix, double factor)
    {
        double trace = 0.0;
        for (int i = 0; i < gramMatrix.RowCount; i++)
            trace += gramMatrix[i, i];

        double meanDiagonal = gramMatrix.RowCount > 0
            ? trace / gramMatrix.RowCount
            : 1.0;

        if (!MathValidation.IsFinite(meanDiagonal) || meanDiagonal <= 0.0)
            meanDiagonal = 1.0;

        return Math.Max(1e-14, factor * meanDiagonal);
    }
}
