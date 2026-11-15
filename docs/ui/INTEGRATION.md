# Main menu and settings

Open `project.godot` in Godot **4.7.2 .NET**, or run:

```sh
godot --headless --path . --editor --import --quit
dotnet build 'Jeep..csproj'
godot --path .
```

The entry scene is `Scene/UI/MainMenu.tscn`. `Scene/UI/menu.cfg` holds the game destination, model, composition and transition timing. The approved destination is `Scene/A.tscn`.

The front end uses Godot Controls and 3D scenes. It has no web runtime or external service. `UserSettings` owns audio buses, display/render options and physical keyboard bindings. Preferences are stored atomically in `user://settings.cfg`, separately from gameplay saves. Window resolution is editable in windowed mode; fullscreen uses the display's native size. Render scale changes 3D resolution without lowering text resolution. Display changes require confirmation within 15 seconds.

Audio is previewed while editing and restored on cancellation. Other changes apply after confirmation. Keyboard conflicts are checked within each active control context; global camera actions also conflict with movement actions. Escape remains available for cancelling dialogs. Controller and mouse bindings already supplied by the game are preserved.

## Station appearance after progress

The menu can show a render-only snapshot of the player's actual modified station. This is **not a gameplay save system**. Nothing is fabricated when no station snapshot exists: the shipped station model is used.

When the station scene is implemented, mark its visual root:

```gdscript
station.add_to_group("menu_preview_station")
```

Return to the main menu through the existing autoload:

```gdscript
StationPreview.return_to_menu()
```

This captures the station in the group, then opens the menu. Alternatively call `StationPreview.capture(station)` explicitly. C# callers use `GetNode("/root/StationPreview").Call("capture", station)`, or `.Call("return_to_menu")`.

Capture strips scripts, physics, cameras and UI; it keeps visible mesh geometry, transforms and materials, including added modules. The preview is stored in `user://menu/station_preview.scn`, with references to project model resources. Node count and file size are bounded by `menu.cfg`. Loaded previews are uniformly framed to fit the menu composition. The current driving test scene has no station and therefore keeps the base menu model.

## Developer resource integration

FPS display works immediately. Infinite resources is visibly unavailable until a real resource system registers; the current prototype has no economy or resource inventory.

The resource owner's node implements:

```gdscript
func set_debug_infinite_resources(enabled: bool) -> void:
    # Implement against the authoritative resource system here.
    # The menu never owns inventory, prices or resource consumption.
    resource_system.debug_infinite_resources = enabled

func _ready() -> void:
    UserSettings.register_resource_provider(self)
```

The provider receives `false` when developer mode is disabled. Its registration is cleared on scene exit. A C# node implements a public method named exactly `set_debug_infinite_resources(bool enabled)` and registers via `GetNode("/root/UserSettings").Call("register_resource_provider", this)`.

## Verification

### Credits catalogue

Settings → **Автори й джерела** reads `ASS/UI/credits.json`. Add entries to its existing sections (title, credit, optional detail, links, assets and licence). Source links open only on user activation; font and engine licence texts work offline. A `license_file` points to an included text file. `runtime_license` accepts `godot` or `godot_components` and uses the running build's notices. No settings are applied or discarded by reading credits.

The inventory of remaining source information is in `ASS/UI/CREDITS.md`; the catalogue is intentionally completed as assets and team credits are finalised. When creating an export preset, include `ASS/UI/credits.json` and `ASS/UI/Fonts/*-OFL.txt` in its non-resource export filter: raw JSON/text read through FileAccess must accompany the exported game.

### Checks

Use an isolated user directory for tests so personal preferences are not replaced:

```sh
XDG_DATA_HOME=/tmp/white-cat-ui-tests godot --headless --path . --script res://tests/ui/test_frontend.gd
XDG_DATA_HOME=/tmp/white-cat-ui-layout godot --headless --path . --script res://tests/ui/test_layout.gd
XDG_DATA_HOME=/tmp/white-cat-ui-captures godot --path . --display-driver x11 --script res://tests/ui/capture.gd -- --screen=menu --size=1600x900 --output=/tmp/white-cat-menu.png
```

Capture options: `settings0` through `settings6` (credits), `license` for the offline licence reader, or `launch1.8` for an actual frame 1.8 seconds into the transition. Add `--scroll=1800` to inspect lower settings content. The capture uses the real 3D renderer and a fixed-size SubViewport so tiling window managers cannot resize the output. `--headless` cannot capture graphics. Inspect native keyboard focus, popup focus, scroll content, display rollback, audio cancellation, saved bindings and scene launch. Physical controller behaviour still needs a connected-device check.

## Command terminal

`Scene/A.tscn` now opens the supplied command-room model. One click on its highlighted monitor opens `help` / `connect`; connecting selects both the real camera and control recipient. Command API, scene adapters, vehicle reference images, import provenance, tests and the pending recovery-delivery contract: [command centre integration](COMMAND_CENTER_HANDOFF.uk.md).
