using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class TournamentServiceTests
{
    private static readonly Guid PartyId = Guid.NewGuid();

    private readonly ITournamentRepository _tournaments = Substitute.For<ITournamentRepository>();
    private readonly ITournamentParticipantRepository _participants = Substitute.For<ITournamentParticipantRepository>();
    private readonly ITournamentMatchRepository _matches = Substitute.For<ITournamentMatchRepository>();

    private readonly TournamentService _sut;

    public TournamentServiceTests()
    {
        _sut = new TournamentService(_tournaments, _participants, _matches);
    }

    private static Tournament CreateTournament(bool isStarted = false, params TournamentParticipant[] participants)
    {
        var tournament = new Tournament { Id = Guid.NewGuid(), PartyId = PartyId, Name = "Cup", CreatedAt = DateTime.UtcNow, IsStarted = isStarted };
        foreach (var p in participants)
            tournament.Participants.Add(p);
        return tournament;
    }

    private static TournamentParticipant CreateParticipant(Guid tournamentId)
        => new() { Id = Guid.NewGuid(), TournamentId = tournamentId, UserId = Guid.NewGuid() };

    [Fact]
    public async Task CreateAsync_PersistsTournament()
    {
        var result = await _sut.CreateAsync(PartyId, "Cup");

        Assert.Equal(PartyId, result.PartyId);
        Assert.Equal("Cup", result.Name);
        Assert.False(result.IsStarted);
        await _tournaments.Received(1).AddAsync(result, Arg.Any<CancellationToken>());
        await _tournaments.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddParticipantAsync_NotStarted_Adds()
    {
        var tournament = CreateTournament();
        _tournaments.GetByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _participants.FindAsync(tournament.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentParticipant?)null);

        var userId = Guid.NewGuid();
        await _sut.AddParticipantAsync(tournament.Id, userId);

        await _participants.Received(1).AddAsync(
            Arg.Is<TournamentParticipant>(p => p.TournamentId == tournament.Id && p.UserId == userId),
            Arg.Any<CancellationToken>());
        await _participants.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddParticipantAsync_AlreadyStarted_Throws()
    {
        var tournament = CreateTournament(isStarted: true);
        _tournaments.GetByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AddParticipantAsync(tournament.Id, Guid.NewGuid()));
        await _participants.DidNotReceive().AddAsync(Arg.Any<TournamentParticipant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddParticipantAsync_AlreadyAParticipant_IsIdempotent()
    {
        var tournament = CreateTournament();
        var userId = Guid.NewGuid();
        _tournaments.GetByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _participants.FindAsync(tournament.Id, userId, Arg.Any<CancellationToken>()).Returns(CreateParticipant(tournament.Id));

        await _sut.AddParticipantAsync(tournament.Id, userId);

        await _participants.DidNotReceive().AddAsync(Arg.Any<TournamentParticipant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_FewerThanTwoParticipants_Throws()
    {
        var tournament = CreateTournament(participants: CreateParticipant(Guid.NewGuid()));
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.StartAsync(tournament.Id));
    }

    [Fact]
    public async Task StartAsync_AlreadyStarted_Throws()
    {
        var tournament = CreateTournament(isStarted: true, CreateParticipant(Guid.NewGuid()), CreateParticipant(Guid.NewGuid()));
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.StartAsync(tournament.Id));
    }

    [Fact]
    public async Task StartAsync_FourParticipants_CreatesFullBracketWithNoByes()
    {
        var tournament = CreateTournament();
        var p1 = CreateParticipant(tournament.Id);
        var p2 = CreateParticipant(tournament.Id);
        var p3 = CreateParticipant(tournament.Id);
        var p4 = CreateParticipant(tournament.Id);
        tournament.Participants.Add(p1);
        tournament.Participants.Add(p2);
        tournament.Participants.Add(p3);
        tournament.Participants.Add(p4);
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var createdMatches = new List<TournamentMatch>();
        _matches.When(m => m.AddAsync(Arg.Any<TournamentMatch>(), Arg.Any<CancellationToken>()))
            .Do(call => createdMatches.Add(call.Arg<TournamentMatch>()));

        await _sut.StartAsync(tournament.Id);

        Assert.True(tournament.IsStarted);
        Assert.Equal(3, createdMatches.Count); // 2 round-1 matches + 1 final
        var round1 = createdMatches.Where(m => m.Round == 1).OrderBy(m => m.Slot).ToList();
        Assert.Equal(2, round1.Count);
        Assert.Equal(p1.Id, round1[0].Participant1Id);
        Assert.Equal(p2.Id, round1[0].Participant2Id);
        Assert.Equal(p3.Id, round1[1].Participant1Id);
        Assert.Equal(p4.Id, round1[1].Participant2Id);
        Assert.All(round1, m => Assert.Null(m.WinnerId));

        var final = Assert.Single(createdMatches, m => m.Round == 2);
        Assert.Null(final.Participant1Id);
        Assert.Null(final.Participant2Id);
    }

    [Fact]
    public async Task StartAsync_ThreeParticipants_ByeAutoAdvancesIntoRoundTwo()
    {
        var tournament = CreateTournament();
        var p1 = CreateParticipant(tournament.Id);
        var p2 = CreateParticipant(tournament.Id);
        var p3 = CreateParticipant(tournament.Id);
        tournament.Participants.Add(p1);
        tournament.Participants.Add(p2);
        tournament.Participants.Add(p3);
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var createdMatches = new List<TournamentMatch>();
        _matches.When(m => m.AddAsync(Arg.Any<TournamentMatch>(), Arg.Any<CancellationToken>()))
            .Do(call => createdMatches.Add(call.Arg<TournamentMatch>()));

        await _sut.StartAsync(tournament.Id);

        // bracketSize=4: round-1 slot0 = p1 vs p2 (real match), slot1 = p3 vs bye (auto-win for p3)
        var round1 = createdMatches.Where(m => m.Round == 1).OrderBy(m => m.Slot).ToList();
        Assert.Equal(p1.Id, round1[0].Participant1Id);
        Assert.Equal(p2.Id, round1[0].Participant2Id);
        Assert.Null(round1[0].WinnerId);

        Assert.Equal(p3.Id, round1[1].Participant1Id);
        Assert.Null(round1[1].Participant2Id);
        Assert.Equal(p3.Id, round1[1].WinnerId);

        var final = createdMatches.Single(m => m.Round == 2);
        Assert.Null(final.Participant1Id); // slot0 winner not decided yet
        Assert.Equal(p3.Id, final.Participant2Id); // bye winner already fed forward
    }

    [Fact]
    public async Task SetMatchWinnerAsync_WinnerNotAParticipant_Throws()
    {
        var tournament = CreateTournament(isStarted: true);
        var p1 = CreateParticipant(tournament.Id);
        var p2 = CreateParticipant(tournament.Id);
        var match = new TournamentMatch { Id = Guid.NewGuid(), TournamentId = tournament.Id, Round = 1, Slot = 0, Participant1Id = p1.Id, Participant2Id = p2.Id };
        tournament.Matches.Add(match);
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SetMatchWinnerAsync(tournament.Id, match.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task SetMatchWinnerAsync_AdvancesWinnerToNextRoundMatch()
    {
        var tournament = CreateTournament(isStarted: true);
        var p1 = CreateParticipant(tournament.Id);
        var p2 = CreateParticipant(tournament.Id);
        var semiMatch = new TournamentMatch { Id = Guid.NewGuid(), TournamentId = tournament.Id, Round = 1, Slot = 1, Participant1Id = p1.Id, Participant2Id = p2.Id };
        var finalMatch = new TournamentMatch { Id = Guid.NewGuid(), TournamentId = tournament.Id, Round = 2, Slot = 0 };
        tournament.Matches.Add(semiMatch);
        tournament.Matches.Add(finalMatch);
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        await _sut.SetMatchWinnerAsync(tournament.Id, semiMatch.Id, p2.Id);

        Assert.Equal(p2.Id, semiMatch.WinnerId);
        // slot 1 is odd -> feeds into the next match's Participant2 slot
        Assert.Equal(p2.Id, finalMatch.Participant2Id);
        Assert.Null(finalMatch.Participant1Id);
        await _tournaments.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetMatchWinnerAsync_FinalRound_DoesNotLookForNextMatch()
    {
        var tournament = CreateTournament(isStarted: true);
        var p1 = CreateParticipant(tournament.Id);
        var p2 = CreateParticipant(tournament.Id);
        var finalMatch = new TournamentMatch { Id = Guid.NewGuid(), TournamentId = tournament.Id, Round = 1, Slot = 0, Participant1Id = p1.Id, Participant2Id = p2.Id };
        tournament.Matches.Add(finalMatch);
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        await _sut.SetMatchWinnerAsync(tournament.Id, finalMatch.Id, p1.Id);

        Assert.Equal(p1.Id, finalMatch.WinnerId);
        await _tournaments.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
