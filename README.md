# calendar

Verfügbarkeits-Webapp: aggregiert mehrere Kalender zu einer anonymisierten Wochenübersicht und erzeugt daraus eine kopierbare Liste freier Terminfenster.

Live unter [calendar.raphibolliger.ch](https://calendar.raphibolliger.ch).

## Aufbau

| Verzeichnis | Inhalt |
| --- | --- |
| `Calendar.Processing/` | .NET 10 Pipeline: lädt die iCalendar-Feeds, löst Serien auf, schreibt statisches JSON |
| `Calendar.Frontend/` | Angular 22 SPA: Wochenraster, freie Slots, Kopierliste |

Die Pipeline läuft alle 15 Minuten in GitHub Actions, das Ergebnis wird zusammen mit dem Frontend auf GitHub Pages deployt. Die generierten Daten werden nie eingecheckt.

## Anonymisierung

Die öffentliche Datei enthält nur zusammengeführte Besetzt-Intervalle – ohne Titel, ohne Kalenderzuordnung, ohne Termintyp. Titel und Serien-IDs stehen ausschliesslich in einer zusätzlichen Datei, deren Dateiname den SHA-256-Hash eines Secrets enthält und die nur über `?details=<secret>` erreichbar ist.

## Lokal starten

```bash
dotnet user-secrets set "CalendarSources" '[{"id":"privat","name":"Privat","url":"https://..."}]' --project Calendar.Processing/Calendar.Processing
dotnet run --project Calendar.Processing/Calendar.Processing
npm ci --prefix Calendar.Frontend
npm start --prefix Calendar.Frontend
```

## Konfiguration

Strukturelle Defaults stehen in `Calendar.Processing/Calendar.Processing/appsettings.json`. Persönliches kommt aus Secrets:

| Secret | Inhalt |
| --- | --- |
| `CALENDAR_SOURCES` | JSON-Array der Feeds: `[{ "id", "name", "url" }]` |
| `AVAILABILITY_OVERRIDES` | JSON-Objekt, wird über die `Availability`-Sektion gemerged: `NonBlockingSeriesUids`, `NonBlockingTitlePatterns`, `AwayKeywords`, Arbeitszeiten |
| `DETAILS_SECRET` | Secret für den Detailmodus |
