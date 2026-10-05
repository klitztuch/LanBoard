namespace LanBoard.Application.Stats;

public interface IPartyStatsService
{
    Task<PartyStats> GetStatsAsync(Guid partyId, CancellationToken ct = default);
}
