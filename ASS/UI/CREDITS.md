# White Cat menu assets

The editable in-game catalogue is `res://ASS/UI/credits.json`. Settings → **Автори й джерела** renders it directly, including source links and offline font/engine licence readers. Add final team names and remaining asset sources there as the project is completed; do not infer an author or a licence from a filename.

- `Textures/earth_nasa.jpg`: NASA Blue Marble 2002, land/ocean/ice/cloud equirectangular composite, 2048×1024. NASA Earth Observatory; Reto Stöckli, Robert Simmon and the Blue Marble team. [Source and description](https://science.nasa.gov/earth/earth-observatory/the-blue-marble-true-color-global-imagery-at-1km-resolution/). [Original image](https://assets.science.nasa.gov/content/dam/science/esd/eo/content-feature/bluemarble/images/land_ocean_ice_cloud_2048.jpg). This historical composite is not a live image.
- `Fonts/Onest.ttf`: Onest Project Authors. SIL OFL 1.1; complete licence included alongside the font.
- `Fonts/BarlowCondensed-SemiBold.ttf`: Barlow Project Authors. SIL OFL 1.1; complete licence included alongside the font.
- `Audio/*.wav`: original procedural tones and a loopable ambient composition, reproduced by `tools/ui_audio.py`; no sampled recordings.
- `../Station/Space_Station.glb`: the existing White Cat ring station, copied unchanged from the companion data repository, `assets/models/Space_Station.glb`. Geometry and atlas were created for White Cat from a supplied reference. The rear dish was adapted from the NASA Science / NASA-JPL Mars Reconnaissance Orbiter model; preserve the `nasa_mro_3d` credit. Provenance is documented in the companion repository's `assets/models/space_station/README.md` and `asset_info.json`.
- Star panorama: the game's existing `ASS/Sky/plain_starfield_1.png`.

Lighting, station rotation, distances and Earth scale in the menu are artistic composition, not a scientific simulation or an orbital prediction.

## Additional confirmed project credits

- `ASS/Helicopter/Mars_Helicopter.glb`: matches the companion project's authored White Cat Mars Helicopter export (SHA-256 `9b0d9d7f2560034f8167ab87c1437b259ad25f2b1e0a4978124fe1f9e279ab25`). Its README documents newly created geometry and textures from a user-supplied reference; it is not credited as an official NASA model.
- `ASS/JEEP/scene.gltf`: embedded `asset.extras` identifies **1987 Jeep Comanche** by **tonielpro520**, [source](https://sketchfab.com/3d-models/1987-jeep-comanche-6380330f485d4798858d9c9027b281d9), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). This credit describes that original import, not unrelated SUV or rover files.
- Engine and bundled-component attribution comes from the running Godot build's `Engine.get_license_text()`, `get_copyright_info()` and `get_license_info()`, so the reader uses the actual engine's notices.

## Sources to collect before final delivery

The existing repository does not record authors/licences for `ASS/Sky/*.png`, `ASS/JEEP/suv*.glb` and their textures, `ASS/RoverNew/*`, `ASS/QweQ/*`, or the older `Sounds/*.mp3`. Filenames referring to Freesound or Epidemic Sound do not establish a licence. These files are not attributed to NASA or to a guessed author. Collect the original source, author, licence and any changes, then add confirmed credits to the catalogue.
