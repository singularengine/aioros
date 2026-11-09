using Aioros.Markup;
using System.Collections.Immutable;
using Aioros.Avalonia.Commands;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html;
using Aioros.Avalonia.Html.Writers;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Aioros.Avalonia.Forms;

internal sealed record PostedForm(ImmutableDictionary<string, string> Values, ImmutableArray<PostedFile> Files);

/// <summary>
/// Binds a post into the page Avalonia shows and runs its button's command. A command that opens a prompt over the page
/// and waits on it leaves the post answered with the prompt, which carries the steps that opened it; a post from the
/// prompt replays them on the fresh page before its own step (stateless, as Web Forms rebuilds its page from the post
/// before it raises the event), so the command resumes with the prompt's answer.
/// </summary>
internal static class FormBinder
{
    public static async ValueTask<PostStatus> ApplyAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm posted, Prompts prompts)
    {
        if (PromptSteps.Parse(posted.Values) is not { } steps)
        {
            return PostStatus.BadRequest;
        }

        if (!steps.IsEmpty)
        {
            return await ReplayAsync(window, root, models, posted, prompts, steps);
        }

        var (status, running, _) = await PressAsync(window, root, models, posted, prompts, static () => static _ => true);
        if (running is null)
        {
            return status;
        }

        prompts.NoticeReplay = PromptSteps.Encode([Opening(posted.Values)]);
        if (prompts.Opened())
        {
            prompts.Replay = prompts.NoticeReplay;
            return PostStatus.Rendered;
        }

        await running;
        return PostStatus.Rendered;
    }

    private static async ValueTask<PostStatus> ReplayAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm posted, Prompts prompts, ImmutableArray<ImmutableDictionary<string, string>> steps)
    {
        var values = posted.Values.Remove(PlanForms.PromptField);
        var (opened, running, _) = await PressAsync(window, root, models, posted with { Values = values.SetItem(PlanForms.ButtonField, steps[0][PlanForms.ButtonField]) }, prompts, static () => static _ => true);
        if (opened != PostStatus.Rendered)
        {
            return opened;
        }

        if (running is null || !prompts.Opened())
        {
            return running is not null && steps.Length == 1 && prompts.Noticed() ? await NoticedAsync(window, root, models, posted with { Values = values }, prompts) : PostStatus.Conflict;
        }

        return await StepsAsync(window, root, models, posted, prompts, steps, values, running);
    }

    /// <summary>Replays the steps after the opening one, then the post's own, while the prompt stays open; the opening command ends once it closes.</summary>
    private static async ValueTask<PostStatus> StepsAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm posted, Prompts prompts, ImmutableArray<ImmutableDictionary<string, string>> steps, ImmutableDictionary<string, string> values, Task running)
    {
        var recorded = ImmutableArray.Create(steps[0]);
        for (var index = 1; index <= steps.Length; index++)
        {
            var step = index < steps.Length ? steps[index] : values;
            var (status, fields) = await StepAsync(window, root, models, posted with { Values = step }, prompts);
            if (status != PostStatus.Rendered)
            {
                return status;
            }

            if (!prompts.Opened())
            {
                return index < steps.Length ? PostStatus.Conflict : await EndAsync(running);
            }

            recorded = recorded.Add(Recorded(step, fields, prompts.Held()));
        }

        prompts.Replay = PromptSteps.Encode(recorded);
        return PostStatus.Rendered;
    }

    /// <summary>Presses a button of a notice the replayed step showed (a toast's action), with its command run to its end.</summary>
    private static async ValueTask<PostStatus> NoticedAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm step, Prompts prompts) =>
        (await prompts.WithNoticesAsync(() => StepAsync(window, root, models, step, prompts))).Status;

    /// <summary>Runs a step of an open prompt: binds the prompt's fields and presses its button, awaiting the button's command unless it opened another prompt.</summary>
    private static async ValueTask<(PostStatus Status, FormFields Fields)> StepAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm step, Prompts prompts)
    {
        Hosting.Realization.Realize(window);
        Dispatcher.UIThread.RunJobs();
        prompts.Step();
        var (status, pressed, fields) = await PressAsync(window, root, models, step, prompts, prompts.Held);
        if (status == PostStatus.Rendered && step.Values.TryGetValue(PlanForms.DismissField, out var dismissal))
        {
            PromptDismissals.Dismiss(window, prompts, dismissal);
        }

        if (pressed is { IsCompleted: false } && !prompts.OpenedInStep())
        {
            await pressed;
        }

        return (status, fields);
    }

    private static async ValueTask<PostStatus> EndAsync(Task running)
    {
        await running;
        return PostStatus.Rendered;
    }

    /// <summary>The opening step: the button that opened the prompt; the page's fields stay in the page and are posted again.</summary>
    private static ImmutableDictionary<string, string> Opening(ImmutableDictionary<string, string> form) =>
        ImmutableDictionary<string, string>.Empty.Add(PlanForms.ButtonField, form[PlanForms.ButtonField]);

    /// <summary>A step that left a prompt open: its button and the values of the prompt's fields, which leave the page with the prompt.</summary>
    private static ImmutableDictionary<string, string> Recorded(ImmutableDictionary<string, string> step, FormFields fields, Func<Control, bool> held)
    {
        var names = Enumerable.Range(0, fields.Count).Where(index => !fields.IsButton(index) && held(fields[index])).Select(index => "f" + fields.Token(index)).ToHashSet(StringComparer.Ordinal);
        return step.Where(pair => pair.Key == PlanForms.ButtonField || names.Contains(pair.Key)).ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>Binds the post's fields and presses its button, on the fields a render wrote for its items.</summary>
    private static async ValueTask<(PostStatus Status, Task? Running, FormFields Fields)> PressedAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm posted, Prompts prompts, FormFields fields, ViewTarget target, Func<Control, bool> filter)
    {
        var form = posted.Values;
        var token = form.TryGetValue(PlanForms.ButtonField, out var pressedValue) && pressedValue.Length > 0 ? pressedValue[..Math.Max(0, pressedValue.IndexOf('|', StringComparison.Ordinal))] : null;
        if (token is null)
        {
            await BindAsync(window, fields, target, posted, filter);
            Dispatcher.UIThread.RunJobs();
            Changed(fields, form);
            return (PostStatus.Rendered, null, fields);
        }

        var pressed = Find(fields, token);
        if (Verified(fields, pressed, token, pressedValue!))
        {
            await BindAsync(window, fields, target, posted, filter);
            Dispatcher.UIThread.RunJobs();
        }
        else
        {
            var slots = Enumerable.Range(0, fields.Count).Select(fields.Slot).Where((slot, index) => slot?.Bind is not null && fields.Target(index) == target).ToList();
            if (!Enumerable.Range(0, fields.Count).Any(index => fields.Slot(index) is not null && fields.Target(index) == target) || slots.Exists(static slot => slot!.Kept is null))
            {
                return (PostStatus.Conflict, null, fields);
            }

            var undo = slots.ConvertAll(static slot => slot!.Kept!());
            var slotted = Enumerable.Range(0, fields.Count).Where(index => fields.Slot(index) is not null).Select(index => fields[index]).ToHashSet();
            await BindAsync(window, fields, target, posted, control => slotted.Contains(control) && filter(control));
            Dispatcher.UIThread.RunJobs();
            (_, fields) = HtmlCompiler.CompileWithFields(window, root, models, planning: false, overlays: prompts.Shown());
            pressed = Find(fields, token);
            if (!Verified(fields, pressed, token, pressedValue!))
            {
                undo.ForEach(static put => put());
                Dispatcher.UIThread.RunJobs();
                return (PostStatus.Conflict, null, fields);
            }

            await BindAsync(window, fields, target, posted, control => !slotted.Contains(control) && filter(control));
            Dispatcher.UIThread.RunJobs();
        }

        var (status, running) = await PressAsync(window, fields, pressed, token, pressedValue!);
        return (status, running, fields);
    }

    /// <summary>
    /// The page's fields once the post's lists are the ones its page wrote. Otherwise the fields written before the first
    /// list, which no list can shift, are bound and the lists looked at again, the changed field left out; when they
    /// still differ, a post that only changed such a field binds it alone, and anything else is refused with what was bound put back.
    /// </summary>
    private static async ValueTask<(FormFields Fields, bool ChangedOnly)?> HoldsItemsAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm posted, Prompts prompts, FormFields fields, ViewTarget target, Func<Control, bool> filter)
    {
        if (Holds(fields, posted.Values, target))
        {
            return (fields, false);
        }

        var changed = posted.Values.TryGetValue(PlanForms.ChangeField, out var named) && named.Length > 0 ? named : null;
        var limit = ListItemPosts.Limit(posted.Values, target, fields.ShapeOf(target));
        var early = Enumerable.Range(0, fields.Count).Where(index => !fields.IsButton(index) && !fields.IsInert(index) && !fields.Listed(index) && fields.Target(index) == target && fields.Id(index) < limit).ToList();
        var before = early.Where(index => !fields.Named(index, changed)).ToList();
        var undo = before.Count == 0 ? null : Kept(fields, before);
        if (undo is not null)
        {
            var controls = before.Select(index => fields[index]).ToHashSet();
            await BindAsync(window, fields, target, posted, control => controls.Contains(control) && filter(control));
            Dispatcher.UIThread.RunJobs();
            Hosting.PageWindows.Settle(window);
            window.UpdateLayout();
            var (_, again) = HtmlCompiler.CompileWithFields(window, root, models, planning: false, overlays: prompts.Shown());
            if (Holds(again, posted.Values, target))
            {
                return (again, false);
            }
        }

        if (changed is not null && !posted.Values.ContainsKey(PlanForms.ButtonField) && early.Exists(index => fields.Named(index, changed)))
        {
            return (fields, true);
        }

        undo?.ForEach(static put => put());
        Dispatcher.UIThread.RunJobs();
        return null;
    }

    /// <summary>What puts back the values binding these fields would overwrite, or nothing when one cannot be put back.</summary>
    private static List<Action>? Kept(FormFields fields, List<int> indexes)
    {
        var undo = new List<Action>();
        foreach (var index in indexes)
        {
            if (fields.Slot(index) is { } slot)
            {
                if (slot.Bind is not null && slot.Kept is null)
                {
                    return null;
                }

                undo.AddRange(slot.Kept is { } keep ? [keep()] : []);
                continue;
            }

            var control = fields[index];
            foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(control).Where(property => Plans.LocalBindings.Of(control, property) is { } binding && Plans.BindingModes.WritesToSource(binding)))
            {
                var kept = control.GetValue(property);
                undo.Add(() => control.SetCurrentValue(property, kept));
            }
        }

        return undo;
    }

    private static bool Holds(FormFields fields, ImmutableDictionary<string, string> form, ViewTarget target) =>
        ListItemPosts.ShapeHolds(fields.ShapeOf(target), form, target) && !ListItemPosts.Stale(Enumerable.Range(0, fields.Count).Select(fields.Token), form, target);

    /// <summary>
    /// Binds the fields of the view the post names that the filter keeps and presses its button: the command's task, not
    /// awaited, once Avalonia has run the work it queued, or no task for a post without a button or a drop. The button is
    /// checked with its parameter before anything is bound; in a view where a slot renderer writes fields or buttons, a
    /// button the page does not write is looked for again in the page with the renderer's fields bound, which are put
    /// back when it is not there either, so a refused post leaves the models as they were.
    /// </summary>
    private static async ValueTask<(PostStatus Status, Task? Running, FormFields Fields)> PressAsync(Window window, Control root, ImmutableArray<object?> models, PostedForm posted, Prompts prompts, Func<Func<Control, bool>> binds)
    {
        var form = posted.Values;
        if (ControlStates.Stale(root, form))
        {
            return (PostStatus.Stale, null, new FormFields());
        }

        var undoStates = ControlStates.Restore(window, root, form);

        var (_, fields) = HtmlCompiler.CompileWithFields(window, root, models, planning: false, overlays: prompts.Shown());
        if (PlanForms.ViewOf(form) is not { } target)
        {
            return (PostStatus.BadRequest, null, fields);
        }

        var filter = binds();
        if (await HoldsItemsAsync(window, root, models, posted, prompts, fields, target, filter) is not { } held)
        {
            undoStates();
            return (PostStatus.Stale, null, fields);
        }

        if (held.ChangedOnly)
        {
            var named = form[PlanForms.ChangeField];
            await BindAsync(window, fields, target, posted, control => Enumerable.Range(0, fields.Count).Any(index => ReferenceEquals(fields[index], control) && fields.Named(index, named)));
            Dispatcher.UIThread.RunJobs();
            return (PostStatus.Rendered, null, fields);
        }

        return await PressedAsync(window, root, models, posted, prompts, held.Fields, target, filter);
    }

    /// <summary>Presses the posted button at this index: a drop target's move, a slot renderer's command, or a button's click and command.</summary>
    private static async ValueTask<(PostStatus Status, Task? Running)> PressAsync(Window window, FormFields fields, int pressed, string token, string pressedValue)
    {
        if (fields.Key(pressed) is { } key)
        {
            fields.Restore(pressed);
            Dispatcher.UIThread.RunJobs();
            var ran = AwaitedCommands.RunAsync(key.Command, key.CommandParameter).AsTask();
            Dispatcher.UIThread.RunJobs();
            return (PostStatus.Rendered, ran);
        }

        if (fields.Slot(pressed) is null && await DropAsync(fields[pressed], pressedValue[(token.Length + 1)..]) is { } dropped)
        {
            return (dropped, null);
        }

        fields.Restore(pressed);
        Dispatcher.UIThread.RunJobs();
        if (fields.Slot(pressed) is { } slot)
        {
            return PressSlot(slot, token, pressedValue);
        }

        if (fields[pressed] is not ICommandSource button)
        {
            return (PostStatus.Conflict, null);
        }

        Hosting.Realization.Measure(window);
        FlyoutPosts.From(fields[pressed]);
        DatePickerPosts.Pressed(fields[pressed]);
        if (CalendarPosts.Press(fields[pressed]))
        {
            return (PostStatus.Rendered, Task.CompletedTask);
        }

        if (button is MenuItem { Command: null } toggle)
        {
            Toggle(fields, toggle);
            return (PostStatus.Rendered, Task.CompletedTask);
        }

        var click = new RoutedEventArgs(Button.ClickEvent);
        (button as Button)?.RaiseEvent(click);
        var running = click.Handled ? Task.CompletedTask : AwaitedCommands.RunAsync(fields.Command(pressed), button.CommandParameter).AsTask();
        Dispatcher.UIThread.RunJobs();
        return (PostStatus.Rendered, running);
    }

    /// <summary>
    /// Clicks a toggle menu item as Avalonia's item does: a check box item flips, a radio item is checked and the other
    /// radio items of its group (its group name, or its parent without one) are unchecked; then its <c>Click</c> is raised.
    /// </summary>
    private static void Toggle(FormFields fields, MenuItem toggle)
    {
        if (toggle.ToggleType == MenuItemToggleType.Radio)
        {
            var group = Enumerable.Range(0, fields.Count).Select(index => fields[index]).OfType<MenuItem>()
                .Where(item => item != toggle && item.ToggleType == MenuItemToggleType.Radio
                    && (string.IsNullOrEmpty(toggle.GroupName) ? item.Parent == toggle.Parent : item.GroupName == toggle.GroupName));
            foreach (var item in group)
            {
                item.SetCurrentValue(MenuItem.IsCheckedProperty, false);
            }
        }

        toggle.SetCurrentValue(MenuItem.IsCheckedProperty, toggle.ToggleType == MenuItemToggleType.Radio || !toggle.IsChecked);
        toggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static int Find(FormFields fields, string token) => Enumerable.Range(0, fields.Count).FirstOrDefault(index => fields.IsButton(index) && !fields.IsInert(index) && fields.Named(index, token), -1);

    /// <summary>Whether the field at this index is the button a post names, read with the data context it was written with.</summary>
    private static bool Verified(FormFields fields, int index, string token, string posted)
    {
        if (index >= 0)
        {
            fields.Restore(index);
            Dispatcher.UIThread.RunJobs();
        }

        return Matches(fields, index, token, posted);
    }

    /// <summary>Whether the field at this index is the button a post names, with the parameter it posts (any move, for a drop target).</summary>
    private static bool Matches(FormFields fields, int index, string token, string posted) =>
        index >= 0 && (fields.Key(index) is { } key ? PlanForms.ButtonValue(token, key.CommandParameter) == posted : fields.Slot(index) is { } slot
            ? slot.Command is not null && PlanForms.ButtonValue(token, slot.Parameter) == posted
            : Drag.IsTarget(fields[index]) || (fields[index] is ICommandSource button && PlanForms.ButtonValue(token, button.CommandParameter) == posted));

    /// <summary>Runs a move posted to a drop target, or nothing when the pressed field is not one.</summary>
    private static async ValueTask<PostStatus?> DropAsync(Control target, string move)
    {
        if (!Drag.IsTarget(target))
        {
            return null;
        }

        return await DropMoves.RunAsync(
            step => AwaitedCommands.RunAsync(Drag.GetCommand(target), step),
            Drag.Key(Drag.GetTarget(target)),
            [.. Drag.Items(target).Select(item => Drag.Key(Drag.GetItem(item)))],
            move);
    }

    /// <summary>Runs the command of a button a slot writer wrote, when the post names the parameter it was written with.</summary>
    private static (PostStatus Status, Task? Running) PressSlot(SlotField slot, string token, string posted)
    {
        if (slot.Command is not { } command || PlanForms.ButtonValue(token, slot.Parameter) != posted)
        {
            return (PostStatus.Conflict, null);
        }

        var running = AwaitedCommands.RunAsync(command(), slot.Parameter).AsTask();
        Dispatcher.UIThread.RunJobs();
        return (PostStatus.Rendered, running);
    }

    private static async Task BindAsync(Window window, FormFields fields, ViewTarget target, PostedForm posted, Func<Control, bool> binds)
    {
        var changed = posted.Values.TryGetValue(PlanForms.ChangeField, out var named) && named.Length > 0 ? named : null;
        var order = Enumerable.Range(0, fields.Count).OrderBy(index => !fields.IsButton(index) && fields.Named(index, changed)).ToList();
        foreach (var index in order)
        {
            if (fields.IsButton(index) || fields.IsInert(index) || fields.Target(index) != target || !binds(fields[index]) || (changed is not null && fields.Slot(index) is { Composite: true } && changed != fields.Token(index)))
            {
                continue;
            }

            fields.Restore(index);
            var name = posted.Values.ContainsKey("f" + fields.Token(index)) ? "f" + fields.Token(index) : "f" + fields.BareToken(index);
            if (fields.Slot(index) is { } slot)
            {
                slot.Bind?.Invoke(posted.Values.GetValueOrDefault(name));
            }
            else if (fields[index] is FilePicker picker)
            {
                await FilePickerWriters.BindAsync(picker, window.StorageProvider, [.. posted.Files.Where(file => file.Field == name)]);
            }
            else
            {
                FieldBinders.Bind(fields[index], posted.Values.GetValueOrDefault(name));
            }
        }
    }
    /// <summary>Keeps open the overlay a post without a button came from, through the field it changed.</summary>
    private static void Changed(FormFields fields, System.Collections.Immutable.ImmutableDictionary<string, string> form)
    {
        if (form.TryGetValue(PlanForms.ChangeField, out var changed) && Enumerable.Range(0, fields.Count).FirstOrDefault(index => !fields.IsButton(index) && fields.Named(index, changed), -1) is >= 0 and var index)
        {
            FlyoutPosts.From(fields[index]);
            DatePickerPosts.Pressed(fields[index]);
        }
    }
}
