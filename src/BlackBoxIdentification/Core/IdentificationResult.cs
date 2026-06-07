namespace BlackBoxIdentification.Core;

public sealed class IdentificationResult
{
    /// <summary>
    /// Constant term H0 of the truncated Volterra model.
    /// </summary>
    public required double ConstantCoefficient { get; init; }

    public required double[] LinearCoefficients { get; init; }
    public required double[,] QuadraticCoefficients { get; init; }
    public required double[] ModelOutput { get; init; }
    public required double[] Residual { get; init; }
    public required double Rmse { get; init; }
    public required double RelativeErrorPercent { get; init; }
    public required double MaxAbsoluteError { get; init; }
}
