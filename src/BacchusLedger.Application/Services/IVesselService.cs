using System.Text.Json;
using BacchusLedger.Application.Contracts.Common;
using BacchusLedger.Application.Contracts.Vessels;

namespace BacchusLedger.Application.Services;

/// Tanks and qvevri: calibration (dip ↔ litres), contents and master data.
public interface IVesselService
{
    Task<IReadOnlyList<VesselDto>> ListAsync();

    /// Vessel by code or cellar label, with its dip chart, drawing profile and the batches that used it.
    Task<ServiceResult<VesselDetailResponse>> GetAsync(string code, string? winery);

    /// Pass dip (cm from the rim) to get litres, or litres to get the dip; the result is a DipToLitresResponse or LitresToDipResponse.
    Task<ServiceResult<object>> ConvertAsync(string code, double? dip, double? litres, string? winery);

    Task<ServiceResult<VesselCreatedResponse>> CreateAsync(JsonElement body);

    /// Cellar label and optional qvevri measurements (vessels are master data, so they can be edited).
    Task<ServiceResult<OkResponse>> UpdateDetailsAsync(string code, string? winery, JsonElement body);
}
