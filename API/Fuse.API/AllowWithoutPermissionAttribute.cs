namespace Fuse.API;

/// <summary>
/// Marks an API endpoint that intentionally does not use the role-permission catalog.
/// The endpoint may still enforce authentication or participant credentials itself.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowWithoutPermissionAttribute : Attribute
{
}
