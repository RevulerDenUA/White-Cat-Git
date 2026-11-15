# UI verification — 2026-11-15

Base repository revision: `89c5a98bc33df7211a1b016c2d846c12c1d3de79`.

- Godot 4.7.2 .NET. Real graphics captures on AMD Radeon Graphics with Vulkan Forward+ and OpenGL Compatibility.
- `dotnet build Jeep..csproj`: success, zero errors and zero warnings.
- Native front-end suite: **46 checks, zero failures**. Numeric bounds, context-aware binding conflicts, physical keys reaching InputMap, atomic disk persistence, audio preview/cancellation, focus restoration, modal focus trapping, display confirmation rejection and actual 15-second timeout, resource-provider callbacks, developer-mode disable, preview capture of added geometry and paint changes, and the complete transition into `Scene/A.tscn`. Credits checks cover a read-only catalogue, complete offline font licence, modal focus and restoration, preserved pending settings and the running engine's component notices.
- Layout suite: **711 visible controls inspected, zero failures**, at logical viewport sizes 1920×1080, 2560×1080, 1920×1440 and 1920×4160. All seven settings tabs and the licence reader are covered; scroll areas are checked against collapsed height. Scroll content is intentionally clipped by its scroll container.
- Rendered and visually inspected: main menu at 1600×900 and 2560×1080, portrait composition at 1080×2340, controls/settings at 1280×720 and 1600×900, plus frames during the camera transition. Font rendering uses MSDF.
- Credits rendered and inspected at 1600×900, 2560×1080 (scrolled to the final sections) and 1080×2340, plus the offline licence reader. `previews/settings-credits.png` records the new tab. The catalogue currently contains 12 confirmed entries in six sections; final team and asset attribution will be added during development.
- `previews/start-transition.mp4`: 150 frames captured from the actual Godot scene at 30 FPS, encoded to H.264. This is a silent visual preview; the game itself has original ambient music and interface/transition sounds.
- Project reference checker: zero errors. Three existing recoverable path warnings remain in the unrelated `Planet Temp/node_3d.tscn`; Godot resolves those scripts through their UIDs. This legacy scene is not in the menu or driving-scene launch path.

The current game has no resource inventory. Infinite resources is unavailable in settings until a real resource provider registers; its integration contract is documented and verified with a test provider. The station snapshot is a visual preview, not a gameplay save. No physical gamepad test was performed.
