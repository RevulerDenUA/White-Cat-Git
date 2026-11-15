#!/usr/bin/env python3
"""Verify game credits against the companion project's actual source records."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOGUE = ROOT / "ASS/UI/credits.json"
RECORDS = ROOT / "ASS/UI/SourceRecords"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def relative_file(base, name):
    path = (base / name).resolve()
    if not path.is_relative_to(base.resolve()) or not path.is_file():
        raise ValueError(f"Missing or invalid evidence: {name}")
    return path


def audit(companion, audit_date):
    catalogue = read(CATALOGUE)
    entries = [entry for section in catalogue["sections"] for entry in section["entries"]]
    sources = read(companion / "data/sources.json")["sources"]
    covered = {key for entry in entries for key in entry.get("source_ids", [])}
    urls = {link["url"] for entry in entries for link in entry.get("links", [])}
    missing_ids = {source["id"] for source in sources} - covered
    missing_urls = {source["url"] for source in sources} - urls
    if missing_ids or missing_urls:
        raise ValueError(f"Incomplete catalogue: IDs {sorted(missing_ids)}, URLs {sorted(missing_urls)}")
    if any(not url.startswith("https://") for url in urls):
        raise ValueError("Credit links must use HTTPS")
    evidence = {}
    for entry in entries:
        for asset in entry.get("assets", []):
            if asset.startswith("res://"):
                relative_file(ROOT, asset.removeprefix("res://"))
        for item in entry.get("evidence", []):
            base = {"White-cat": companion, "White-Cat-Git": ROOT}[item["repository"]]
            path = relative_file(base, item["path"])
            key = item["repository"] + "/" + item["path"]
            if key not in evidence:
                evidence[key] = {"bytes": path.stat().st_size, "sha256": digest(path)}

    # The old inventory predates the expanded source index. Record changes; never
    # overwrite originals or quietly describe changed files as matching the old hash.
    inventory = read(companion / "docs/audit/raw_inventory.json")
    changed = []
    for item in inventory["files"]:
        path = relative_file(companion, item["path"])
        actual = digest(path)
        if actual != item["sha256"]:
            changed.append({"path": item["path"], "previous_sha256": item["sha256"], "sha256": actual})

    prepared = read(companion / "assets/images/manifest.json")["images"]
    selected = {image["image_id"]: image for image in prepared}
    raw = read(companion / "data/rover_images/images_manifest.json")["items"]
    if len({item["image_id"] for item in raw}) != len(raw):
        raise ValueError("Duplicate raw image IDs")
    raw_ids = {item["image_id"] for item in raw}
    if not set(selected).issubset(raw_ids):
        raise ValueError("A prepared image is absent from the original image manifest")
    images = []
    for item in raw:
        source = relative_file(companion, "data/rover_images/" + item["filename"])
        derived = selected.get(item["image_id"])
        if derived:
            relative_file(companion, derived["path"])
            if derived["credit"] != item["credit"] or derived["source_page"] != item["source_page"]:
                raise ValueError(f"Changed credit/source on {item['image_id']}")
        images.append({key: item[key] for key in ["image_id", "filename", "camera_id", "sol", "title", "credit", "source_page", "download_url"]}
                      | {"sha256": digest(source), "prepared_asset": derived})

    library = []
    for item in read(companion / "data/images/nasa-library/perseverance_jezero_search.json")["items"]:
        metadata = item["data"][0]
        identifier = metadata["nasa_id"]
        previews = sorted((companion / "data/images/nasa-library/previews").glob(identifier + "*"))
        if not previews or not metadata.get("secondary_creator"):
            raise ValueError(f"Missing preview or original creator credit: {identifier}")
        library.append({key: metadata.get(key) for key in ["nasa_id", "title", "date_created", "center", "secondary_creator", "photographer"]}
                       | {"source_collection": item["href"], "previews": [str(path.relative_to(companion)) for path in previews]})

    models = read(companion / "data/models/telescopes/sources.json")["models"]
    for model in models:
        path = relative_file(companion, "data/models/telescopes/" + model["file"])
        if digest(path) != model["sha256"] or model["source_page"] not in urls:
            raise ValueError(f"Missing or altered NASA telescope source: {model['name']}")
    ingenuity = read(companion / "data/models/Ingenuity_Mars_Helicopter.source.json")
    if digest(companion / "data/models/Ingenuity_Mars_Helicopter.glb") != ingenuity["sha256"] or ingenuity["page"] not in urls:
        raise ValueError("Missing or altered official Ingenuity reference")
    rocks = read(ROOT / "ASS/QweQ/White cat камінці/catalog.json")["references"]
    constants = read(companion / "data/physics/physical_constants.json")["constants"]
    for url in {ref["url"] for ref in rocks} | {constant["source"] for constant in constants.values()}:
        if url not in urls:
            raise ValueError(f"Uncredited geological/physical source: {url}")
    return {
        "schema_version": 1,
        "audit_date": audit_date,
        "scope": "Source records in both local projects, including ignored source archives. Not a claim that all datasets run in Godot.",
        "catalogue_sha256": digest(CATALOGUE),
        "source_index": sources,
        "covered_source_ids": sorted(covered),
        "evidence": evidence,
        "original_archive": {"files": len(inventory["files"]), "hash_matches": len(inventory["files"]) - len(changed), "changed_since_original_inventory": changed},
        "raw_images": images,
        "nasa_library_images": library,
        "telescope_models": models,
        "official_ingenuity_reference": ingenuity,
    }


def document(catalogue, records):
    lines = ["WHITE CAT — NASA ТА ІНШІ ДЖЕРЕЛА", "Перевірено: " + records["audit_date"], "",
             "Перевірено обидва локальні репозиторії, включно з файлами поза Git.",
             "Охоплено всі 31 записи data/sources.json та додаткові локальні джерела.",
             "Статус кожного матеріалу описано окремо. Архівні дані не є живою телеметрією.",
             "Посилання всередині PDF та сторонніх вебсторінок не вважаються автоматично використаними даними.", ""]
    for section in catalogue["sections"]:
        lines += [section["title"].upper(), ""]
        for entry in section["entries"]:
            lines += [entry["title"], entry.get("usage", ""), entry["credit"], entry.get("detail", "")]
            lines += [link["label"] + ": " + link["url"] for link in entry.get("links", [])]
            lines += ["Підтвердження — " + proof["repository"] + "/" + proof["path"] for proof in entry.get("evidence", [])]
            lines += ["Асет у грі — " + asset for asset in entry.get("assets", [])]
            lines.append("")
    lines += ["25 ОРИГІНАЛЬНИХ ФОТО PERSEVERANCE", "Кредит кожного фото збережено з оригінального manifest. П’ять фото також підготовлено як WebP.", ""]
    for i, image in enumerate(records["raw_images"], 1):
        lines += [f"{i}. {image['image_id']}", image["title"], f"Камера: {image['camera_id']}; sol: {image['sol']}",
                  "Кредит: " + image["credit"], image["source_page"], "Оригінал: " + image["download_url"],
                  "Локальний оригінал: White-cat/data/rover_images/" + image["filename"], "SHA-256: " + image["sha256"]]
        if image["prepared_asset"]:
            prepared = image["prepared_asset"]
            lines += ["Підготовлений файл: White-cat/" + prepared["path"], "Оброблення: змінено розмір і закодовано WebP; реальне фото Марса."]
        lines.append("")
    lines += ["25 МАТЕРІАЛІВ NASA IMAGE AND VIDEO LIBRARY", "Локальний архів прев’ю. У ньому є фото, карти та ілюстрації. Не підключені до поточної сцени гри.", ""]
    for i, image in enumerate(records["nasa_library_images"], 1):
        lines += [f"{i}. {image['nasa_id']} — {image['title']}", "Кредит secondary_creator: " + image["secondary_creator"],
                  "Центр публікації: " + image["center"], "Дата матеріалу: " + image["date_created"], image["source_collection"]]
        if image.get("photographer"):
            lines.append("Photographer: " + image["photographer"])
        lines += ["Прев’ю: White-cat/" + path for path in image["previews"]]
        lines.append("")
    lines += ["КОНТРОЛЬ ПОХОДЖЕННЯ", f"Початковий архів: {records['original_archive']['files']} файлів; відсутніх немає.",
              f"Збіг із початковими SHA-256: {records['original_archive']['hash_matches']}."]
    for change in records["original_archive"]["changed_since_original_inventory"]:
        lines.append("Змінено після початкового аудиту: " + change["path"] + "; поточний SHA-256: " + change["sha256"])
    lines += ["", "Зіркові фони, частина старих моделей і звуків не мають підтвердженого авторства в проєкті. Їх не приписано NASA.",
              "Ресурси з позначками «не завантажено» або «згадані в челенджі» не заявляються як фактично використані набори."]
    return "\n".join(line for line in lines if line is not None) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--companion", required=True, type=Path, help="Local White-cat data repository")
    parser.add_argument("--date", default="2026-11-15", help="Audit date, YYYY-MM-DD")
    parser.add_argument("--write", action="store_true", help="Regenerate the bundled provenance and offline reader")
    args = parser.parse_args()
    records = audit(args.companion.resolve(), args.date)
    serialized = json.dumps(records, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    text = document(read(CATALOGUE), records)
    if args.write:
        RECORDS.mkdir(parents=True, exist_ok=True)
        (RECORDS / "provenance.json").write_text(serialized, encoding="utf-8")
        (RECORDS / "science_sources.uk.txt").write_text(text, encoding="utf-8")
    else:
        if (RECORDS / "provenance.json").read_text(encoding="utf-8") != serialized or (RECORDS / "science_sources.uk.txt").read_text(encoding="utf-8") != text:
            raise ValueError("Bundled provenance is stale; review changes, then regenerate with --write")
    print(f"SCIENCE CREDITS: {len(records['source_index'])} source IDs covered; {len(records['evidence'])} evidence files; {len(records['raw_images']) + len(records['nasa_library_images'])} individually credited images; archive {records['original_archive']['hash_matches']}/{records['original_archive']['files']} original hashes match, changes explicitly recorded")


if __name__ == "__main__":
    main()
