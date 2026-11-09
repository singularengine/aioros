# Benchmarks

How Aioros is measured against other stacks, and the latest results. The goal is a comparison that another person could reproduce and disagree with.

## What is measured

The same app is built with each stack: Lumen, a Linear-style issue tracker with the same pages, markup, CSS, data and behavior everywhere. Its data lives in memory, so the comparison measures the frameworks rather than a database. The implementations are in `samples/Lumen` and `bench/`.

Each stack runs in its best official configuration: a production build, in production mode, set up the way its own documentation recommends, following the rules of the [TechEmpower benchmarks](https://github.com/TechEmpower/FrameworkBenchmarks/wiki/Project-Information-Framework-Tests-Overview).

| Variant | Configuration |
| --- | --- |
| `aioros` | Precompiled plans |
| `blazor` | Blazor Web App with global Interactive Server |
| `blazor-ssr` | Static SSR with enhanced navigation |
| `nextjs` | App Router, every request rendered |
| `nextjs-cache` | App Router with Cache Components |
| `go` | Go with templ and htmx |
| `leptos` | Rust with Leptos |

## Method

- The server and the load generator run on separate, pinned CPU cores.
- Each page is warmed up before it is measured.
- Runs are interleaved, so drift on the machine spreads across all variants.
- Latency is measured open loop at fixed rates as well, to avoid coordinated omission.
- First-load numbers come from a real browser.

## Latest results

Measured on 2026-10-19, on a machine shared with other work. These are one run of the quick profile, so read them as trends. Compare ratios within the table and not against other runs.

Requests per second, closed loop:

| Page | Aioros | Blazor | Blazor SSR | Next.js | Next.js cached | Go | Leptos |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `/issues` | 31,917 | 1,852 | 2,030 | 378 | 2,254 | 9,570 | 5,370 |
| `/issues/LUM-105` | 85,620 | 5,677 | 5,898 | 814 | 4,898 | 38,347 | 14,522 |
| `/projects` | 16,378 | 5,397 | 6,030 | 521 | 3,097 | 11,904 | 6,126 |
| `/inbox` | 53,943 | 7,150 | 8,550 | 785 | 5,303 | 19,296 | 8,456 |
| `/settings` | 57,961 | 7,298 | 8,015 | 966 | 7,090 | 38,673 | 22,873 |

The rest of the picture, including where Aioros is not the best:

| Metric | Aioros | Blazor | Blazor SSR | Next.js | Next.js cached | Go | Leptos |
| --- | --- | --- | --- | --- | --- | --- | --- |
| First load, total | 99.4 KB | 140.4 KB | 55.5 KB | 171.5 KB | 242.6 KB | 28.7 KB | 365.1 KB |
| First load, JavaScript | 9.7 KB | 46.8 KB | 46.8 KB | 150.3 KB | 160.3 KB | 20.3 KB | 7.3 KB |
| First contentful paint | 64 ms | 44 ms | 56 ms | 64 ms | 60 ms | 60 ms | 52 ms |
| `/issues` p99 at 90% load | 4.46 ms | 5.22 ms | 5.66 ms | 156.17 ms | 71.78 ms | 29.04 ms | 7.92 ms |
| Save, click to confirmation | 51 ms | 13 ms | 39 ms | 43 ms | 78 ms | 34 ms | 19 ms |
| Memory idle | 98 MB | 103 MB | 102 MB | 506 MB | 456 MB | 9 MB | 10 MB |
| Memory after load | 134 MB | 160 MB | 172 MB | 1,175 MB | 881 MB | 15 MB | 23 MB |
| Cold start | 461 ms | 594 ms | 540 ms | 686 ms | 597 ms | 19 ms | 25 ms |

Go and Rust use far less memory and start far faster. A save in Aioros is slower than in most stacks.

## Startup

| Metric | JIT | ReadyToRun | Native AOT |
| --- | --- | --- | --- |
| Cold start | 463 ms | 399 ms | 121 ms |
| Memory idle | 98 MB | 97 MB | 54 MB |
| Memory after load | 142 MB | 145 MB | 77 MB |
| First save | 57 ms | 42 ms | 32 ms |

## Running it

The benchmarks run on Linux and need the toolchains of the stacks they compare.

```bash
bench/quick.sh                     # everything at reduced length, about 10 minutes
bench/app.sh                       # one run of every variant, about 12 minutes
RUNS=5 bench/app.sh                # 5 interleaved runs, for publishing
bench/startup.sh                   # JIT, ReadyToRun and Native AOT
bench/regression.sh main           # your branch against main
```

Results are written to `bench/results/`.
