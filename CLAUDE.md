# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

Two independent sub-projects, no shared tooling at the root:

- **`Calendar.Frontend/`** — Angular 22 SPA (zoneless, signals, Tailwind v4) that renders a week grid of anonymized busy blocks, marks the free slots and generates a copyable list of free time windows.
- **`Calendar.Processing/`** — .NET 10 console app that downloads the iCalendar feeds, expands recurrences, classifies every occurrence and writes the static JSON the frontend consumes.

Deployment runs via `.github/workflows/build-and-deploy.yml` ("Build and Deploy", on push to `main`, every 15 minutes via cron, or manual dispatch), live at https://calendar.raphibolliger.ch. The workflow runs the pipeline on the runner, builds the frontend with the freshly generated data and deploys to GitHub Pages. **The generated data is never committed** — it is gitignored and only exists locally (after a local pipeline run) or inside a workflow run.

## Anonymization is the point of this project

The public JSON contains merged busy intervals with start and end only. No titles, no calendar names, no event types, no attendees. Anything that identifies an appointment belongs exclusively in the capability-gated details file. When adding fields to `AvailabilityPayload`, assume the file is read by everyone the link is shared with.

Feed URLs and the personal override lists live in repository secrets, never in the repo.

## Processing (`Calendar.Processing/`)

Commands (run from the repository root, so the frontend directory is found):

- `dotnet run --project Calendar.Processing/Calendar.Processing` — runs the full pipeline.

What it does:

1. Loads every feed in `CalendarSources`, rewriting `webcal://` to `https://`.
2. `IcsSanitizer` unfolds the feed and aligns mismatched `DTSTART`/`DTEND` value types. The published iCloud feed contains events where `DTSTART` is a `DATE-TIME` and `DTEND` a `DATE`; Ical.Net throws on those when it computes the effective duration, which would abort the whole run.
3. `OccurrenceReader` expands each event individually (not via `Calendar.GetOccurrences`) so that one broken event cannot kill the run and so unbounded series stay lazy — `TakeWhile` ends the enumeration at the window end. It searches from 120 days before the window to catch multi-day events that started earlier, then drops everything that ends before the window.
4. Ical.Net does not apply `RECURRENCE-ID` overrides itself. The reader collects all events carrying a recurrence identifier and skips the master's occurrence at that instant, so a moved instance is not counted twice.
5. Classification per occurrence, first match wins: cancelled → ignored; UID in `NonBlockingSeriesUids` → soft; title matches a `NonBlockingTitlePatterns` regex → soft; `TRANSP:TRANSPARENT` or `X-MICROSOFT-CDO-BUSYSTATUS:FREE` → soft; all-day with an `AwayKeywords` hit → away (blocks the whole day), all-day without → soft; everything else → blocking.
6. Timed blocking intervals are merged, soft intervals are merged and then have the blocking ones subtracted, and both are clipped to the daily `DisplayHours` window. All-day occurrences become `awayDays` (unavailable) or `markedDays` (informational).
7. Writes `availability-<guid>.json`, `details-<guid>-<sha256(secret)>.json` and `data-manifest.json` into `Calendar.Frontend/public/data/`, deleting the previous generation first.

**Free slots are deliberately not computed here.** The frontend lets the user change working hours, buffer and minimum length live, so the slot math has to run client side; duplicating it in the pipeline would only create drift. The pipeline ships the defaults in the payload instead.

### Configuration

`appsettings.json` holds structural defaults only (working hours, slot sizes, horizon, display window, generic away keywords). Everything personal comes from configuration at runtime — user secrets locally (`UserSecretsId` is `calendar-processing`), environment variables in CI:

- `CalendarSources` — required, JSON array of `{ "id", "name", "url" }`.
- `AvailabilityOverrides` — optional, JSON object deep-merged over the `Availability` section of `appsettings.json`. This is where `NonBlockingSeriesUids`, `NonBlockingTitlePatterns` and extra `AwayKeywords` belong.
- `DetailsSecret` — optional; without it no details file is written.

In the workflow the repository secrets `CALENDAR_SOURCES`, `AVAILABILITY_OVERRIDES` and `DETAILS_SECRET` are mapped onto those names.

Set locally with `dotnet user-secrets set "CalendarSources" '[...]'` (run from `Calendar.Processing/Calendar.Processing/`).

## Frontend (`Calendar.Frontend/`)

Commands (run from `Calendar.Frontend/`):

- `npm start` — dev server at `http://localhost:4200`. Needs a local pipeline run first, otherwise `data/data-manifest.json` 404s.
- `npm run build` — production build into `dist/calendar-frontend/`.

Stack & conventions:

- **Angular 22, zoneless, standalone components, signals throughout** (`signal`, `computed`, `httpResource`). Don't introduce `NgZone`-dependent patterns or `async` pipes for state.
- **Tailwind v4** via `@tailwindcss/postcss` (`.postcssrc.json`), `@use "tailwindcss"` in `styles.scss`.
- `AvailabilityStore` chains three `httpResource`s: manifest → availability, manifest + hash → details. A cache-busting `t` parameter on the manifest request is bumped every five minutes so a running tab picks up newer data.

### All times are Europe/Zurich wall clock

The grid must show the same hours no matter where the browser is. `zurich.ts` converts every ISO timestamp into a `{ dayKey, minutes }` wall clock via `Intl.DateTimeFormat` with the fixed zone, and all slot math in `slots.ts` runs on minutes-within-a-day. Because the pipeline already clips busy blocks to the daily display window, no block crosses midnight and no `Date` arithmetic across DST boundaries is needed. Keep it that way — day keys are ISO strings and compare correctly with `<`.

### The details are gated by a secret (capability URL)

The details filename ends with the SHA-256 hex hash of `DetailsSecret`. The frontend only knows the `details-<guid>` prefix from the manifest; it derives the hash from the `?details=<secret>` query parameter at runtime via Web Crypto and requests the resulting URL. Without the secret the URL cannot be constructed, and GitHub Pages does not list directory contents. A secret that loads successfully is persisted in `localStorage` (`detailsSecret`); a failing request removes it again.

With details unlocked, the grid labels each block with its title and calendar, and a table of all recurring series appears below the slot list. Selecting series there produces the `NonBlockingSeriesUids` JSON snippet for the `AVAILABILITY_OVERRIDES` secret — that is the intended workflow for marking a series as non-blocking.

## Cross-cutting notes

- This is a personal hobby project; there is no test coverage yet.
- The scheduled run is `*/15 * * * *`, but GitHub throttles scheduled workflows under load, so the real interval varies.
- The published Outlook feed for the business calendar currently returns very little data and is cached by Microsoft; switching that source to Microsoft Graph is the known next step.
