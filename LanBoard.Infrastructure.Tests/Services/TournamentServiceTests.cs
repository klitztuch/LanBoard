using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class TournamentServiceTests
{
    private static readonly Guid PartyId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly ITournamentRepository _tournaments = Substitute.For<ITournamentRepository>();
    private readonly ITournamentParticipantRepository _participants = Substitute.For<ITournamentParticipantRepository>();
    private readonly ITournamentMatchRepository _matches = Substitute.For<ITournamentMatchRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();

    private readonly TournamentService _sut;

    public TournamentServiceTests()
    {
        _sut = new TournamentService(_tournaments, _participants, _matches, _permissions);
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

    private Tournament StartableTournament(int participantCount, out List<TournamentParticipant> participants)
    {
        var tournament = CreateTournament();
        participants = Enumerable.Range(0, participantCount).Select(_ => CreateParticipant(tournament.Id)).ToList();
        foreach (var p in participants)
            tournament.Participants.Add(p);
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _matches.When(m => m.AddAsync(Arg.Any<TournamentMatch>(), Arg.Any<CancellationToken>()))
            .Do(call => tournament.Matches.Add(call.Arg<TournamentMatch>()));
        return tournament;
    }

    private static TournamentMatch MatchAt(Tournament tournament, int round, int slot)
        => tournament.Matches.Single(m => m.Round == round && m.Slot == slot);

    [Fact]
    public async Task CreateAsync_PersistsTournament()
    {
        var result = await _sut.CreateAsync(PartyId, "Cup", AdminId);

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
        await _sut.AddParticipantAsync(tournament.Id, userId, AdminId);

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

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.AddParticipantAsync(tournament.Id, Guid.NewGuid(), AdminId));
        Assert.Equal(ErrorCodes.TournamentAlreadyStarted, ex.Code);
        await _participants.DidNotReceive().AddAsync(Arg.Any<TournamentParticipant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddParticipantAsync_AlreadyAParticipant_IsIdempotent()
    {
        var tournament = CreateTournament();
        var userId = Guid.NewGuid();
        _tournaments.GetByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _participants.FindAsync(tournament.Id, userId, Arg.Any<CancellationToken>()).Returns(CreateParticipant(tournament.Id));

        await _sut.AddParticipantAsync(tournament.Id, userId, AdminId);

        await _participants.DidNotReceive().AddAsync(Arg.Any<TournamentParticipant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_FewerThanTwoParticipants_Throws()
    {
        var tournament = CreateTournament(participants: CreateParticipant(Guid.NewGuid()));
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.StartAsync(tournament.Id, AdminId));
        Assert.Equal(ErrorCodes.TournamentTooFewParticipants, ex.Code);
        Assert.Equal([2], ex.Args);
    }

    [Fact]
    public async Task StartAsync_AlreadyStarted_Throws()
    {
        var tournament = CreateTournament(isStarted: true, CreateParticipant(Guid.NewGuid()), CreateParticipant(Guid.NewGuid()));
        _tournaments.GetWithDetailsAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.StartAsync(tournament.Id, AdminId));
        Assert.Equal(ErrorCodes.TournamentAlreadyStarted, ex.Code);
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

        await _sut.StartAsync(tournament.Id, AdminId);

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

        await _sut.StartAsync(tournament.Id, AdminId);

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
    public async Task StartAsync_FiveParticipants_ByeCascadesPastEmptyBranchIntoFinal()
    {
        var tournament = StartableTournament(5, out var participants);

        await _sut.StartAsync(tournament.Id, AdminId);

        // bracketSize=8: round-1 slot2 = p5 vs bye, slot3 = bye vs bye (empty branch)
        var p5 = participants[4].Id;
        var emptyMatch = MatchAt(tournament, 1, 3);
        Assert.Null(emptyMatch.Participant1Id);
        Assert.Null(emptyMatch.Participant2Id);
        Assert.Null(emptyMatch.WinnerId);

        var round2Bye = MatchAt(tournament, 2, 1);
        Assert.Equal(p5, round2Bye.Participant1Id);
        Assert.Null(round2Bye.Participant2Id);
        Assert.Equal(p5, round2Bye.WinnerId);
        Assert.Equal(p5, MatchAt(tournament, 3, 0).Participant2Id);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(11)] // a reported winner faces an empty branch in round 3 and must advance on its own
    public async Task SetMatchWinnerAsync_PlayingOutBracketWithByes_ProducesAChampion(int participantCount)
    {
        var tournament = StartableTournament(participantCount, out _);

        await _sut.StartAsync(tournament.Id, AdminId);

        // Each report decides one match, so a bracket can never need more reports than it has matches.
        for (var reports = 0; reports <= tournament.Matches.Count; reports++)
        {
            if (tournament.Matches.FirstOrDefault(m => m is { Participant1Id: not null, Participant2Id: not null, WinnerId: null }) is not { } playable)
                break;
            await _sut.SetMatchWinnerAsync(tournament.Id, playable.Id, playable.Participant1Id!.Value, AdminId);
        }

        var final = Assert.Single(tournament.Matches, m => m.Round == tournament.Matches.Max(x => x.Round));
        Assert.NotNull(final.WinnerId);
        // No match is left stuck with a participant but without a winner.
        Assert.DoesNotContain(tournament.Matches, m => (m.Participant1Id ?? m.Participant2Id) is not null && m.WinnerId is null);
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

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.SetMatchWinnerAsync(tournament.Id, match.Id, Guid.NewGuid(), AdminId));
        Assert.Equal(ErrorCodes.MatchInvalidWinner, ex.Code);
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

        await _sut.SetMatchWinnerAsync(tournament.Id, semiMatch.Id, p2.Id, AdminId);

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

        await _sut.SetMatchWinnerAsync(tournament.Id, finalMatch.Id, p1.Id, AdminId);

        Assert.Equal(p1.Id, finalMatch.WinnerId);
        await _tournaments.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    public static TheoryData<string> MutatingMethods => ["Create", "AddParticipant", "RemoveParticipant", "Start", "SetMatchWinner"];

    [Theory]
    [MemberData(nameof(MutatingMethods))]
    public async Task MutatingMethod_CallerLacksPermission_ThrowsAndChangesNothing(string method)
    {
        var deniedUserId = Guid.NewGuid();
        _permissions.EnsureAsync(deniedUserId, Permissions.TournamentManage, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionDeniedException(Permissions.TournamentManage)));
        var tournamentId = Guid.NewGuid();

        Func<Task> act = method switch
        {
            "Create" => () => _sut.CreateAsync(PartyId, "Cup", deniedUserId),
            "AddParticipant" => () => _sut.AddParticipantAsync(tournamentId, Guid.NewGuid(), deniedUserId),
            "RemoveParticipant" => () => _sut.RemoveParticipantAsync(tournamentId, Guid.NewGuid(), deniedUserId),
            "Start" => () => _sut.StartAsync(tournamentId, deniedUserId),
            "SetMatchWinner" => () => _sut.SetMatchWinnerAsync(tournamentId, Guid.NewGuid(), Guid.NewGuid(), deniedUserId),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        await Assert.ThrowsAsync<PermissionDeniedException>(act);

        await _tournaments.DidNotReceive().AddAsync(Arg.Any<Tournament>(), Arg.Any<CancellationToken>());
        await _participants.DidNotReceive().AddAsync(Arg.Any<TournamentParticipant>(), Arg.Any<CancellationToken>());
        _participants.DidNotReceive().Remove(Arg.Any<TournamentParticipant>());
        await _matches.DidNotReceive().AddAsync(Arg.Any<TournamentMatch>(), Arg.Any<CancellationToken>());
        await _tournaments.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _participants.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
