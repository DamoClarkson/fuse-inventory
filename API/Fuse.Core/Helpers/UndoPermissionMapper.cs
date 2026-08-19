using Fuse.Core.Areas.Undo;
using Fuse.Core.Models;

namespace Fuse.Core.Helpers;

public static class UndoPermissionMapper
{
    public static string ToPermissionKey(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Application => UndoPermissions.ApplicationsUndoKey,
            EntityType.Account => UndoPermissions.AccountsUndoKey,
            EntityType.Identity => UndoPermissions.IdentitiesUndoKey,
            EntityType.DataStore => UndoPermissions.DataStoresUndoKey,
            EntityType.Platform => UndoPermissions.PlatformsUndoKey,
            EntityType.Environment => UndoPermissions.EnvironmentsUndoKey,
            EntityType.ExternalResource => UndoPermissions.ExternalResourcesUndoKey,
            EntityType.MessageBroker => UndoPermissions.MessageBrokersUndoKey,
            EntityType.Tag => UndoPermissions.TagsUndoKey,
            EntityType.Position => UndoPermissions.PositionsUndoKey,
            EntityType.ResponsibilityType => UndoPermissions.ResponsibilitiesUndoKey,
            EntityType.ResponsibilityAssignment => UndoPermissions.ResponsibilitiesUndoKey,
            EntityType.Risk => UndoPermissions.RisksUndoKey,
            EntityType.SecretProvider => UndoPermissions.SecretProvidersUndoKey,
            EntityType.SqlIntegration => UndoPermissions.SqlIntegrationsUndoKey,
            EntityType.KumaIntegration => UndoPermissions.KumaIntegrationsUndoKey,
            EntityType.SecurityUser => UndoPermissions.SecurityUndoKey,
            EntityType.SecurityRole => UndoPermissions.SecurityUndoKey,
            EntityType.PasswordGeneratorConfig => UndoPermissions.ConfigurationUndoKey,
            _ => UndoPermissions.ConfigurationUndoKey
        };
    }

}
