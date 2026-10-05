using Sage.Kahoot.Application.Abstractions;

namespace Sage.Kahoot.Infrastructure.Time;

/// <summary>Relógio real do servidor. Substituível em teste por um relógio controlado.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
