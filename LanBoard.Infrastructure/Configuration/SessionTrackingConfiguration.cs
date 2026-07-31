using System.ComponentModel.DataAnnotations;

namespace LanBoard.Infrastructure.Configuration;

public class SessionTrackingConfiguration
{
    [Range(1, int.MaxValue)]
    public int PollIntervalSeconds { get; set; } = 60;
}
