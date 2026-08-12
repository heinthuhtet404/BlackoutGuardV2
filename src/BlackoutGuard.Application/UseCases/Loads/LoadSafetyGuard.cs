using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Loads;

internal sealed class LoadSafetyGuard
{
    private readonly ILoadRepository _loadRepo;
    private readonly IFacilityRepository _facilityRepo;

    public LoadSafetyGuard(ILoadRepository loadRepo, IFacilityRepository facilityRepo)
    {
        _loadRepo = loadRepo;
        _facilityRepo = facilityRepo;
    }

    public async Task<LoadDto?> FindRelayConflictAsync(Guid facilityId, int relayAddress, Guid? excludeLoadId, CancellationToken ct)
    {
        return await _loadRepo.GetByRelayAddressAsync(facilityId, relayAddress, excludeLoadId, ct);
    }

    public async Task<CapacityEvaluation> EvaluateCapacityAsync(Guid facilityId, double newRatingKw, Guid? excludeLoadId, CancellationToken ct)
    {
        var facility = await _facilityRepo.GetByIdAsync(facilityId, ct);
        if (facility is null)
            return CapacityEvaluation.FacilityNotFound();

        var existingP1 = await _loadRepo.GetP1LoadsAsync(facilityId, excludeLoadId, ct);
        var totalP1Kw = existingP1.Sum(l => l.PowerRatingKw) + newRatingKw;
        var deficit = totalP1Kw - facility.GeneratorCapacityKW;

        return new CapacityEvaluation(facility, totalP1Kw, deficit);
    }
}

internal sealed class CapacityEvaluation
{
    public FacilityDto? Facility { get; }
    public double TotalP1Kw { get; }
    public double Deficit { get; }

    public CapacityEvaluation(FacilityDto? facility, double totalP1Kw, double deficit)
    {
        Facility = facility;
        TotalP1Kw = totalP1Kw;
        Deficit = deficit;
    }

    public static CapacityEvaluation FacilityNotFound() => new(null, 0, 0);
}
