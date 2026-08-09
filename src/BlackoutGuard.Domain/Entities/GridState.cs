namespace BlackoutGuard.Domain.Entities;

public class GridState
{
    public double Frequency { get; set; }
    public double Voltage { get; set; }
    public double TotalLoad { get; set; }
    public bool GeneratorOn { get; set; }
    public bool IsBreakerTripped { get; set; }
    public DateTime TimestampUtc { get; set; }
}
