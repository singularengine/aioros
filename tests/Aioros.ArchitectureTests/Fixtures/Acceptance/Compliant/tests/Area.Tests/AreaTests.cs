namespace Area.Tests;

public sealed class AreaTests
{
    [Fact]
    [Trait("AC", "AREA-01")]
    public void AValueIsReturnedUnchanged()
    {
    }

    [Theory]
    [Trait("AC", "AREA-02")]
    [InlineData("")]
    public void NothingIsReturnedWithoutAValue(string value)
    {
    }

    private static void NotATest()
    {
    }
}
