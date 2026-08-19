using Fuse.Core.Areas.Undo;
using Fuse.Core.Areas.Activity;
using Fuse.Core.Helpers;

namespace Fuse.API;

/// <summary>
/// Resolves the area-specific undo permission from the version referenced by the route.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireUndoPermissionAttribute : Attribute
{
    public async Task<string?> ResolvePermissionKeyAsync(HttpContext context, CancellationToken ct)
    {
        if (!context.Request.RouteValues.TryGetValue("versionId", out var rawVersionId)
            || !Guid.TryParse(rawVersionId?.ToString(), out var versionId))
        {
            return null;
        }

        var history = context.RequestServices.GetRequiredService<IVersionHistoryService>();
        var version = await history.GetVersionByIdAsync(versionId, ct);
        return version is null ? null : UndoPermissionMapper.ToPermissionKey(version.EntityType);
    }
}
