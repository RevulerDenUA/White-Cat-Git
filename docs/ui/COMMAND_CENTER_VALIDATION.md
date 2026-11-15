# Command terminal and dev integration verification — 2026-11-15

Godot 4.7.2 .NET, .NET SDK 8.0.426. Command-room checkpoint: `9d59e5e`.
Gameplay source: `origin/dev` at `f2dda94`, including the latest merged rover
preset/physics commit `76cae39`. The engine SDK remains 4.7.2.
All tests and captures use separate `XDG_DATA_HOME` directories.

- `dotnet build Jeep..csproj --no-restore`: success, zero warnings/errors.
- `tests/command/test_command_center.gd`: **134 checks, zero failures**.
  Room camera, authored monitor ray, one-click entry, terminal focus, all four
  current camera addresses, rejected removed camera, delayed movement and
  vertical controls, held-key/focus isolation, remapped keys and prompts,
  developer mode, late registration, selected camera/vehicle deletion,
  cutout visibility/bounds inside the monitor, recovery dispatch and room light
  masks. The room remains the root camera during piloting; the monitor selects
  the actual vehicle camera as its feed source. Four real streams are covered.
- `tests/command/test_physical_monitor.gd`: **79 checks, zero failures**.
  Actual terminal texture in standby, authored screen UV hit mapping, translucent
  housing halo, automatic entry focus and editing after Enter, real typed help
  and connect commands, focus after clicking output, 41 physical keyboard keys
  with press/release travel, no movement from typing, source/feed transform match,
  one shared-world stream excluding the room, transparent cutouts with no frames,
  housing and keyboard framing at 1280×720 / 2560×1080 / 1920×1440, and return to
  the authored overview with retained terminal history.
- `tests/command/test_upstream_integration.gd`: **69 checks, zero failures**.
  Old shortcut preservation when new defaults conflict, new-action conflicts
  and rebinding, delayed command isolation across vehicles, named descend
  action replacing hard-coded Shift, camera look, throttle reset, actual drill
  preview/placement on the supplied vein, real resource debit, production while
  the player is in the room/terminal, collection, hold-to-dismantle, 50% refund
  rounded down, save reload, upstream emergency kit, and WorldGen's rover adapter.
  Also checks the single-row inventory, clear centre of the feed, hidden recipe
  during ordinary driving, and two-line contextual build instructions.
- Existing frontend suite: **46 checks, zero failures**. Includes approved
  settings/rebinding/credits and the actual menu transition into `Scene/A.tscn`.
- Existing menu layout suite: **711 visible controls, zero failures**.
- Native Vulkan Forward+ captures on AMD Radeon Graphics: physical monitor
  with its keyboard and real terminal, transparent rover cutouts with compact
  inventory at 1280×720, Ingenuity at 2560×1080, and the housing hover halo.
  Current images: `previews/monitor-terminal.png`, `monitor-rover-720.png`,
  `monitor-ingenuity-wide.png`, `monitor-hover.png`.
  Earlier images preserve the integration history, including WorldGen terrain.
- Scene resource references and `git diff --check` checked during integration.
  A stale upstream rover model UID and obsolete DoorTrigger signal connections
  were removed; scene paths point to the real files.
  The incoming dev history contains whitespace warnings in planet shaders and
  the bundled freecam plugin; those unrelated files were not reformatted.

Runtime entry remains MainMenu → A. A is the shared control/drill test scene;
WorldGen is a separate terrain scene using Rover.tscn and the same terminal API.
Additional assets already present on the destination YuRa branch are preserved.

Recovery is an automatic `RecoveryRequested` event only. By user decision,
physical delivery is deferred to the shuttle stage. Planetary departure must
later require both the rover and Ingenuity physically aboard. No shuttle,
loading detector, autopilot or teleport fallback has been fabricated.

The upstream starter/emergency kit and offline production remain as supplied.
The infinite-resources settings provider is still unregistered and inactive.
Physical controller/gamepad testing was not performed. Earlier pre-integration
headless runs intermittently aborted on engine shutdown; the current suites and
native captures listed above exited normally.
