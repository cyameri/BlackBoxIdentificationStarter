using System.Collections.Generic;
using System.Linq;

namespace BlackBoxIdentification.Core;

public sealed class SignalData
{
    public SignalData(IReadOnlyList<SignalPoint> points) => Points = points;
    public IReadOnlyList<SignalPoint> Points { get; }
    public int Count => Points.Count;
    public IEnumerable<double> Time => Points.Select(p => p.Time);
    public IEnumerable<double> Input => Points.Select(p => p.Input);
    public IEnumerable<double> Output => Points.Select(p => p.Output);
}
