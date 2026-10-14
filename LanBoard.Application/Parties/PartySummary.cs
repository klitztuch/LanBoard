namespace LanBoard.Application.Parties;

public record PartySummary(Guid Id, string Name, DateTime Date, string Location, bool IsActive);
