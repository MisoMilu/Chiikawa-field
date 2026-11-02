"""Run with Blender in background mode. Reads the original GLB; writes a Unity FBX copy."""
import bpy
from mathutils import Vector
from pathlib import Path

source = Path("/Users/misomilu/Desktop/College/1_year_after_Senior/COMM 638/Chiikawa game/chiikawa.glb")
output = Path(__file__).resolve().parents[2] / "Assets/Chiikawa/Models/Chiikawa.fbx"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
# Bake the source hierarchy into the static mesh without changing its shape or UVs.
for obj in meshes:
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world
for obj in list(bpy.context.scene.objects):
    if obj.type != "MESH":
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.ops.object.select_all(action="SELECT")
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
coords = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
print("MODEL_BOUNDS", [min(p[i] for p in coords) for i in range(3)], [max(p[i] for p in coords) for i in range(3)])
bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", bake_anim=False, add_leaf_bones=False, path_mode="STRIP")
print("EXPORTED", output)
