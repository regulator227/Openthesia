using Openthesia.Core.Practice;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticePlaybackFilterTests
{
    [Fact]
    public void ConfiguredPlaybackAllowsOnlyAccompanimentNotes()
    {
        try
        {
            PracticePlaybackFilter.Configure(new[] { 1, 3 });

            Assert.True(PracticePlaybackFilter.AllowsAll(new[] { 1, 3 }));
            Assert.False(PracticePlaybackFilter.AllowsAll(new[] { 1, 2 }));
        }
        finally
        {
            PracticePlaybackFilter.Disable();
        }

        Assert.True(PracticePlaybackFilter.AllowsAll(new[] { 2 }));
    }
}
