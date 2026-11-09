# Controls: ranges and dates

Sliders, progress bars, scroll bars, calendars and date and time pickers (`src/Adapters/Aioros.Avalonia/Html/Families/RangesFamily.cs`). Each control is the native element the web has for it (`<input type="range">`, `<progress>`, `<input type="date">`, `<input type="time">`) with the Fluent look read from the realized template. Fluent's calendar is a static table of the month Avalonia computed; a calendar of another theme is written through its template, its navigation, caption and days posting their clicks and its arrows moving the focus in the keyboard module.

39 criteria. Each has an identifier and exactly one test.

## Range controls

### RANGES-01: Sliders are range inputs

- **Given** a `Slider`
- **When** it is rendered
- **Then** it is an `<input type="range" name="f{id}">` with its `min`, `max` and `value` (a slot when `Value` is bound), `step="any"` unless it snaps to ticks (then its `TickFrequency`), its value also as the `--v` property its fill is drawn from (which the framework's script follows while the thumb moves), and `disabled` when it is not enabled.

### RANGES-02: Posted numbers move sliders

- **Given** a `Slider`
- **When** a post carries its field as a number
- **Then** `Value` becomes that number read with the invariant culture
- **When** the field is missing or not a number
- **Then** `Value` is unchanged.

### RANGES-03: Sliders wear the fluent track and thumb

- **Given** a `Slider`
- **When** it is rendered
- **Then** its style sheet draws the track, the value fill and the round thumb with the colors and sizes of its realized Fluent template (the input as tall as Avalonia lays the slider out, 50 px; the track 2 px with its radius, filled from `--v` up to the thumb's center; the thumb 20 px), and a vertical slider is laid out vertically.

### RANGES-04: Progress bars are progress elements

- **Given** a `ProgressBar`
- **When** it is rendered
- **Then** it is a `<progress>` whose `max` is `Maximum - Minimum` and whose `value` is `Value - Minimum` (a slot when bound), and with `ShowProgressText` its text formatted by `ProgressTextFormat` is written over the bar; a bound value outside `Minimum` and `Maximum` is clamped to them in the bar and its text, as Avalonia's `RangeBase` coerces it (AdvancedToDoList's progress of -1 shows 0%).

### RANGES-05: Indeterminate progress bars animate

- **Given** an indeterminate `ProgressBar`
- **When** it is rendered
- **Then** it is a `<progress>` without a value and its style sheet animates the indicator with CSS.

### RANGES-06: Calendars are month tables

- **Given** a `Calendar` in month mode
- **When** it is rendered
- **Then** it is its month header followed by a `<table>` whose head row holds the day names and whose body holds the six weeks Avalonia computed, with the selected day `aria-selected="true"`, days of other months marked inactive and today marked; a bound `SelectedDate` or `DisplayDate` makes the page render literally.

### RANGES-07: Year and decade calendars are tables of four

- **Given** a `Calendar` in year or decade mode
- **When** it is rendered
- **Then** it is its header followed by a `<table>` of the months or years Avalonia computed, four per row, with the selected one `aria-selected="true"`.

### RANGES-08: Calendar date pickers are date inputs

- **Given** a `CalendarDatePicker`
- **When** it is rendered
- **Then** it is an `<input type="date" name="f{id}">` whose value is `SelectedDate` as `yyyy-MM-dd` (a slot when bound) and whose `min` and `max` are `DisplayDateStart` and `DisplayDateEnd`.

### RANGES-09: Posted dates set calendar date pickers

- **Given** a `CalendarDatePicker`
- **When** a post carries its field
- **Then** a `yyyy-MM-dd` value sets `SelectedDate` and an empty value clears it
- **When** the field is missing or not a date
- **Then** `SelectedDate` is unchanged.

### RANGES-10: Date pickers are date inputs

- **Given** a `DatePicker`
- **When** it is rendered
- **Then** it is an `<input type="date" name="f{id}">` whose value is `SelectedDate` as `yyyy-MM-dd` (a slot when bound), limited to the years from `MinYear` to `MaxYear`.

### RANGES-11: Posted dates set date pickers

- **Given** a `DatePicker`
- **When** a post carries its field
- **Then** a `yyyy-MM-dd` value sets `SelectedDate` keeping its offset and an empty value clears it
- **When** the field is missing or not a date
- **Then** `SelectedDate` is unchanged.

### RANGES-12: Time pickers are time inputs

- **Given** a `TimePicker`
- **When** it is rendered
- **Then** it is an `<input type="time" name="f{id}">` whose value is `SelectedTime` as `HH:mm` (`HH:mm:ss` with `UseSeconds`, a slot when bound) and whose `step` is its `MinuteIncrement` in seconds (its `SecondIncrement` with `UseSeconds`).

### RANGES-13: Posted times set time pickers

- **Given** a `TimePicker`
- **When** a post carries its field
- **Then** an `HH:mm` or `HH:mm:ss` value sets `SelectedTime` and an empty value clears it
- **When** the field is missing or not a time
- **Then** `SelectedTime` is unchanged.

### RANGES-14: Scroll bars are the browsers

- **Given** a `ScrollBar` in either orientation
- **When** it is rendered
- **Then** nothing is written for it: the browser draws the scroll bars of the scrolling element, inside a `ScrollViewer` or anywhere else.

### RANGES-15: Slider ticks are drawn across the track

- **Given** a `Slider` with a `TickPlacement`
- **When** it is rendered
- **Then** its style sheet draws a tick every `TickFrequency` across the track on the placed sides, in the `TickBar` fill of its template.

### RANGES-16: Sliders draw their themes track and thumb

- **Given** a `Slider` whose control theme is 6 px high with a 6 px track rounded 3 px, filled `#171717` over `#f5f5f5`, and a 16 px white `Thumb` whose template border has a 1 px `#171717` border, an 8 px radius and a small shadow, which a `:pointerover` style and a class bound to the slider's `IsKeyboardFocusWithin` give a 4 px ring (Aioros.Cosmo's)
- **When** the page is rendered
- **Then**
  - Its range input is 6 px high with no overflow clipping, its track 6 px rounded 3 px with the two fills, its thumb 16 px with that border, radius and shadow (centered on the track, overflowing the input as Avalonia's does), and its `:hover` and `:focus-visible` rules give the thumb the ring, read with the thumb pointed at and the slider focused (really, or, where the page is written without a focusable window, with the thumb's classes bound to the slider's keyboard focus set as the binding would set them).
  - A two-thumb range is not written: Avalonia's `Slider` holds one `Value`.

### RANGES-30: Two sliders marked as a range share one panel

- **Given** a panel marked `a:Web.Range` holding two `Slider`s, the lower and the upper value of one range (as Aioros.Cosmo's `c:RangeSlider` template does)
- **When** it is rendered
- **Then** the panel is written with `data-aioros-range` and a hidden `_range` field naming the two sliders' fields in order, each input passes the pointer to its thumb only (`pointer-events:none` on the input, `auto` on its thumb) so either thumb can be dragged without the script, and the panel takes no touch scrolling (`touch-action:none`, Radix's `touch-none`); a panel without the hint, or marked but holding other than two sliders, is written as before.

*Precedent: the two overlaid native range inputs of the multi-thumb range without script, Radix Slider and the WAI-ARIA multi-thumb slider.*

### RANGES-32: A crossed range is bound in order

- **Given** a post that carries a range's two fields crossed (the lower at 900, the upper at 300, as two thumbs dragged past each other without the script) with its `_range` field
- **When** the post is bound
- **Then** the lower slider takes 300 and the upper 900, and the `_range` field reaches no control; with the values in order, or without the `_range` field, each field is bound as it came.

### RANGES-33: A band inset from the end leaves the end clear

- **Given** a `Slider` whose theme draws the band of its increase part inset from the track's end (a margin on the band, as Aioros.Cosmo's range draws its lower slider's band short of the end its upper slider's band covers)
- **When** it is rendered
- **Then** the track's fill leaves that end clear by the inset, so in Chromium, as on the desktop, a band drawn over it shows its own color alone at that end, with no antialiased edge of the band below; a band that reaches the end is written as before.

## Calendar theme

### RANGES-17: Themed calendars are written through their template

- **Given** a `Calendar` whose control themes are not Fluent's (a theme shaped as Aioros.Cosmo's: a padded border holding a `CalendarItem` whose template lays a 32 px header row of a previous button, a header button and a next button over a `PART_MonthView` of seven 32 px columns and a `PART_YearView`, day titles 12.8 px in a muted color, day buttons 32 px with a top margin of 8 whose template is a `PART_Band` border around a `PART_Root` border), showing June 2025 with 12 June selected
- **When** it is rendered
- **Then** it is written through its template: an element marked `data-aioros-calendar` with its border's background and padding, the previous and next buttons as posting buttons with their content and look, the header button with the caption Avalonia wrote, and the month as a `<table role="grid">` whose head row holds the seven day titles (`<th scope="col">`, each as wide as its grid column) in their template's font and color, followed by its weeks.

*Precedent: react-day-picker as shadcn/ui's `Calendar` renders it (a `nav` of previous and next buttons, a caption, a `table role="grid"` with a head row of weekdays); the WAI-ARIA date picker dialog's grid.*

### RANGES-18: Themed days take the look of their states

- **Given** the calendar of RANGES-17 with a blackout range from 1 to 10 June
- **When** it is rendered
- **Then** each of the 42 days of the month view is a `<button type="submit" name="_btn">` in a `<td role="gridcell">`, inside an element in the band's box (`PART_Band`: 32 px, 8 px above), the button in the look `PART_Root` takes in the day's states as Avalonia resolves them and holding the day's number as its own text (so a selector by the button's text, Playwright's `button:text-is('18')`, finds it): the selected day in its `:selected` look (`#171717` and `#fafafa`) with `aria-selected="true"` on its cell, the other months' days in their `:inactive` color, the blackout days in their `:blackout` color, the pointed-at look a rule under the button's `:hover`; a day outside `DisplayDateStart` and `DisplayDateEnd` is `disabled` at opacity 0, as Avalonia hides it; the Fluent calendar stays the table of RANGES-06.

### RANGES-19: Ranges keep the marks their library gives their days

- **Given** the calendar of RANGES-17 in `SingleRange` mode from 9 to 26 June, whose library code (shaped as Aioros.Cosmo's `CalendarDays`) marks its days `range-start`, `range-middle` and `range-end`, rounds a middle day only at the ends of its week and fills the `PART_Band` of the first and last days with the band's color rounded on their outer side
- **When** it is rendered
- **Then** those classes and values are written as Avalonia shows them: the middle days in the `range-middle` look (`#f5f5f5`), square inside a week and rounded on the outer side at its ends, the first and last days in the selected look over a band filled `#f5f5f5` and rounded on their outer side, and the grid `aria-multiselectable="true"`.

### RANGES-20: Hidden weeks and outside days keep their buttons

- **Given** the calendar of RANGES-17 whose library code hides the weeks that hold no day of the shown month (June 2025's first week, wholly in May) and, with its class `outside-hidden`, a theme that makes the other months' days not hit-testable with a transparent band
- **When** it is rendered
- **Then** a hidden week is a `<tr hidden>` whose seven buttons are still written, so every month numbers its buttons alike, and each other month's day is `tabindex="-1" aria-hidden="true"` at opacity 0 and without pointer events.

### RANGES-21: One day is the tab stop

- **Given** calendars of RANGES-17 with a selected day in the shown month, with a selected day in another month, and without a selection showing the current month
- **When** they are rendered
- **Then** exactly one day button of each is in the tab sequence and every other is `tabindex="-1"`: the selected day, else today, else the first day of the month, as the WAI-ARIA grid's roving tab stop.

*Precedent: the WAI-ARIA date picker dialog (one focusable day, the selected one or today); react-day-picker's `focused` day.*

### RANGES-22: Year and decade views are tables of their buttons

- **Given** the calendar of RANGES-17 in `Year` mode and in `Decade` mode
- **When** it is rendered
- **Then** its `PART_YearView` is a `<table role="grid">` of the twelve month or year buttons four per row, each in its theme's look with the selected one's, and the month view is still written as a `<table hidden>` without the grid role, its 42 buttons in it (as is the year view in month mode, so a query of the shown grid never reaches the hidden one), so the buttons after the calendar keep their numbers in every mode.

### RANGES-23: Calendars keep what they show in a hidden field

- **Given** the calendar of RANGES-17
- **When** it is rendered
- **Then** it holds a hidden field `<input type="hidden" name="f{id}">` whose value names the date it shows (`DisplayDate` in month mode, the shown year or decade otherwise) as `yyyy-MM-dd`, its display mode the first day of a tapped range still waiting for its last, the modifiers and the selected days (`2025-06-12|Month|||`, ENGINE-277); a calendar whose `SelectedDate` or an attached property of a library (`CalendarSelection.RangeStart`) is bound is planned with its field computed for each render (`{shown}|Month|||{selected days}`, RANGES-36), and a page with a calendar binds its posts through Avalonia, whose answer shows what the post changed.

### RANGES-34: Week texts are row headers before their days

- **Given** the calendar of RANGES-17 whose library code puts a text before each week's days and an empty one over the weekdays, in a column it inserts before the days (which it moves a column, as Aioros.Cosmo's `week-numbers` class shows react-day-picker's `showWeekNumber`)
- **When** it is rendered
- **Then** the head row starts with an empty `<th>` in that column's width before the seven weekdays and each shown week's row starts with a `<th scope="row">` holding its text before its seven days, and in Chromium every week text stands where Avalonia places it on the desktop, by the center its column's alignment fixes and its height against its row's days, within 1 px (its left edge follows the font's glyph widths, a pixel apart between Chromium builds, as LAYOUT-83 found)
- **When** the library leaves those texts hidden
- **Then** neither cell is written (the head row is the seven weekdays and each row the seven days, as RANGES-17).

*Precedent: react-day-picker 9's `WeekNumber` cell (`<th scope="row">` in each week's row, an empty `<th>` over the weekdays) and HTML's row headers in a data table.*

## Date picker theme

### RANGES-24: Themed pickers are written through their template

- **Given** a `CalendarDatePicker` whose control theme is not Fluent's (a theme shaped as Aioros.Cosmo's: an outline border holding a calendar icon and a text box that takes no input, a `PART_Button` covering it, a `PART_Popup` 4 px below its start holding a bordered calendar), with `SelectedDate` 12 June 2025 shown as `MMMM d, yyyy`
- **When** it is rendered
- **Then** it is written through its template: an element anchoring the popover with the border in its look and the text box showing `June 12, 2025`, `PART_Button` as a `<button type="button" popovertarget>` that opens the popover in the browser without a post, and the popover (`<div popover role="dialog">`) holding the popup's border and its calendar written as RANGES-17 writes calendars, placed below the picker's start 4 px away as Avalonia's positioner places it; a Fluent picker stays the `<input type="date">` of RANGES-08.

*Precedent: shadcn/ui's date picker (a `Popover` whose `PopoverTrigger` is an outline button with the date, its content a `Calendar`); HTML's popover attribute and CSS anchor positioning.*

### RANGES-25: Empty pickers show their placeholder

- **Given** the picker of RANGES-24 without a `SelectedDate`
- **When** it is rendered
- **Then** its text box shows the theme's placeholder (`Pick a date`) in the theme's placeholder color.

### RANGES-26: Input pickers are fields with an end button

- **Given** a picker in the theme's `input` variant (the text box takes input, a 24 px button at the field's end opens the popup, placed `BottomEdgeAlignedRight` with offsets 8 and 10), with `SelectedDate` 1 June 2025 shown as `MMMM dd, yyyy`
- **When** it is rendered
- **Then** the text box is a field (`<input name="f{id}" data-change>`, posted when it changes, as the picker parses its text when it loses the focus) holding `June 01, 2025`, the end button opens the popover, and the popover is placed below the picker's end as Avalonia places it.

### RANGES-27: Bound pickers render literally

- **Given** a themed picker whose `SelectedDate` is bound
- **When** the page is compiled
- **Then** the page renders literally (its plan fails naming the picker), and its posts are bound through Avalonia.

### RANGES-28: Literal pickers are planned

- **Given** themed pickers of RANGES-24 whose `SelectedDate` is a literal or unset (their calendar's `SelectedDate` bound to the picker through its template, as Aioros.Cosmo's theme binds it)
- **When** the page is compiled
- **Then** the plan is reusable and writes what Avalonia writes, popover and calendar included: a calendar's bindings to its templated parent read no model; a picker whose own date is bound still renders literally (RANGES-27).

### RANGES-29: Themed pickers take their parts looks when pointed at

- **Given** a themed picker of RANGES-24 whose theme gives its template's parts other looks while the picker is pointed at (its root border `#f5f5f5` under `:pointerover`, its icon `#2563eb`, as Semi.Avalonia's picker turns its field gray and its icon blue)
- **When** it is rendered
- **Then** the picker's element carries a class whose rules give those parts those looks under `:hover` (`#f5f5f5` on the root's element, `#2563eb` on the icon's), as Avalonia shows them, a part styled in two states (the root's border `#a3a3a3` under `:focus-within` too) keeping both rules' classes, and nothing else changes at rest; the same for `:active` and `:focus-within` where the theme styles `:pressed` and `:focus-within`.

### RANGES-35: A post from a pickers select keeps it open

- **Given** a themed picker (RANGES-24) whose template's popup holds a `ComboBox` above its calendar (shaped as Aioros.Cosmo's presets)
- **When** a post comes from that combo box's change (`_chg` naming its field)
- **Then** the answer writes the picker's popover open (`data-aioros-open`), as a post from its month buttons does
- **When** a post selects a day
- **Then** it is written closed, as Avalonia closes the picker on a day.

## Range script

### RANGES-31: A range takes the pointer and the keys as radix does

Given that range in Chromium with the framework's script, the sliders from 0 to 1000 by 10 at 200 and 800, when the pointer presses the panel at 700 and drags to 750, presses at 100, drags the lower thumb past the upper one, and when Right, Shift+Right, PageUp and PageDown, Home and End are pressed on a thumb and the lower one is stepped past the upper one, then, as Radix Slider does, a press moves the closest thumb and drags it, the values stay ordered (a thumb moved past the other becomes the upper and keeps the focus), Right moves by one step, Shift with an arrow and the page keys by ten, Home moves the lower thumb to the minimum and End the upper to the maximum, every value snaps to the step, and each change fires the inputs' `input` and `change` events so their fills and the form follow.

## Calendar plan

### RANGES-36: Bound calendars are planned for every month and day

- **Given** the themed calendar of RANGES-17 in `SingleDate` mode whose `DisplayDate` and `SelectedDate` are bound to the model
- **When** the page is compiled
- **Then** its plan is reusable and, for models showing June 2025, February 2026 and December 2024 with the selected day in the shown month, on a day of another month shown, unset and on today, writes the bytes Avalonia writes for each, as does the plan read back from a plans file: the caption, the six weeks (another month's days, today, the selected day, the tab stop), the buttons numbered alike and the hidden field; with only `SelectedDate` bound, the plan shows the month of the day it renders, as Avalonia shows today's.

### RANGES-37: Bound ranges and days are planned with their librarys marks

- **Given** the themed calendar with its library's marks (RANGES-19: weeks without a day of the month hidden, a range's days marked `range-start`, `range-middle` and `range-end`, its band rounded at each week's ends) in `SingleRange` mode with the library's attached `RangeStart` and `RangeEnd` bound, and in `MultipleRange` mode with its `Dates` bound
- **When** each page is compiled
- **Then** the plan is reusable and writes Avalonia's bytes for ranges within a week, across weeks and months, starting and ending on a week's ends, of one day, with `RangeEnd` unset and empty, and for several days, consecutive or not, in and out of the shown month.

### RANGES-38: Bound calendars are planned in each culture with their week numbers

- **Given** the bound calendar of RANGES-36 with its library's week numbers (RANGES-34), compiled from an `en-US` caller and from an `fr-FR` caller (weeks from Monday, French names)
- **When** each plan renders December 2025, January 2026, December 2020 and January 2021
- **Then** it writes what Avalonia writes in its culture (the caption, `janvier 2026` in French, the day titles, each week's number in the library's rule), and in Chromium the planned January places its days and week numbers within 2 px of where Avalonia places them on the desktop.

### RANGES-39: Bound calendars are planned with their caption selects

- **Given** a bound themed calendar whose template's caption is a month select and a year select bound to a library's attached properties (shaped as Aioros.Cosmo's `CalendarCaption`: the culture's abbreviated month names and the years from a hundred before today to today, widened to the shown year)
- **When** the page is compiled
- **Then** the plan is reusable and writes Avalonia's bytes for June 2025, for January of this year, for a year 120 years ago and for one three years ahead: the shown month and year selected among the years computed for the render; with the library's `plain` class, which shows the caption's label and hides the selects (as Aioros.Cosmo's calendars without `dropdown`), the plan writes the label computed for each render and learns nothing from the hidden selects.
