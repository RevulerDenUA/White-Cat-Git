# Scientific source catalogue — 2026-11-15

The in-game page **Налаштування → Автори й джерела** reads
`ASS/UI/credits.json`. The page now covers all 31 IDs in the companion
White-cat repository's `data/sources.json`, plus source records found only in
the local archives and asset documentation. No scientific simulation or new
dataset integration was added to gameplay by this change.

Each entry has a visible `usage` description and a machine-readable `scope`:
game asset/derivative, data package, archive, reference, listed-only or
not-downloaded. ExoVista is labelled synthetic, observations remain historical,
and slope-based traversability is labelled a game calculation. DACE/NGPPS,
HiRISE DTM, GMAT, Mission Viz, the NASA/CSA educational games and similar
listed resources are not presented as imported game datasets.

The original credits for fonts, Godot, procedural audio, station and older
imports remain available. The authored Ingenuity model is distinguished from
the separate official NASA GLB. The adapted MRO station antenna and the Hubble
reference retain their separate credits, including DigitalSpace Corporation.

## Offline evidence

- `ASS/UI/SourceRecords/science_sources.uk.txt`: full readable inventory,
  all 25 raw rover images, all 25 NASA-library previews, original item-level
  credits, URLs, raw image hashes and the five prepared WebP mappings.
- `ASS/UI/SourceRecords/provenance.json`: original source index, catalogue
  hash, 60 local evidence-file hashes, telescope metadata, official Ingenuity
  metadata and archive verification results.
- Initial 208-file archive: no missing files, 207 hashes match. The expanded
  `data/sources.json` differs from the old inventory; both old and current
  hashes are recorded explicitly. No source files were rewritten.

The audit covers actual project source records and direct references in team
documentation. It does not recursively claim every citation inside a PDF or
every hyperlink in a downloaded webpage as a resource used by White Cat.
Some original web endpoints reject automated rereads (USGS returned 403;
VizieR timed out); their locally recorded provenance is retained. Current
NASA pages confirmed Blue Marble authorship, the Hubble original author,
ExoVista, PDS services and the IceBridge image credit.

## Update and verify

Edit `ASS/UI/credits.json` first, including concrete `evidence` paths and source
IDs. Use repository-relative paths; do not store absolute workstation paths.
The tool reads the companion project without modifying it:

```sh
python3 tools/audit_science_credits.py --companion '/path/to/White-cat' --write
python3 tools/audit_science_credits.py --companion '/path/to/White-cat'
XDG_DATA_HOME=/tmp/white-cat-science-frontend godot --headless --path . --script res://tests/ui/test_frontend.gd
XDG_DATA_HOME=/tmp/white-cat-science-layout godot --headless --path . --script res://tests/ui/test_layout.gd
```

Pass `--date=YYYY-MM-DD` when making a new source audit. The second command
checks coverage against the real companion index, source files, image IDs,
original credits, telescope hashes and generated offline reader. This catches
a missing data source even when the JSON itself parses successfully.

`CreditsView.document_requested(title, text)` opens the existing read-only
modal through Settings, preserving pending settings and returning keyboard
focus to the document link when closed. Source documents are restricted to
`res://ASS/UI/SourceRecords/`; web links remain HTTPS-only. Include these JSON
and TXT files in future game export presets alongside the existing credits
and font-licence files.

Native screenshot capture uses `tests/ui/capture.gd -- --screen=settings6`
with `--scroll=` for long catalogue sections, or `--screen=sources` for the
offline reader. Tests use separate user data directories.
