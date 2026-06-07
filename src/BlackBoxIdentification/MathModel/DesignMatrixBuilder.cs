using System;
using System.Collections.Generic;
using System.Linq;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class DesignMatrixBuilder
{
    public static int GetParameterCount(IdentificationParameters p)
    {
        // H0 + A_i + C_ij
        return 1 + p.LinearOrder + p.QuadraticOrder * p.QuadraticOrder;
    }

    public static int[] SelectLeastSquaresIndexes(SignalData data, IdentificationParameters p)
    {
        int parameterCount = GetParameterCount(p);

        if (data.Count <= parameterCount)
            throw new InvalidOperationException($"Недостаточно точек для МНК. Нужно больше {parameterCount} точек сигнала.");

        // Для МНК полезно использовать больше уравнений, чем неизвестных коэффициентов.
        // После добавления H0 начальные точки тоже полезны: они помогают восстановить
        // постоянную составляющую выхода, когда интегральные слагаемые еще малы.
        int requested = Math.Max(parameterCount * 3, p.NodeCount);
        int count = Math.Min(requested, data.Count);

        return SelectEvenly(0, data.Count - 1, count);
    }

    public static int[] SelectCollocationIndexes(SignalData data, IdentificationParameters p)
    {
        int parameterCount = GetParameterCount(p);
        int start = GetFirstUsableIndex(data, p);
        int available = data.Count - start;

        if (available + 1 < parameterCount)
            throw new InvalidOperationException($"Недостаточно точек для метода коллокации. Нужно не менее {parameterCount} рабочих узлов.");

        // В идеальной квадратной постановке число узлов равно числу неизвестных.
        // На реальных дискретных данных такая система часто становится вырожденной,
        // поэтому допускаем использовать N узлов и решать стабилизированную систему.
        int requested = Math.Max(parameterCount, p.NodeCount);
        int count = Math.Min(requested, data.Count);

        int candidateCount = Math.Min(available, Math.Max(count * 3, parameterCount * 5));
        int[] candidates = SelectEvenly(start, data.Count - 1, candidateCount);

        // Отбрасываем узлы, где интегральная часть строки почти нулевая.
        var rows = candidates
            .Select(index => new
            {
                Index = index,
                Norm = RowNorm(BuildFeatureVector(data, p, index), skipFirst: true)
            })
            .Where(x => MathValidation.IsFinite(x.Norm) && x.Norm > 1e-10)
            .OrderBy(x => x.Index)
            .ToList();

        if (rows.Count + 1 < parameterCount)
            throw new InvalidOperationException("Не удалось выбрать достаточное число информативных узлов коллокации. Попробуйте уменьшить m1/m2 или длину памяти L.");

        // Добавляем начальную точку для устойчивого определения H0, а остальные
        // узлы берем равномерно по информативной части интервала.
        var selected = new List<int> { 0 };
        int tailCount = Math.Min(count - 1, rows.Count);

        for (int k = 0; k < tailCount; k++)
        {
            int rowIndex = (int)Math.Round((rows.Count - 1) * k / (double)Math.Max(1, tailCount - 1));
            selected.Add(rows[rowIndex].Index);
        }

        return selected.Distinct().OrderBy(x => x).ToArray();
    }

    public static (double[,] Matrix, double[] Target, int[] SampleIndexes) Build(SignalData data, IdentificationParameters p, int[] sampleIndexes)
    {
        if (data.Count < 2)
            throw new InvalidOperationException("Для идентификации требуется не менее двух точек сигнала.");

        if (p.LinearOrder <= 0 || p.QuadraticOrder <= 0)
            throw new InvalidOperationException("Порядки аппроксимации должны быть положительными.");

        int parameterCount = GetParameterCount(p);
        var matrix = new double[sampleIndexes.Length, parameterCount];
        var target = new double[sampleIndexes.Length];

        for (int row = 0; row < sampleIndexes.Length; row++)
        {
            int sampleIndex = sampleIndexes[row];
            double[] features = BuildFeatureVector(data, p, sampleIndex);

            for (int column = 0; column < parameterCount; column++)
                matrix[row, column] = features[column];

            target[row] = data.Points[sampleIndex].Output;
        }

        MathValidation.EnsureFinite(matrix, "матрица признаков");
        MathValidation.EnsureFinite(target, "вектор выходного сигнала");

        return (matrix, target, sampleIndexes);
    }

    public static double[] BuildFeatureVector(SignalData data, IdentificationParameters p, int sampleIndex)
    {
        int parameterCount = GetParameterCount(p);
        var features = new double[parameterCount];

        // Первый коэффициент соответствует нулевому члену ряда Вольтерра H0.
        // Он нужен для учета постоянной составляющей выходного сигнала и особенно
        // важен в начале интервала, где интегральные слагаемые еще малы.
        features[0] = 1.0;

        int betaOrder = Math.Max(p.LinearOrder, p.QuadraticOrder);
        double[] beta = ComputeBetaValues(data, p, sampleIndex, betaOrder);

        int column = 1;
        for (int i = 0; i < p.LinearOrder; i++)
            features[column++] = beta[i];

        for (int i = 0; i < p.QuadraticOrder; i++)
        {
            for (int j = 0; j < p.QuadraticOrder; j++)
            {
                // Для модели с произведением x(t-s1)x(t-s2) двойной интеграл
                // с разделяемым базисом T_i(s1)T_j(s2) записывается как произведение
                // соответствующих одномерных интегральных коэффициентов beta_i beta_j.
                features[column++] = beta[i] * beta[j];
            }
        }

        return features;
    }

    private static double[] ComputeBetaValues(SignalData data, IdentificationParameters p, int sampleIndex, int order)
    {
        var time = data.Time.ToArray();
        var input = data.Input.ToArray();
        var beta = new double[order];

        double step = EstimateStep(time);
        double memoryInterval = Math.Max(step, p.MemoryLength * step);
        double currentTime = time[sampleIndex] - time[0];
        double upper = Math.Min(currentTime, memoryInterval);

        if (upper <= 0)
            return beta;

        int intervals = Math.Max(1, (int)Math.Ceiling(upper / step));

        double previousS = 0.0;
        double[] previousValues = BasisInputProducts(time, input, order, currentTime, previousS, memoryInterval);

        for (int q = 1; q <= intervals; q++)
        {
            double s = Math.Min(q * step, upper);
            double[] currentValues = BasisInputProducts(time, input, order, currentTime, s, memoryInterval);
            double h = s - previousS;

            for (int i = 0; i < order; i++)
                beta[i] += 0.5 * h * (previousValues[i] + currentValues[i]);

            previousS = s;
            previousValues = currentValues;
        }

        return beta;
    }

    private static double[] BasisInputProducts(double[] time, double[] input, int order, double currentTime, double s, double memoryInterval)
    {
        var values = new double[order];
        double z = ChebyshevBasis.MapToMinusOneOne(s, memoryInterval);
        double shiftedInput = InterpolateInput(time, input, time[0] + currentTime - s);

        for (int i = 0; i < order; i++)
            values[i] = ChebyshevBasis.T(i, z) * shiftedInput;

        return values;
    }

    private static double InterpolateInput(double[] time, double[] input, double targetTime)
    {
        if (targetTime <= time[0])
            return input[0];

        if (targetTime >= time[^1])
            return input[^1];

        int right = Array.BinarySearch(time, targetTime);
        if (right >= 0)
            return input[right];

        right = ~right;
        int left = right - 1;

        double denominator = time[right] - time[left];
        if (denominator <= 0)
            return input[left];

        double alpha = (targetTime - time[left]) / denominator;
        return input[left] + alpha * (input[right] - input[left]);
    }

    public static double EstimateStep(double[] time)
    {
        if (time.Length < 2)
            return 1.0;

        var diffs = new List<double>();
        for (int i = 1; i < time.Length; i++)
        {
            double diff = time[i] - time[i - 1];
            if (diff > 0)
                diffs.Add(diff);
        }

        if (diffs.Count == 0)
            return 1.0;

        diffs.Sort();
        return diffs[diffs.Count / 2];
    }

    private static int GetFirstUsableIndex(SignalData data, IdentificationParameters p)
    {
        if (data.Count < 2)
            return 0;

        // Не начинаем коллокацию/МНК с первых точек: там интегралы малые,
        // строки системы часто почти нулевые, что приводит к вырождению.
        int byMemory = Math.Max(2, p.MemoryLength);
        int byPart = Math.Max(2, data.Count / 10);
        int start = Math.Min(data.Count - 1, Math.Max(byMemory, byPart));

        // Если данных мало, не отрезаем слишком много.
        if (data.Count - start < GetParameterCount(p) + 1)
            start = Math.Max(1, data.Count / 4);

        return Math.Clamp(start, 1, data.Count - 1);
    }

    private static int[] SelectEvenly(int first, int last, int count)
    {
        if (count <= 0)
            return Array.Empty<int>();

        if (first > last)
            return Array.Empty<int>();

        if (count == 1)
            return new[] { last };

        var selected = new List<int>();
        for (int k = 0; k < count; k++)
        {
            int index = (int)Math.Round(first + (last - first) * k / (double)(count - 1));
            index = Math.Clamp(index, first, last);

            if (selected.Count == 0 || selected[^1] != index)
                selected.Add(index);
        }

        for (int index = first; selected.Count < count && index <= last; index++)
        {
            if (!selected.Contains(index))
                selected.Add(index);
        }

        selected.Sort();
        return selected.ToArray();
    }

    private static double RowNorm(double[] row, bool skipFirst = false)
    {
        double sum = 0.0;
        int start = skipFirst ? 1 : 0;

        for (int i = start; i < row.Length; i++)
            sum += row[i] * row[i];

        return Math.Sqrt(sum);
    }
}
