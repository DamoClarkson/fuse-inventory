using Fuse.Core.Models;
using Xunit;

namespace Fuse.Tests.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void ScrumPoker_IsDisabledByDefault()
    {
        Assert.False(new AppSettings().ScrumPokerEnabled);
    }

    [Fact]
    public void ScrumPoker_CanBeEnabledWithoutChangingOtherDefaults()
    {
        var settings = new AppSettings(ScrumPokerEnabled: true);

        Assert.True(settings.ScrumPokerEnabled);
        Assert.True(settings.IncompleteDataWarningEnabled);
        Assert.False(settings.McpServerEnabled);
    }
}
