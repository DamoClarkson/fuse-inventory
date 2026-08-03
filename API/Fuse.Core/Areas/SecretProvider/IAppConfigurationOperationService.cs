using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SecretProvider;

public interface IAppConfigurationOperationService
{
    Task<Result<IReadOnlyList<AppConfigurationEntry>>> ListKeyValuesAsync(
        Guid providerId,
        string? keySearch = null,
        string? keyPrefix = null,
        string? label = null);

    Task<Result<AppConfigurationEntry>> SetKeyValueAsync(SetAppConfigurationValue command, string userName, Guid? userId);

    Task<Result<ResolvedAppConfigurationReferenceSecretResponse>> RevealReferencedSecretAsync(
        Guid providerId,
        string key,
        string? label,
        string userName,
        Guid? userId);
}
