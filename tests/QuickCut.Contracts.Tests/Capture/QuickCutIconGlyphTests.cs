using System.Windows.Media;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class QuickCutIconGlyphTests
{
    [Theory]
    [MemberData(nameof(Icons))]
    public void AllIconPathsAreValidGeometry(QuickCutIcon icon)
    {
        var geometry = Geometry.Parse(QuickCutIconGlyphs.PathData(icon));

        Assert.False(geometry.IsEmpty());
    }

    public static IEnumerable<object[]> Icons() =>
        Enum.GetValues<QuickCutIcon>().Select(icon => new object[] { icon });
}
