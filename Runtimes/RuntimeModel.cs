namespace GPU_T.StressTest.Runtimes;

/// <summary>
/// Type of Windows compatibility environment on Linux.
/// </summary>
public enum RuntimeType
{
    Wine,
    Proton
}

/// <summary>
/// Represents a discovered Wine or Proton installation.
/// </summary>
public sealed record RuntimeEnvironment(
    string Name,
    RuntimeType Type,
    string ExecutablePath,
    string? PrefixPath = null
);