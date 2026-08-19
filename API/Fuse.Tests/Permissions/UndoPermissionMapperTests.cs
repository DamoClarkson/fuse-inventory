using Fuse.Core.Helpers;
using Fuse.Core.Areas.Undo;
using Fuse.Core.Models;
using Xunit;

namespace Fuse.Tests.Permissions;

public class UndoPermissionMapperTests
{
    [Theory]
    [InlineData(EntityType.Application, UndoPermissions.ApplicationsUndoKey)]
    [InlineData(EntityType.Account, UndoPermissions.AccountsUndoKey)]
    [InlineData(EntityType.Identity, UndoPermissions.IdentitiesUndoKey)]
    [InlineData(EntityType.DataStore, UndoPermissions.DataStoresUndoKey)]
    [InlineData(EntityType.SecurityRole, UndoPermissions.SecurityUndoKey)]
    [InlineData(EntityType.PasswordGeneratorConfig, UndoPermissions.ConfigurationUndoKey)]
    public void ToPermissionKey_MapsEntityTypeToStringPermission(EntityType entityType, string expected)
    {
        Assert.Equal(expected, UndoPermissionMapper.ToPermissionKey(entityType));
    }
}
