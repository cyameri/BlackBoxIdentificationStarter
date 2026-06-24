namespace BlackBoxIdentification.Core;

public sealed class IdentificationParameters
{
    public IdentificationMethod Method { get; init; } = IdentificationMethod.Collocation;

    /// <summary>
    /// Количество базисных функций Чебышева для ядра первого порядка K1(s).
    /// В обозначениях Maple-программы руководителя: m.
    /// </summary>
    public int FirstKernelBasisCount { get; init; } = 3;

    /// <summary>
    /// Количество базисных функций для первого аргумента ядра K2(s1, s2).
    /// </summary>
    public int SecondKernelBasisCountS1 { get; init; } = 3;

    /// <summary>
    /// Количество базисных функций для второго аргумента ядра K2(s1, s2).
    /// </summary>
    public int SecondKernelBasisCountS2 { get; init; } = 3;

    /// <summary>
    /// Число точек, используемых методом наименьших квадратов.
    /// Для коллокации число узлов вычисляется автоматически.
    /// </summary>
    public int LeastSquaresPointCount { get; init; } = 40;

    /// <summary>
    /// Необязательная стандартизация амплитуд x и y.
    /// В исходной Maple-схеме она отсутствует, поэтому по умолчанию выключена.
    /// </summary>
    public bool NormalizeSignals { get; init; }

    /// <summary>
    /// Число интервалов составной формулы Симпсона при вычислении интегралов.
    /// Значение автоматически приводится к четному числу.
    /// </summary>
    public int QuadratureIntervals { get; init; } = 160;

    public int ParameterCount =>
        FirstKernelBasisCount + SecondKernelBasisCountS1 * SecondKernelBasisCountS2;

    public int CollocationNodeCount => ParameterCount;
}
