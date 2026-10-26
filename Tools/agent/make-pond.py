"""Builds the knee-deep pond: a floor tile with a dug pit and the water that fills it, exported as one FBX.

Run with Blender in background mode:
  blender -b -P Tools/agent/make-pond.py -- <out.fbx> [out.blend] [preview_dir]

The "ground" part replaces a 2x2 block of Kenney Mini Arena floor tiles (5 x 5 m, top at 0), so its
outer edge must stay exactly on the tile border. It shares the Kenney colormap palette. The "water"
part is a closed prism: its top is the visible surface and the whole prism doubles as the trigger
volume in Unity. Its second UV channel holds each vertex's distance to the shore in meters, which the
water shader uses to calm the waves at the shore.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT_FBX = ARGS[0]
OUT_BLEND = next((arg for arg in ARGS[1:] if arg.endswith(".blend")), None)
PREVIEW_DIR = next((arg for arg in ARGS[1:] if not arg.endswith(".blend")), None)
COLORMAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                        "Assets", "Models", "KenneyMiniArena", "Textures", "colormap.png")

# Pixel positions of flat color cells in colormap.png (512x512, origin top-left).
CELLS = {
    # The exact cell the Kenney floor tile uses, so there is no seam between the tiles.
    "floor": (496, 448),
    "dirt_light": (360, 456),
    "dirt_dark": (424, 440),
}

# Dimensions in meters. The character is ~1.9 m tall, so the water reaches about the knee.
TILE_HALF = 2.5
# Kept small enough that the wobbly rim stays clear of the stairs that start at the tile border.
RIM_RADIUS = 1.9
RIM_WOBBLE = 0.1
WOBBLE_SEED = 1.7
PIT_DEPTH = 0.65
# Horizontal run of the wall from rim to floor; about 30 degrees, well under the controller's slope limit.
WALL_RUN = 1.1
LIGHT_BAND_DEPTH = 0.08
WATER_LEVEL = -0.15
# The water reaches a little into the wall so no strip of wall shows through at the waterline.
WATER_OVERLAP = 0.03
# The water surface is an even mesh of irregular triangles, so the flat-shaded facets read as low-poly
# ripples. A fan from the center would draw long spokes instead.
WATER_GRID_SPACING = 0.3
WATER_GRID_JITTER = 0.1
# Keeps interior points away from the shore ring so no sliver triangles form there.
WATER_GRID_MARGIN = 0.15
# Must be a multiple of 8 so the samples land on the tile corners.
SEGMENTS = 64


def cell_uv(name):
    pixel_x, pixel_y = CELLS[name]
    return (pixel_x / 512.0, 1.0 - pixel_y / 512.0)


def paint(bm, faces, cell_name):
    uv_layer = bm.loops.layers.uv.verify()
    uv = cell_uv(cell_name)
    for face in faces:
        for loop in face.loops:
            loop[uv_layer].uv = uv


def angles():
    return [math.tau * index / SEGMENTS for index in range(SEGMENTS)]


def rim_radius(angle):
    # Whole-number frequencies keep the outline seamless where the angle wraps.
    wobble = (math.sin(angle * 3.0 + WOBBLE_SEED) * 0.5
              + math.sin(angle * 5.0 + WOBBLE_SEED * 1.7) * 0.3
              + math.sin(angle * 7.0 + WOBBLE_SEED * 2.3) * 0.2)
    return RIM_RADIUS * (1.0 + wobble * RIM_WOBBLE)


def wall_radius(angle, z):
    return rim_radius(angle) - WALL_RUN * (-z / PIT_DEPTH)


def square_radius(angle):
    return TILE_HALF / max(abs(math.cos(angle)), abs(math.sin(angle)))


def ring(bm, radius_at, z):
    return [bm.verts.new((radius_at(angle) * math.cos(angle), radius_at(angle) * math.sin(angle), z))
            for angle in angles()]


def bridge(bm, outer, inner):
    """Quads between two rings; outer before inner keeps the winding counterclockwise from above."""
    faces = []
    for index in range(SEGMENTS):
        next_index = (index + 1) % SEGMENTS
        faces.append(bm.faces.new((outer[index], outer[next_index], inner[next_index], inner[index])))
    return faces


def fan(bm, outer, center):
    return [bm.faces.new((outer[index], outer[(index + 1) % SEGMENTS], center)) for index in range(SEGMENTS)]


def make_water_material():
    material = bpy.data.materials.new("water")
    material.diffuse_color = (0.05, 0.62, 0.66, 1.0)
    return material


def make_material():
    material = bpy.data.materials.new("colormap")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(os.path.normpath(COLORMAP))
    # Packed, so the .blend opens correctly on any machine.
    texture.image.pack()
    texture.interpolation = "Closest"
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    material.node_tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    return material


def make_part(name, material, build):
    """Faces are wound explicitly, so normals are not recalculated: that is unreliable on the open ground sheet."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    build(bm)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def build_ground(bm):
    edge = ring(bm, square_radius, 0.0)
    rim = ring(bm, rim_radius, 0.0)
    band = ring(bm, lambda angle: wall_radius(angle, -LIGHT_BAND_DEPTH), -LIGHT_BAND_DEPTH)
    floor = ring(bm, lambda angle: wall_radius(angle, -PIT_DEPTH), -PIT_DEPTH)
    center = bm.verts.new((0.0, 0.0, -PIT_DEPTH))
    paint(bm, bridge(bm, edge, rim), "floor")
    paint(bm, bridge(bm, rim, band), "dirt_light")
    paint(bm, bridge(bm, band, floor) + fan(bm, floor, center), "dirt_dark")


def build_water(bm):
    shore_radius = lambda angle: wall_radius(angle, WATER_LEVEL)
    outline = [((shore_radius(angle) + WATER_OVERLAP) * math.cos(angle), (shore_radius(angle) + WATER_OVERLAP) * math.sin(angle))
               for angle in angles()]
    # Fixed seed, so every run of the script produces the same mesh.
    jitter = random.Random(7)
    interior = []
    steps = int(RIM_RADIUS * 2 / WATER_GRID_SPACING) + 2
    for row in range(-steps, steps + 1):
        for column in range(-steps, steps + 1):
            # Every other row is shifted by half a step, which gives roughly equilateral triangles.
            x = (column + (row % 2) * 0.5) * WATER_GRID_SPACING + jitter.uniform(-WATER_GRID_JITTER, WATER_GRID_JITTER)
            y = row * WATER_GRID_SPACING * 0.866 + jitter.uniform(-WATER_GRID_JITTER, WATER_GRID_JITTER)
            if shore_radius(math.atan2(y, x)) - math.hypot(x, y) > WATER_GRID_MARGIN:
                interior.append((x, y))

    points = outline + interior
    # Output type 1 keeps only the triangles inside the shore outline.
    out_points, _, out_faces, out_origins, _, _ = delaunay_2d_cdt(
        [Vector(point) for point in points], [], [list(range(SEGMENTS))], 1, 1e-6)
    verts = [bm.verts.new((point.x, point.y, WATER_LEVEL)) for point in out_points]
    shore = [None] * SEGMENTS
    for index, origins in enumerate(out_origins):
        for origin in origins:
            if origin < SEGMENTS:
                shore[origin] = verts[index]
    top = []
    for face in out_faces:
        corners = [verts[index] for index in face]
        # Counterclockwise from above, so the surface faces +Z.
        if (corners[1].co - corners[0].co).cross(corners[2].co - corners[0].co).z < 0.0:
            corners.reverse()
        top.append(bm.faces.new(corners))

    # The sides and bottom sit inside the ground, so they are never seen; they close the trigger volume.
    bottom_ring = ring(bm, lambda angle: shore_radius(angle) + WATER_OVERLAP, -PIT_DEPTH - 0.05)
    bottom_center = bm.verts.new((0.0, 0.0, -PIT_DEPTH - 0.05))
    sides = bridge(bm, bottom_ring, shore)
    bottom = [bm.faces.new((bottom_ring[(index + 1) % SEGMENTS], bottom_ring[index], bottom_center))
              for index in range(SEGMENTS)]

    uv_layer = bm.loops.layers.uv.verify()
    shore_layer = bm.loops.layers.uv.new("shore")
    for face in top + sides + bottom:
        for loop in face.loops:
            position = loop.vert.co
            # Planar, spanning the tile; the current shader does not read it, but the mesh keeps a sane UV0.
            loop[uv_layer].uv = (position.x / (2 * TILE_HALF) + 0.5, position.y / (2 * TILE_HALF) + 0.5)
            angle = math.atan2(position.y, position.x)
            loop[shore_layer].uv = (shore_radius(angle) - Vector((position.x, position.y)).length, 0.0)


def check_normals(part, faces_up):
    """Guards the explicit winding: every upward face of the walkable or visible surface must face +Z."""
    flipped = [polygon.index for polygon in part.data.polygons if faces_up(polygon) and polygon.normal.z <= 0.0]
    if flipped:
        raise RuntimeError(f"{part.name}: {len(flipped)} faces point down")


def build_pond():
    material = make_material()
    water_material = make_water_material()
    ground = make_part("ground", material, build_ground)
    water = make_part("water", water_material, build_water)
    check_normals(ground, lambda polygon: True)
    check_normals(water, lambda polygon: abs(polygon.center.z - WATER_LEVEL) < 1e-4)
    return [ground, water]


def export_fbx(parts):
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        path_mode="STRIP",
    )


def render_previews():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.world = bpy.data.worlds.new("preview")
    scene.world.color = (0.7, 0.75, 0.8)
    camera = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    target = Vector((0.0, 0.0, -0.3))
    for name, location in (("three_quarter", (4.5, -4.5, 3.5)), ("low", (0.0, -4.0, 0.6)), ("top", (0.0, -0.5, 7.0))):
        camera.location = location
        camera.rotation_euler = (target - Vector(location)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(PREVIEW_DIR, f"pond_{name}.png")
        bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
parts = build_pond()
export_fbx(parts)
if OUT_BLEND:
    os.makedirs(os.path.dirname(os.path.abspath(OUT_BLEND)), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(OUT_BLEND), compress=True)
if PREVIEW_DIR:
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    render_previews()
print("parts:", [part.name for part in parts], "faces:", sum(len(part.data.polygons) for part in parts))
