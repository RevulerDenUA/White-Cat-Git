# White Cat menu assets

The editable in-game catalogue is `res://ASS/UI/credits.json`. Settings → **Автори й джерела** renders it directly, including source links and offline font/engine licence readers. Add final team names and remaining asset sources there as the project is completed; do not infer an author or a licence from a filename.

The 2026-11-15 audit expands that page to include every source ID in the companion
`data/sources.json`, additional local telescope/Ingenuity records, geological and
drill references, scientific constants, and the references explicitly cited by
the team guide. Each entry states whether it is used by the game, prepared in the
data package, archived locally, a reading reference, or not downloaded. This is
an attribution catalogue, not a claim that every dataset is integrated into Godot.

**Повний аудит і перелік 50 зображень** opens the bundled
`SourceRecords/science_sources.uk.txt` reader. It preserves the individual credits
of all 25 raw rover images and all 25 NASA-library items, plus the mapping to the
five prepared WebP images. `SourceRecords/provenance.json` records source metadata,
60 evidence-file hashes and the old archive audit: all 208 files present, 207
original hashes matched, expanded `data/sources.json` explicitly recorded as changed.
Regeneration and validation are documented in `docs/ui/SCIENTIFIC_SOURCES.md`.

- `Textures/earth_nasa.jpg`: NASA Blue Marble 2002, land/ocean/ice/cloud equirectangular composite, 2048×1024. NASA Earth Observatory; Reto Stöckli, Robert Simmon and the Blue Marble team. [Source and description](https://science.nasa.gov/earth/earth-observatory/the-blue-marble-true-color-global-imagery-at-1km-resolution/). [Original image](https://assets.science.nasa.gov/content/dam/science/esd/eo/content-feature/bluemarble/images/land_ocean_ice_cloud_2048.jpg). This historical composite is not a live image.
- `Fonts/Onest.ttf`: Onest Project Authors. SIL OFL 1.1; complete licence included alongside the font.
- `Fonts/BarlowCondensed-SemiBold.ttf`: Barlow Project Authors. SIL OFL 1.1; complete licence included alongside the font.
- `Audio/*.wav`: original procedural tones and a loopable ambient composition, reproduced by `tools/ui_audio.py`; no sampled recordings.
- `../Station/Space_Station.glb`: the existing White Cat ring station, copied unchanged from the companion data repository, `assets/models/Space_Station.glb`. Geometry and atlas were created for White Cat from a supplied reference. The rear dish was adapted from the NASA Science / NASA-JPL Mars Reconnaissance Orbiter model; preserve the `nasa_mro_3d` credit. Provenance is documented in the companion repository's `assets/models/space_station/README.md` and `asset_info.json`.
- Star panorama: the game's existing `ASS/Sky/plain_starfield_1.png`.

Lighting, station rotation, distances and Earth scale in the menu are artistic composition, not a scientific simulation or an orbital prediction.

## Additional confirmed project credits

- `ASS/Helicopter/Mars_Helicopter.glb`: matches the companion project's authored White Cat Mars Helicopter export (SHA-256 `9b0d9d7f2560034f8167ab87c1437b259ad25f2b1e0a4978124fe1f9e279ab25`). Its README documents newly created geometry and textures from a user-supplied reference; it is not credited as an official NASA model.
- The earlier `ASS/JEEP/scene.gltf` import identified **1987 Jeep Comanche** by **tonielpro520**, [source](https://sketchfab.com/3d-models/1987-jeep-comanche-6380330f485d4798858d9c9027b281d9), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). That file was removed upstream; its historical credit is retained and does not describe the new rover.
- Engine and bundled-component attribution comes from the running Godot build's `Engine.get_license_text()`, `get_copyright_info()` and `get_license_info()`, so the reader uses the actual engine's notices.

## Sources to collect before final delivery

The existing repository does not consistently record authors/licences for
`ASS/Sky/*.png`, older SUV/rover assets, other `ASS/QweQ/*` files, or
`Sounds/*.mp3`. Geological references in the Terrain Scatter catalogue are now
credited separately; those references do not establish a licence for every file
under QweQ. Filenames referring to Freesound or Epidemic Sound do not establish
a licence. Unresolved assets are not attributed to NASA or to a guessed author.
