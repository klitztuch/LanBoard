namespace LanBoard.Application.Errors;

public static class ErrorCodes
{
    public const string PartyNotFound = "Party.NotFound";
    public const string SeatNotFound = "Seat.NotFound";
    public const string SeatTaken = "Seat.Taken";
    public const string SeatNotAssignedToUser = "Seat.NotAssignedToUser";
    public const string SeatAssigned = "Seat.Assigned";
    public const string TournamentNotFound = "Tournament.NotFound";
    public const string TournamentAlreadyStarted = "Tournament.AlreadyStarted";
    public const string TournamentTooFewParticipants = "Tournament.TooFewParticipants"; // args: min
    public const string MatchNotFound = "Match.NotFound";
    public const string MatchInvalidWinner = "Match.InvalidWinner";
    public const string RoleNotFound = "Role.NotFound";
    public const string RoleNameInvalid = "Role.NameInvalid";             // args: maxLength
    public const string RoleNameTaken = "Role.NameTaken";
    public const string RoleSystemImmutable = "Role.SystemImmutable";
    public const string RoleUnknownPermission = "Role.UnknownPermission"; // args: key
    public const string RoleLastAdmin = "Role.LastAdmin";
    public const string UserNotFound = "User.NotFound";

    public static IReadOnlyList<string> All { get; } = ConstantCatalog.Of(typeof(ErrorCodes));
}
