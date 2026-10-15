using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Tournaments;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class TournamentService(
    ITournamentRepository tournaments,
    ITournamentParticipantRepository participants,
    ITournamentMatchRepository matches,
    IPermissionService permissions) : ITournamentService
{
    private const int MinParticipants = 2;

    public Task<IReadOnlyList<Tournament>> GetByPartyAsync(Guid partyId, CancellationToken ct = default)
        => tournaments.GetByPartyAsync(partyId, ct);

    public async Task<Tournament> CreateAsync(Guid partyId, string name, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.TournamentManage, ct);

        var tournament = new Tournament { Id = Guid.NewGuid(), PartyId = partyId, Name = name, CreatedAt = DateTime.UtcNow };
        await tournaments.AddAsync(tournament, ct);
        await tournaments.SaveChangesAsync(ct);
        return tournament;
    }

    public Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => tournaments.GetWithDetailsAsync(id, ct);

    public async Task AddParticipantAsync(Guid tournamentId, Guid userId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.TournamentManage, ct);

        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.TournamentNotFound);
        if (tournament.IsStarted)
            throw new BusinessRuleException(ErrorCodes.TournamentAlreadyStarted);

        if (await participants.FindAsync(tournamentId, userId, ct) is not null)
            return;

        await participants.AddAsync(new TournamentParticipant { Id = Guid.NewGuid(), TournamentId = tournamentId, UserId = userId }, ct);
        await participants.SaveChangesAsync(ct);
    }

    public async Task RemoveParticipantAsync(Guid tournamentId, Guid userId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.TournamentManage, ct);

        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.TournamentNotFound);
        if (tournament.IsStarted)
            throw new BusinessRuleException(ErrorCodes.TournamentAlreadyStarted);

        var participant = await participants.FindAsync(tournamentId, userId, ct);
        if (participant is null) return;

        participants.Remove(participant);
        await participants.SaveChangesAsync(ct);
    }

    public async Task StartAsync(Guid tournamentId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.TournamentManage, ct);

        var tournament = await tournaments.GetWithDetailsAsync(tournamentId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.TournamentNotFound);
        if (tournament.IsStarted)
            throw new BusinessRuleException(ErrorCodes.TournamentAlreadyStarted);
        if (tournament.Participants.Count < MinParticipants)
            throw new BusinessRuleException(ErrorCodes.TournamentTooFewParticipants, MinParticipants);

        var seeded = tournament.Participants.ToList();
        var bracketSize = 1;
        while (bracketSize < seeded.Count) bracketSize *= 2;

        var slots = new TournamentParticipant?[bracketSize];
        for (var i = 0; i < seeded.Count; i++)
            slots[i] = seeded[i];

        var totalRounds = (int)Math.Log2(bracketSize);
        var bracket = new Dictionary<(int Round, int Slot), TournamentMatch>();

        for (var round = 1; round <= totalRounds; round++)
        {
            var matchesInRound = bracketSize >> round;
            for (var slot = 0; slot < matchesInRound; slot++)
            {
                var match = new TournamentMatch { Id = Guid.NewGuid(), TournamentId = tournamentId, Round = round, Slot = slot };
                if (round == 1)
                {
                    match.Participant1Id = slots[slot * 2]?.Id;
                    match.Participant2Id = slots[slot * 2 + 1]?.Id;
                }

                bracket[(round, slot)] = match;
                await matches.AddAsync(match, ct);
            }
        }

        ResolveByes(bracket);

        tournament.IsStarted = true;
        await tournaments.SaveChangesAsync(ct);
    }

    public async Task SetMatchWinnerAsync(Guid tournamentId, Guid matchId, Guid winnerParticipantId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.TournamentManage, ct);

        var tournament = await tournaments.GetWithDetailsAsync(tournamentId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.TournamentNotFound);

        var match = tournament.Matches.FirstOrDefault(m => m.Id == matchId)
            ?? throw new BusinessRuleException(ErrorCodes.MatchNotFound);

        if (match.Participant1Id != winnerParticipantId && match.Participant2Id != winnerParticipantId)
            throw new BusinessRuleException(ErrorCodes.MatchInvalidWinner);

        var bracket = tournament.Matches.ToDictionary(m => (m.Round, m.Slot));
        AdvanceWinner(bracket, match, winnerParticipantId);
        ResolveByes(bracket);

        await tournaments.SaveChangesAsync(ct);
    }

    // A match whose feeders can never produce a participant is empty; a match with exactly one
    // empty side is a bye that the other side wins without playing. Rounds are resolved in order
    // so byes cascade: with bracketSize >= 8, whole round-1 matches can be empty (bye vs bye), and
    // a reported winner can later face an empty branch. Re-running is idempotent.
    private static void ResolveByes(IReadOnlyDictionary<(int Round, int Slot), TournamentMatch> bracket)
    {
        var empty = new HashSet<(int Round, int Slot)>();

        foreach (var match in bracket.Values.OrderBy(m => m.Round))
        {
            var side1Empty = match.Round == 1
                ? match.Participant1Id is null
                : empty.Contains((match.Round - 1, match.Slot * 2));
            var side2Empty = match.Round == 1
                ? match.Participant2Id is null
                : empty.Contains((match.Round - 1, match.Slot * 2 + 1));

            if (side1Empty && side2Empty)
                empty.Add((match.Round, match.Slot));
            else if (side2Empty && match.Participant1Id is { } soleParticipant1)
                AdvanceWinner(bracket, match, soleParticipant1);
            else if (side1Empty && match.Participant2Id is { } soleParticipant2)
                AdvanceWinner(bracket, match, soleParticipant2);
        }
    }

    private static void AdvanceWinner(
        IReadOnlyDictionary<(int Round, int Slot), TournamentMatch> bracket,
        TournamentMatch match,
        Guid winnerId)
    {
        match.WinnerId = winnerId;
        if (!bracket.TryGetValue((match.Round + 1, match.Slot / 2), out var nextMatch)) return;

        if (match.Slot % 2 == 0)
            nextMatch.Participant1Id = winnerId;
        else
            nextMatch.Participant2Id = winnerId;
    }
}
