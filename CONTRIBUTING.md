# Contributing

Thanks for your interest in Aioros. This page covers how to build it, how to run the tests and what a change needs. The complete rules are in [AGENTS.md](AGENTS.md).

> [!NOTE]
> Aioros is in public preview, and its APIs are still moving. For anything larger than a small fix, please open an issue first so we can agree on the approach.

## Build and test

The repository pins its SDK in `global.json`. If you do not have that version, `eng/install-sdk.sh` installs it into `.dotnet/`. The browser scripts are minified with esbuild, which `eng/install-scripts.sh` installs and which needs Node 22 or later.

```bash
eng/install-sdk.sh
eng/install-scripts.sh
dotnet build Aioros.slnx
dotnet test --solution Aioros.slnx
```

Browser tests expect Chromium at `/usr/bin/chromium`, or at the path in `AIOROS_CHROMIUM`.

## What a change needs

**Acceptance criteria first.** Each behavior is described in the area's file in [docs/acceptance](docs/acceptance), in Given, When and Then form, with an identifier that is never reused.

**One test per criterion, written before the code.** The test carries its criterion with `[Trait("AC", "ROUTING-01")]`, and you should watch it fail before making it pass. The architecture tests fail the build if a criterion has no test, or two.

**No sleeps in tests.** Await the event that says the thing happened, and use a fake `TimeProvider` for behavior that is about time.

**The architecture rules.** Every class is `sealed` or `static`, there are no locks or comments, and warnings are errors. The tests explain themselves when a rule is broken: fix the cause rather than working around it.

**Writer changes need the compatibility bench.** A change to what a control writes can shift themes that only `bench/compat/run.sh` covers.

## Reporting bugs

Please include the smallest XAML that shows the problem, what you expected and what you got. For security issues, follow the [security policy](SECURITY.md) instead of opening an issue.
