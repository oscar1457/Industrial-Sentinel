# Industrial Sentinel (Pro-Version)

Industrial Sentinel ist ein Echtzeit-Monitoring-System für industrielle Maschinen mit multithreaded Telemetrie-Pipeline, MVVM-Architektur und stabilem 60-FPS-Rendering. Diese Version ist portfolio-ready und optimiert für geringe Latenz und stabile Performance auf schwächerer Hardware.

## Highlights
- Strikte MVVM-Trennung (UI, Logik, Daten).
- Entkoppelte Services: Ingestion, Processing, Persistenz, Visualisierung.
- Multithreading ohne Blockierung des UI-Threads.
- Mathematische Sensorsimulation (RPM/Temperatur/Vibration) mit Anomalien.
- Kontinuierliches Buffering gegen Latenzspitzen.
- Historische Persistenz in SQLite.
- Unit-Tests für kritische Logik.

## Architektur

```
IndustrialSentinel.sln
+- src
¦  +- IndustrialSentinel.App (WPF, MVVM, UI 60 FPS)
¦  +- IndustrialSentinel.Core (Domain, Pipeline, Alerts, Simulation)
¦  +- IndustrialSentinel.Infrastructure (SQLite, OPC-UA)
+- tests
   +- IndustrialSentinel.Tests
```

## Multithread-Pipeline
- Ingest-Thread: erzeugt Telemetrie im Ziel-Takt.
- Processing-Thread: filtert und bewertet Alerts.
- Persist-Thread: schreibt Telemetrie und Alerts in SQLite.
- UI: empfängt Events und rendert ohne Blockierung.

## Persistenz
- SQLite lokal, normalisiertes Schema und einfache Schema-Versionierung.
- Tabellen für Telemetrie, Alerts, Nutzer und Audit.
- Indizes für zeitbasierte Analyse.

## Start

```bash
# im Repo-Root

dotnet restore

dotnet build

dotnet run --project src/IndustrialSentinel.App

dotnet test
```

Die Datenbank wird in `%LocalAppData%\IndustrialSentinel\industrial_sentinel.db` erzeugt.

## Einheitliche Konfiguration (appsettings.json)
Die Hauptkonfiguration liegt in `src/IndustrialSentinel.App/appsettings.json` und wird ins Output-Verzeichnis kopiert.

- `Profile`: `simulation` oder `opcua`.
- `Database`: SQLite-Parameter (inkl. `TelemetryEnabled` für In-Memory-Betrieb).
- `System`: Pipeline-Parameter und Schwellenwerte.
- `Security`: Passwort- und Session-Policy.
- `OpcUa`: OPC-UA Verbindung.

## OPC-UA (reale Telemetrie)
Standardmäßig läuft Simulation. Für echte Daten:

1. `src/IndustrialSentinel.App/appsettings.json` öffnen
2. `Profile` auf `opcua` setzen
3. `OpcUa.Enabled` auf `true` setzen
4. `EndpointUrl` + NodeIds anpassen
5. App starten

Beispiel (Abschnitt `OpcUa`):

```
{
  "Enabled": true,
  "EndpointUrl": "opc.tcp://192.168.1.50:4840",
  "RpmNodeId": "ns=2;s=Machine1.RPM",
  "TemperatureNodeId": "ns=2;s=Machine1.Temperature",
  "VibrationNodeId": "ns=2;s=Machine1.Vibration",
  "OperationTimeoutMs": 2000,
  "SessionTimeoutMs": 60000,
  "KeepAliveIntervalMs": 10000,
  "ReconnectBaseDelayMs": 1000,
  "ReconnectMaxDelayMs": 30000,
  "ReconnectJitterMs": 500,
  "UseSecurity": false
}
```

## Performance-Hinweise
- Rendering pro Frame via `CompositionTarget.Rendering`.
- Zeichnen optimiert über `StreamGeometry`.
- Endliche Queues verhindern Backlog.

## Tastenkürzel
- `Ctrl+S`: Pipeline starten
- `Ctrl+Shift+S`: Pipeline stoppen
- `Ctrl+L`: Logs leeren
- `Ctrl+H`: Health-Report exportieren
- `Ctrl+M`: Kompaktmodus

## Diagnostik
- Statusleiste: Queue, Raten, Latenz, FPS, RAM, Persistenzstatus.
- `Report` erzeugt JSON unter `%LocalAppData%\IndustrialSentinel\reports`.

## Tests
- AlertServiceTests
- TelemetrySimulatorTests
- RingBufferTests
- PipelineTests (Start/Stop)
- SqliteSmokeTests (Schema/Nutzer)

## Sicherheit (Standard)
- Lokale Nutzer mit Rollen (Admin / Operator / Viewer).
- Starke Passwörter (min. 12 Zeichen, Groß/Klein/Zahl/Symbol).
- Lockout bei Fehlversuchen, Session Timeout.
- Audit-Log mit Hash-Kette.
- Passwortrotation via `PasswordMaxAgeDays`.

### Admin-Panel
- Nutzerverwaltung (Anlegen/Reset/Entsperren).
- Audit-Export als CSV.
- Passwortwechsel für aktuellen Nutzer.

### Erststart
1. App starten und Admin im Login erstellen.
2. DB wird automatisch in `%LocalAppData%` angelegt.

### Security-Konfiguration
`src/IndustrialSentinel.App/appsettings.json` (oder `security.json` falls kein appsettings genutzt wird).

## Packaging für GitHub Releases

### Publish
```
./scripts/publish.ps1 -Configuration Release -Runtime win-x64 -SelfContained
```

### ZIP + Setup (Inno Setup)
```
./scripts/pack.ps1 -Configuration Release -Runtime win-x64 -SelfContained -CreateInstaller
```

Voraussetzung: Inno Setup 6 installiert.

### Automatische Releases
Der Workflow `/.github/workflows/release.yml` erzeugt ZIP + Setup bei Tags:

```
git tag v1.0.0

git push origin v1.0.0
```

Hinweis: `MyAppURL` in `install/IndustrialSentinel.iss` auf dein Repo setzen.
