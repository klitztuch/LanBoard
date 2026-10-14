namespace LanBoard.Application.Parties;

public interface IPartyService
{
    Task<IReadOnlyList<PartySummary>> GetAllAsync(CancellationToken ct = default);
}
