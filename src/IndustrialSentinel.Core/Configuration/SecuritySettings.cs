namespace IndustrialSentinel.Core.Configuration;

public sealed class SecuritySettings
{
    public int SessionTimeoutMinutes { get; init; } = 10;
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 10;
    public int PasswordMinLength { get; init; } = 12;
    public int PasswordMaxAgeDays { get; init; } = 0;
    public bool AllowSelfRegistration { get; init; } = false;
    public bool RequireUpper { get; init; } = true;
    public bool RequireLower { get; init; } = true;
    public bool RequireDigit { get; init; } = true;
    public bool RequireSymbol { get; init; } = true;

    public static SecuritySettings Default() => new();
}
