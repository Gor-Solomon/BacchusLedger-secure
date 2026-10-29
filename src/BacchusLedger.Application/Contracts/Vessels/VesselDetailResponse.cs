namespace BacchusLedger.Application.Contracts.Vessels;

/// Profile: inner shape for drawing as [z_cm, radius_cm] pairs.
public sealed record VesselDetailResponse(VesselDto Vessel, IReadOnlyList<DipChartRowDto> Chart, IReadOnlyList<double[]>? Profile, IReadOnlyList<VesselBatchDto> Batches);
