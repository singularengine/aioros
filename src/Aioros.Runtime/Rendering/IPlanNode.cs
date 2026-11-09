namespace Aioros.Runtime.Rendering;

public interface IPlanNode
{
    void Write(ref HtmlOutput output, RenderScope scope);
}
