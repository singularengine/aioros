# Platform profile

`Aioros.Platform` (`src/Platform/Aioros.Platform`): `AppBuilder.UsePlatformProfile` detects where an Avalonia app runs and applies the settings that make it work well there. It depends on Avalonia only, so any Avalonia app can use it; the Aioros desktop host does. Each rule answers a problem reproduced and measured, names its cause and is removed when the cause is fixed (see the workarounds in docs/architecture.md).

3 criteria. Each has an identifier and exactly one test.

### PLATFORM-01: The session decides the rules

- **Given** the environment and the operating system
- **When** the profile is detected
- **Then**
  - A Linux session with a Wayland display draws through Avalonia's Wayland backend with the dispatcher clock aligned; a Linux session without one, or with `AIOROS_DESKTOP_X11=1`, draws through X11 with no rule
  - Windows and macOS keep Avalonia's own platform with no rule; and the compositor (`XDG_CURRENT_DESKTOP`) is reported when known.

### PLATFORM-02: The profile describes what it decided

- **Given** a detected profile
- **When** it is described
- **Then** one line names the operating system, the windowing system, the compositor when known, and each rule applied with its cause (`platform: linux wayland (Hyprland) · wayland backend · dispatcher clock (Avalonia #22064)`), so a developer sees what the framework decided.

### PLATFORM-03: The dispatcher clock follows the platform

- **Given** the UI dispatcher of a running app
- **When** the dispatcher clock rule is applied (the workaround for Avalonia issue 22064, which made every timer fire 120 to 260 ms late on Wayland)
- **Then** the dispatcher's clock reads the platform's own clock, so timers and animations run on time; the rule does nothing when a later Avalonia no longer has the fields it corrects.
