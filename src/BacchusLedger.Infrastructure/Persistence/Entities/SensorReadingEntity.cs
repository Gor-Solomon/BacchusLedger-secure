namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table sensor_readings: A reading from the sensor stub.
public sealed class SensorReadingEntity
{
    public long Id { get; set; }
    public long? VesselId { get; set; }
    public double? DipCm { get; set; }
    public double? Litres { get; set; }
    public string? CreatedAt { get; set; }
}
