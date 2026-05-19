using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public interface IIdentifier
{
    IdentificationResult Identify(SignalData data, IdentificationParameters parameters);
}
