"""Export the supplied room unchanged; run in Blender with --disable-autoexec."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy

parser = argparse.ArgumentParser()
parser.add_argument("--output", required=True)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
target = Path(args.output).resolve()
target.parent.mkdir(parents=True, exist_ok=True)
# glTF cannot evaluate Blender's procedural Noise/ColorRamp graph. Preserve the
# authored base colour instead of silently exporting its disconnected white default.
for material in bpy.data.materials:
    if not material.use_nodes:
        continue
    principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if principled:
        socket = principled.inputs["Base Color"]
        if socket.is_linked:
            for link in list(socket.links):
                material.node_tree.links.remove(link)
            socket.default_value = material.diffuse_color
    elif material.blend_method == "BLEND":
        # The source glazing uses a Transparent/Glossy Fresnel mix, also outside glTF.
        output = next(node for node in material.node_tree.nodes if node.type == "OUTPUT_MATERIAL")
        principled = material.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        principled.inputs["Base Color"].default_value = material.diffuse_color
        principled.inputs["Alpha"].default_value = material.diffuse_color[3]
        principled.inputs["Roughness"].default_value = 0.12
        material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
# glTF has no area-light primitive. Preserve their authored transforms and values
# as metadata; the Godot wrapper supplies the supported real-time light type.
for obj in list(bpy.context.scene.objects):
    if obj.type == "LIGHT" and obj.data.type == "AREA":
        marker = bpy.data.objects.new(obj.name + " | Godot light source", None)
        bpy.context.scene.collection.objects.link(marker)
        marker.matrix_world = obj.matrix_world
        marker["room_light_watts"] = obj.data.energy
        marker["room_light_color"] = list(obj.data.color)
bpy.ops.export_scene.gltf(
    filepath=str(target), export_format="GLB", export_yup=True,
    export_extras=True, export_cameras=True, export_lights=True,
    export_animations=False, export_apply=False,
)
source = Path(bpy.data.filepath)
manifest = {
    "source_archive": "Головна кімната — White Cat-20261115T131056Z-1-001.zip",
    "source_file": "Головна кімната — White Cat.blend",
    "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
    "exporter": "Blender " + bpy.app.version_string,
    "scene": bpy.context.scene.name,
    "output_sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
    "note": "Unchanged geometry and authored camera. Procedural colours use the authored base-colour fallback; glazing is converted to a transparent PBR material. Area lights are metadata markers for Godot. Original Blend is never saved or modified.",
}
target.with_suffix(".source.json").write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
)
