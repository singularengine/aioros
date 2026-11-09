namespace Area.Tests;

public sealed class AreaTests
{
    [Fact]
    [Trait("AC", "AREA-01")]
    public void CoveredOnce()
    {
    }

    [Fact]
    [Trait("AC", "AREA-03")]
    public void FirstDuplicate()
    {
    }

    [Fact]
    [Trait("AC", "AREA-03")]
    public void SecondDuplicate()
    {
    }

    [Fact]
    [Trait("AC", "AREA-99")]
    public void UnknownCriterion()
    {
    }

    [Fact]
    public void NoCriterion()
    {
    }
}
