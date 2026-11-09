using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Html.Writers;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html;

/// <summary>
/// Writes a realized visual tree as HTML into a plan: each control through the writer that claims it, a view in its
/// region (<see cref="ViewWriter"/>), a layout's outlet (<see cref="OutletWriter"/>), anything else as a box
/// (<see cref="ContainerWriter"/>), with what the plan records around a control first (<see cref="PlannedVisibility"/>).
/// </summary>
internal sealed class HtmlTreeWriter
{
    private readonly HtmlContext _context;
    private readonly ViewWriter _views;
    private readonly OutletWriter _outlets;
    private readonly ContainerWriter _containers;
    private readonly PlannedVisibility _planned;
    private Control? _released;
    private Control? _root;

    public HtmlTreeWriter(PlanRecorder recorder, StyleSheet styles, ImmutableArray<object?> renderModels)
    {
        _context = new HtmlContext(recorder, styles, renderModels, Write);
        _views = new ViewWriter(_context);
        _outlets = new OutletWriter(_context);
        _containers = new ContainerWriter(_context);
        _planned = new PlannedVisibility(_context, WriteVisible);
    }

    public FormFields Fields => _context.Fields;

    public HtmlContext Context => _context;

    /// <inheritdoc cref="ViewWriter.ViewError"/>
    public ImmutableArray<IPlanNode> ViewError => _views.ViewError;

    /// <inheritdoc cref="ViewWriter.RecordViewError"/>
    public void RecordViewError(Control error) => _views.RecordViewError(error);

    /// <summary>Writes a control the page's render leaves out where it sits, as what a post left for the user.</summary>
    public void WriteLeftOut(Control control, ParentKind parent)
    {
        _released = control;
        Write(control, parent);
    }

    public void Write(Visual visual, ParentKind parent)
    {
        if (visual is not Control control || (!ReferenceEquals(control, _released) && _context.LeavesOut(control)))
        {
            return;
        }

        if (!_context.Plan.Recording)
        {
            WriteControlled(control, parent);
            return;
        }

        _context.Plan.Writing(control);
        try
        {
            WriteControlled(control, parent);
        }
        finally
        {
            _context.Plan.DoneWriting();
        }
    }

    private void WriteControlled(Control control, ParentKind parent)
    {
        _root ??= control;

        if (_context.StandsIn(control, parent))
        {
            return;
        }

        if (_views.Write(control, parent))
        {
            return;
        }

        if (_context.Flags.Write(control, () => Write(control, parent)))
        {
            return;
        }

        if (_context.Plan.Planning && _planned.Write(control, parent))
        {
            return;
        }

        _context.Modals.Write(control, WriteLeftOut);

        if (control.IsVisible || FilteredLists.WritesHidden(control) || _context.Flags.ShownInOther(control))
        {
            WriteVisible(control, parent);
        }
        else
        {
            _context.Flags.Skip(control);
        }
    }

    private void WriteVisible(Control control, ParentKind parent)
    {
        if (_context.Plan.Planning && control.TemplatedParent is null)
        {
            _context.Plan.Written(control);
        }

        _context.Flags.Visit(control);
        KeyBindingWriters.Write(_context, control, scoped: !ReferenceEquals(control, _root));
        var sheet = _context.Flags.OpenSheet(control);

        if (OverlayWriters.Decorates(control) || _context.Flags.OtherTip(control) is not null)
        {
            OverlayWriters.Write(_context, control, parent, WriteControl);
        }
        else
        {
            WriteControl(control, parent);
        }

        _context.Plan.Literal(sheet ? "</dialog>" : string.Empty);
    }

    private void WriteControl(Control control, ParentKind parent)
    {
        if (_context.Substitutes.TryGetValue(control, out var substitute))
        {
            substitute(parent);
            return;
        }

        if (!_context.HoldsPrompts(control) && ControlWriters.For(control) is { } writer)
        {
            writer.Write(_context, control, parent);
            return;
        }

        if (control is Outlet outlet)
        {
            _outlets.Write(outlet, parent);
            return;
        }

        if (_containers.PassThrough(control, parent) is { } only)
        {
            Write(only, parent);
            return;
        }

        _containers.Write(control, parent);
    }
}
