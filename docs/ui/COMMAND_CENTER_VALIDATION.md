# Command terminal verification — 2026-11-15

Godot 4.7.2 .NET, .NET SDK 8.0.426. Base revision before this work: `d615be6`.
Tests use separate `XDG_DATA_HOME` directories; personal settings are untouched.

- `dotnet build Jeep..csproj --no-restore`: success, zero warnings/errors.
- `tests/command/test_command_center.gd`: **143 checks, zero failures**, clean
  exit in the final headless run. Tests cover room camera, real monitor ray hit,
  one-click terminal entry, text focus, five camera addresses, immediate control,
  held-key isolation, focus-loss disarming and immediate HUD updates,
  real movement/vertical input, invalid addresses, remapped
  movement/camera keys and prompts, developer camera gating, late registration,
  removed camera/vehicle, reference-image ownership/visibility/bounds, recovery
  event dispatch and room light masks.
- Existing frontend suite: **46 checks, zero failures**, including settings,
  key rebinding, credits and the actual menu transition into `Scene/A.tscn`.
- Existing menu layout suite: **711 visible controls, zero layout failures**.
  This suite reports two ObjectDB instances / one resource still in use at exit;
  the approved menu/credits implementation was not rewritten to mask that warning.
- Real Vulkan Forward+ captures on AMD Radeon Graphics: room, monitor hover,
  terminal, rover at 1280×720, Ingenuity at 1600×900 and 2560×1080. Reference
  images remain in the upper-right corner with their aspect ratio and alpha.
  Terminal layout and keyboard focus also checked at logical 1920×1080,
  2560×1080, 1920×1440 and 1920×4160.
- Command scene resource paths and `git diff --check`: clean.

Screenshots in `previews/`: `rover-control.png`, `ingenuity-control.png`,
`ingenuity-control-wide.png`, `command-room.png`, `command-terminal.png`.
They show the actual integration/test scene, not a completed planet or station level.

Earlier command-test runs intermittently aborted during engine shutdown after
all assertions passed. Copying the native signal connection dictionaries was
removed, and presentation context is now event-driven rather than allocating
C# snapshots every physics frame. The final run above exited normally. Native
rendered capture runs also exited normally.

Recovery is verified as an automatic `RecoveryRequested` event only. The user
confirmed that physical delivery is deferred to the shuttle stage; the current
scope is the request alone. There is no transport/autopilot implementation or
teleport fallback; the terminal labels this missing system. Rover/drill
resources and refunds are deferred by the user.
The future shuttle must block planetary departure until both the rover and
Ingenuity are physically aboard. This user-approved requirement is documented
in the handoff; it is not tested or implemented without the shuttle/loading system.
Physical controller/gamepad testing was not performed.
