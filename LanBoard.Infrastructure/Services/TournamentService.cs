using LanBoard.Application.Interfaces;
using LanBoard.Application.Tournaments;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class TournamentService(
    ITournamentRepository tournaments,
    ITournamentParticipantRepository participants,
    ITournamentMatchRepository matches) : ITournamentService
{
    public Task<IReadOnlyList<Tournament>> GetByPartyAsync(Guid partyId, CancellationToken ct = default)
        => tournaments.GetByPartyAsync(partyId, ct);

    public async Task<Tournament> CreateAsync(Guid partyId, string name, CancellationToken ct = default)
    {
        var tournament = new Tournament { Id = Guid.NewGuid(), PartyId = partyId, Name = name, CreatedAt = DateTime.UtcNow };
        await tournaments.AddAsync(tournament, ct);
        await tournaments.SaveChangesAsync(ct);
        return tournament;
    }

    public Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => tournaments.GetWithDetailsAsync(id, ct);

    public async Task AddParticipantAsync(Guid tournamentId, Guid userId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (tournament.IsStarted)
            throw new InvalidOperationException("Tournament has already started.");

        if (await participants.FindAsync(tournamentId, userId, ct) is not null)
            return;

        await participants.AddAsync(new TournamentParticipant { Id = Guid.NewGuid(), TournamentId = tournamentId, UserId = userId }, ct);
        await participants.SaveChangesAsync(ct);
    }

    public async Task RemoveParticipantAsync(Guid tournamentId, Guid userId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (tournament.IsStarted)
            throw new InvalidOperationException("Tournament has already started.");

        var participant = await participants.FindAsync(tournamentId, userId, ct);
        if (participant is null) return;

        participants.Remove(participant);
        await participants.SaveChangesAsync(ct);
    }

    public async Task StartAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetWithDetailsAsync(tournamentId, ct)
            ?? throw new InvalidOperationException("Tournament not found.");
        if (tournament.IsStarted)
            throw new InvalidOperationException("Tournament has already started.");
        if (tournament.Participants.Count < 2)
            throw new InvalidOperationException("At least 2 participants are required to start a tournament.");

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

        // A round-1 match with only one participant (an odd participant count doesn't fill the
        // bracket evenly) is an automatic bye: that participant advances without playing.
        foreach (var match in bracket.Values.Where(m => m.Round == 1))
        {
            if (match.Participant1Id is { } soleParticipant1 && match.Participant2Id is null)
                AdvanceWinner(bracket, match, soleParticipant1, totalRounds);
            else if (match.Participant2Id is { } soleParticipant2 && match.Participant1Id is null)
                AdvanceWinner(bracket, match, soleParticipant2, totalRounds);
        }

        tournament.IsStarted = true;
        await tournaments.SaveChangesAsync(ct);
    }

    public async Task SetMatchWinnerAsync(Guid tournamentId, Guid matchId, Guid winnerParticipantId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetWithDetailsAsync(tournamentId, ct)
            ?? throw new InvalidOperationException("Tournament not found.");

        var match = tournament.Matches.FirstOrDefault(m => m.Id == matchId)
            ?? throw new InvalidOperationException("Match not found.");

        if (match.Participant1Id != winnerParticipantId && match.Participant2Id != winnerParticipantId)
            throw new InvalidOperationException("Winner must be one of the match's participants.");

        match.WinnerId = winnerParticipantId;

        var totalRounds = tournament.Matches.Max(m => m.Round);
        if (match.Round < totalRounds)
        {
            var nextMatch = tournament.Matches.First(m => m.Round == match.Round + 1 && m.Slot == match.Slot / 2);
            if (match.Slot % 2 == 0)
                nextMatch.Participant1Id = winnerParticipantId;
            else
                nextMatch.Participant2Id = winnerParticipantId;
        }

        await tournaments.SaveChangesAsync(ct);
    }

    private static void AdvanceWinner(
        Dictionary<(int Round, int Slot), TournamentMatch> bracket,
        TournamentMatch match,
        Guid winnerId,
        int totalRounds)
    {
        match.WinnerId = winnerId;
        if (match.Round >= totalRounds) return;

        var nextMatch = bracket[(match.Round + 1, match.Slot / 2)];
        if (match.Slot % 2 == 0)
            nextMatch.Participant1Id = winnerId;
        else
            nextMatch.Participant2Id = winnerId;
    }
}
