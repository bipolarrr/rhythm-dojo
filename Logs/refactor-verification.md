# Refactor verification — 2026-09-28

Unity 6000.3.23f1, Windows x64. Package versions retained.

| Check | Result | Evidence |
| --- | --- | --- |
| Domain/Application + Unity Edit Mode | 42 passed, 0 failed | [NUnit XML](refactor-editmode.xml), [log](refactor-editmode.log) |
| Unity Play Mode | 4 passed, 0 failed | [NUnit XML](refactor-playmode.xml), [log](refactor-playmode.log) |
| Scene regeneration | Bootstrap, SongSelection, Gameplay recreated and repeated hierarchy stable | [integration results](verification.txt) |
| Original mixed run | 13 Perfect / 2 Good / 3 Miss; combo 3; timestamped inputs through a 180ms hitch | [integration log](refactor-foundation.log) |
| Original restarted run | 18 Perfect / combo 18, including held-key restart | [integration results](verification.txt) |
| Original no-input run | 18 Miss, including hold starts | [integration results](verification.txt) |
| Tempo Shift, Constant | 8 Perfect / combo 8 | [integration results](verification.txt) |
| Tempo Shift, BPM | 8 Perfect / combo 8; hold across the 6s tempo change | [integration results](verification.txt) |
| Windows player | Build succeeded; Null-graphics startup without managed exceptions | [build/integration log](refactor-foundation.log), [startup log](refactor-player.log) |
| Visual checks | Gameplay and selection captures inspected | [Gameplay](playmode.png), [selection](selection.png) |

Play Mode coverage includes an actual virtual mouse click through InputSystemUIInputModule, keyboard Submit/Navigate, independent difficulty/scroll selection, duplicate starts, two-song transitions, Escape cleanup, focus reset, and a six-lane definition driving lane input, HUD indicators and playfield rails. The six-lane test restores its temporary catalog changes.

Inspected managed player assembly references:

- Game.Domain: netstandard only.
- Game.Application: netstandard and Game.Domain only.
- Game.Runtime: Domain/Application, netstandard, UnityEngine modules, UnityEngine.UI and Unity.InputSystem; no UnityEditor.
- Shipped game assemblies: Game.Domain.dll, Game.Application.dll and Game.Runtime.dll. No Game.Editor or Game.*.Tests assemblies.

The player used for startup inspection was stopped after the check. These automated checks do not measure hardware/audio latency. Full-song profiling, lane cursors, active ranges and View pooling remain deferred until authored musical content is available.
