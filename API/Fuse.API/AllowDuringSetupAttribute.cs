namespace Fuse.API;

/// <summary>
/// Marks endpoints that may be called while the application is in setup mode
/// (no admin account exists yet).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowDuringSetupAttribute : Attribute
{
}
