namespace IndustrialSentinel.Core.Security;

public sealed record UserAccount(
    long Id,
    string Username,
    UserRole Role,
    bool IsLocked,
    DateTime? LockoutUntilUtc,
    DateTime? PasswordChangedUtc);
