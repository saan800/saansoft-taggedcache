namespace SaanSoft.Tests.TaggedCache;

public class UnitTests
{
    /// <summary>
    /// Fix for "Zero tests ran" error in this test project
    /// </summary>
    [Fact]
    public void PlaceHolderTest()
    {
        var str = "hello";
        str.Should().NotBeNullOrWhiteSpace();
    }
}
