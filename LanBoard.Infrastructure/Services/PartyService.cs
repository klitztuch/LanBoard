using LanBoard.Application.Interfaces;
using LanBoard.Application.Parties;

namespace LanBoard.Infrastructure.Services;

public class PartyService(ILanPartyRepository parties) : IPartyService
{
    public async Task<IReadOnlyList<PartySummary>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await parties.GetAllAsync(ct);
        return all
            .Select(p => new PartySummary(p.Id, p.Name, p.Date, p.Location.Name, p.IsActive))
            .ToList();
    }
}
