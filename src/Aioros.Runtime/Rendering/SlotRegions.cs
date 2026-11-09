using System.Globalization;
using System.Security.Cryptography;

namespace Aioros.Runtime.Rendering;

/// <summary>
/// The regions of a rendered body that slot renderers writing fields or commands own: each renderer's box carries the
/// render's nonce in <see cref="Attribute"/> and its output sits between <see cref="Open"/> and <see cref="Close"/>, so a
/// post that changed only what one renderer shows is answered with that region's content (a Turbo Frame, LiveView's diff
/// of one component). The regions are where <see cref="SlotRegionNode"/>s wrote them, noted by the render itself
/// (<see cref="SlotRegionLog"/>), never found by scanning the body, so markup a value or a writer produces cannot
/// imitate one; the script finds a region's element by the nonce, which content cannot know.
/// </summary>
public static class SlotRegions
{
    public const string Attribute = "data-aioros-slot";

    public const string Open = "<!--s-->";

    public const string Close = "<!--/s-->";
}

public enum SlotRegionPart
{
    /// <summary>The box's attribute carrying the render's nonce: a dynamic of the plan's template.</summary>
    Mark,

    Open,

    Close,
}

/// <summary>A slot region's mark, opening or closing, written by the framework where a slot renderer's box is.</summary>
public sealed class SlotRegionNode : IPlanNode
{
    private SlotRegionNode(SlotRegionPart part) => Part = part;

    public static SlotRegionNode Mark { get; } = new(SlotRegionPart.Mark);

    public static SlotRegionNode Open { get; } = new(SlotRegionPart.Open);

    public static SlotRegionNode Close { get; } = new(SlotRegionPart.Close);

    public SlotRegionPart Part { get; }

    /// <summary>The static text of an opening or closing mark, as the plan's template holds it.</summary>
    public string Text => Part switch
    {
        SlotRegionPart.Open => SlotRegions.Open,
        SlotRegionPart.Close => SlotRegions.Close,
        _ => string.Empty,
    };

    public void Write(ref HtmlOutput output, RenderScope scope) => output.WriteSlotRegion(Part);
}

/// <summary>
/// Where a render wrote its slot regions and its nonce, noted as the region nodes are written: offsets in the body, the
/// bytes of the buffer being written to counted from <see cref="Base"/>.
/// </summary>
public sealed class SlotRegionLog(PooledBuffer measured, string? nonce = null)
{
    private int _open = -1;

    /// <summary>A fresh nonce per render, eight hexadecimal digits, or the nonce of the render whose dynamics these are.</summary>
    public string Nonce { get; } = nonce ?? RandomNumberGenerator.GetInt32(int.MaxValue).ToString("x8", CultureInfo.InvariantCulture);

    /// <summary>Where the measured buffer's bytes start in the body: 0 for a body rendered whole, the body's length so far for one rendered slot by slot.</summary>
    public int Base { get; set; }

    /// <summary>The content of every slot region, between its marks, in document order.</summary>
    public List<Range> Regions { get; } = [];

    /// <summary>Where the nonce was written, left out of the hashes of what is outside each region.</summary>
    public List<Range> Nonces { get; } = [];

    private int Position => Base + measured.WrittenSpan.Length;

    internal void NonceAt(int length) => Nonces.Add((Position - length)..Position);

    internal void Opened() => _open = Position;

    internal void Closed()
    {
        if (_open >= 0)
        {
            Regions.Add(_open..Position);
            _open = -1;
        }
    }
}
