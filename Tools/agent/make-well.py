"""Builds the low-poly stone well as separate parts and exports them as one FBX.

Run with Blender in background mode:
  blender -b -P Tools/agent/make-well.py -- <out.fbx> [out.blend] [preview_dir]

Every face is UV-mapped to one flat color cell of the Kenney Mini Arena colormap, so the well
shares the palette (and the material) of the rest of the arena. The water is the exception: it gets
its own "water" material, an even mesh of small triangles for the flat-shaded waves, and a second UV
channel with each vertex's distance to the shaft wall in meters, which the water shader uses to calm
the waves at the wall.
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
    "stone": (144, 400),
    "stone_light": (208, 400),
    "stone_dark": (80, 400),
    "wood": (400, 400),
    "wood_light": (336, 400),
    "roof": (272, 300),
}

# Dimensions in meters. The character is ~1.9 m tall, so the rim sits around waist height.
WELL_RADIUS = 0.95
WELL_HOLE_RADIUS = 0.78
WELL_HEIGHT = 0.85
RIM_RADIUS = 1.05
RIM_HOLE_RADIUS = 0.74
RIM_HEIGHT = 0.12
WATER_LEVEL = 0.65
# Deep enough below the water that the depth-based water shader shows its deep color.
SHAFT_FLOOR = 0.1
WATER_GRID_SPACING = 0.18
WATER_GRID_JITTER = 0.05
# Keeps interior points away from the outline so no sliver triangles form there.
WATER_GRID_MARGIN = 0.08
POST_SIZE = 0.16
POST_X = 0.9
POST_TOP = 2.05
BEAM_HEIGHT = 1.55
# Must stay below POST_SIZE / 2 so the beam fits inside the posts.
BEAM_RADIUS = 0.07
ROOF_EAVE_HEIGHT = 2.0
ROOF_RIDGE_HEIGHT = 2.65
ROOF_HALF_WIDTH = 0.8
ROOF_HALF_LENGTH = 1.2
SIDES = 8


def cell_uv(name):
    pixel_x, pixel_y = CELLS[name]
    return (pixel_x / 512.0, 1.0 - pixel_y / 512.0)


def paint(bm, faces, cell_name):
    uv_layer = bm.loops.layers.uv.verify()
    uv = cell_uv(cell_name)
    for face in faces:
        for loop in face.loops:
            loop[uv_layer].uv = uv


def add_prism(bm, polygon, offset, cell_name):
    """Closed prism from a convex polygon extruded along offset."""
    bottom = [bm.verts.new(point) for point in polygon]
    top = [bm.verts.new(Vector(point) + Vector(offset)) for point in polygon]
    count = len(polygon)
    faces = [bm.faces.new(bottom), bm.faces.new(list(reversed(top)))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append(bm.faces.new((bottom[index], bottom[next_index], top[next_index], top[index])))
    paint(bm, faces, cell_name)


def circle_polygon(radius, z, sides=SIDES, rotation=math.pi / SIDES, center=(0.0, 0.0)):
    return [(center[0] + radius * math.cos(rotation + math.tau * index / sides),
             center[1] + radius * math.sin(rotation + math.tau * index / sides), z)
            for index in range(sides)]


def add_ring_prism(bm, outer_radius, inner_radius, z_bottom, z_top, cell_name):
    outer_bottom = [bm.verts.new(point) for point in circle_polygon(outer_radius, z_bottom)]
    outer_top = [bm.verts.new(point) for point in circle_polygon(outer_radius, z_top)]
    inner_bottom = [bm.verts.new(point) for point in circle_polygon(inner_radius, z_bottom)]
    inner_top = [bm.verts.new(point) for point in circle_polygon(inner_radius, z_top)]
    faces = []
    for index in range(SIDES):
        next_index = (index + 1) % SIDES
        faces.append(bm.faces.new((outer_bottom[index], outer_bottom[next_index], outer_top[next_index], outer_top[index])))
        faces.append(bm.faces.new((inner_bottom[next_index], inner_bottom[index], inner_top[index], inner_top[next_index])))
        faces.append(bm.faces.new((outer_top[index], outer_top[next_index], inner_top[next_index], inner_top[index])))
        faces.append(bm.faces.new((outer_bottom[next_index], outer_bottom[index], inner_bottom[index], inner_bottom[next_index])))
    paint(bm, faces, cell_name)


def rect_polygon_xy(center_x, center_y, half_x, half_y, z):
    return [(center_x - half_x, center_y - half_y, z), (center_x + half_x, center_y - half_y, z),
            (center_x + half_x, center_y + half_y, z), (center_x - half_x, center_y + half_y, z)]


def make_water_material():
    material = bpy.data.materials.new("water")
    material.diffuse_color = (0.045, 0.76, 0.87, 1.0)
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
    """One mesh object per part. Every part keeps its origin at the well origin, so parts line up
    when they are placed at zero and can be swapped or hidden on their own."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def shaft_wall_distance(x, y):
    """Distance from a point inside the shaft opening to its nearest octagon edge."""
    apothem = WELL_HOLE_RADIUS * math.cos(math.pi / SIDES)
    # circle_polygon is rotated by half a side, so the edge normals point along whole multiples of the side angle.
    return apothem - max(x * math.cos(math.tau * index / SIDES) + y * math.sin(math.tau * index / SIDES)
                         for index in range(SIDES))


def add_water_slab(bm, outline, thickness):
    """Top made of even, slightly irregular triangles so flat-shaded waves do not form spokes from one corner."""
    # Fixed seed, so every run of the script produces the same mesh.
    jitter = random.Random(7)
    interior = []
    steps = int(WELL_HOLE_RADIUS / WATER_GRID_SPACING) + 2
    for row in range(-steps, steps + 1):
        for column in range(-steps, steps + 1):
            # Every other row is shifted by half a step, which gives roughly equilateral triangles.
            x = (column + (row % 2) * 0.5) * WATER_GRID_SPACING + jitter.uniform(-WATER_GRID_JITTER, WATER_GRID_JITTER)
            y = row * WATER_GRID_SPACING * 0.866 + jitter.uniform(-WATER_GRID_JITTER, WATER_GRID_JITTER)
            if shaft_wall_distance(x, y) > WATER_GRID_MARGIN:
                interior.append((x, y))

    count = len(outline)
    points = [Vector((point[0], point[1])) for point in outline] + [Vector(point) for point in interior]
    # Output type 1 keeps only the triangles inside the outline.
    out_points, _, out_faces, out_origins, _, _ = delaunay_2d_cdt(points, [], [list(range(count))], 1, 1e-6)
    level = outline[0][2]
    verts = [bm.verts.new((point.x, point.y, level)) for point in out_points]
    rim = [None] * count
    for index, origins in enumerate(out_origins):
        for origin in origins:
            if origin < count:
                rim[origin] = verts[index]
    faces = []
    for face in out_faces:
        corners = [verts[index] for index in face]
        # Counterclockwise from above, so the surface faces +Z.
        if (corners[1].co - corners[0].co).cross(corners[2].co - corners[0].co).z < 0.0:
            corners.reverse()
        faces.append(bm.faces.new(corners))

    bottom = [bm.verts.new((point[0], point[1], level - thickness)) for point in outline]
    faces.append(bm.faces.new(list(reversed(bottom))))
    for index in range(count):
        next_index = (index + 1) % count
        faces.append(bm.faces.new((bottom[index], bottom[next_index], rim[next_index], rim[index])))

    uv_layer = bm.loops.layers.uv.verify()
    wall_layer = bm.loops.layers.uv.new("shore")
    for face in faces:
        for loop in face.loops:
            position = loop.vert.co
            loop[uv_layer].uv = (position.x / (2 * WELL_HOLE_RADIUS) + 0.5, position.y / (2 * WELL_HOLE_RADIUS) + 0.5)
            loop[wall_layer].uv = (max(shaft_wall_distance(position.x, position.y), 0.0), 0.0)


def build_well():
    material = make_material()
    water_material = make_water_material()
    rim_bottom = WELL_HEIGHT + RIM_HEIGHT
    post_height = POST_TOP - rim_bottom

    def stone_body(bm):
        # Solid block under the shaft, so the underside is one flat closed surface.
        add_prism(bm, circle_polygon(WELL_RADIUS, 0.0), (0.0, 0.0, SHAFT_FLOOR), "stone")
        add_ring_prism(bm, WELL_RADIUS, WELL_HOLE_RADIUS, SHAFT_FLOOR, WELL_HEIGHT, "stone")
        add_ring_prism(bm, RIM_RADIUS, RIM_HOLE_RADIUS, WELL_HEIGHT, rim_bottom, "stone_light")

    def water(bm):
        # Fills the shaft opening exactly. A thin closed slab, so the imported collider stays a closed volume.
        add_water_slab(bm, circle_polygon(WELL_HOLE_RADIUS, WATER_LEVEL), 0.05)

    def posts(bm):
        for side in (-1.0, 1.0):
            add_prism(bm, rect_polygon_xy(side * POST_X, 0.0, POST_SIZE / 2, POST_SIZE / 2, rim_bottom),
                      (0.0, 0.0, post_height), "wood")

    def beam(bm):
        # Windlass beam along X. It ends at the post centers, so both ends are buried inside the posts.
        polygon = [(-POST_X, BEAM_RADIUS * math.cos(angle), BEAM_HEIGHT + BEAM_RADIUS * math.sin(angle))
                   for angle in [math.tau * index / 6 for index in range(6)]]
        add_prism(bm, polygon, (2 * POST_X, 0.0, 0.0), "wood_light")

    def roof(bm):
        # Gable roof along X.
        polygon = [(-ROOF_HALF_LENGTH, -ROOF_HALF_WIDTH, ROOF_EAVE_HEIGHT),
                   (-ROOF_HALF_LENGTH, ROOF_HALF_WIDTH, ROOF_EAVE_HEIGHT),
                   (-ROOF_HALF_LENGTH, 0.0, ROOF_RIDGE_HEIGHT)]
        add_prism(bm, polygon, (2 * ROOF_HALF_LENGTH, 0.0, 0.0), "roof")

    return [
        make_part("body", material, stone_body),
        make_part("water", water_material, water),
        make_part("posts", material, posts),
        make_part("beam", material, beam),
        make_part("roof", material, roof),
    ]


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
    target = Vector((0.0, 0.0, 1.4))
    for name, location in (("front", (0.0, -6.5, 2.6)), ("three_quarter", (4.6, -4.6, 3.4)), ("top", (0.0, -1.2, 7.0))):
        camera.location = location
        camera.rotation_euler = (target - Vector(location)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(PREVIEW_DIR, f"well_{name}.png")
        bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
parts = build_well()
export_fbx(parts)
if OUT_BLEND:
    os.makedirs(os.path.dirname(os.path.abspath(OUT_BLEND)), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(OUT_BLEND), compress=True)
if PREVIEW_DIR:
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    render_previews()
print("parts:", [part.name for part in parts], "faces:", sum(len(part.data.polygons) for part in parts))
