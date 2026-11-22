"""Builds the Blender test scene for the Overbryce import (nothing binary is committed; run this to make it):

    blender -b --factory-startup --python make_test_scene.py -- test_scene.blend

The scene exercises what the import handles: a hierarchy of empties, primitives that the import recognises (sphere,
cube, cylinder, cone, torus, plane, disk), a mesh with modifiers (bevel + subdivision), a text object, Principled
materials with an image texture (packed into the file), a procedural texture, glass, metal and emission, point, spot,
sun and area lights, two cameras switched by markers (one with depth of field), keyframed motion, an animated shape
key, a hide_render toggle and a small armature whose two meshes are each bound rigidly to one bone.
"""

import math
import os
import sys

import bpy
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
target = os.path.abspath(args[0] if args else "test_scene.blend")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = "ImportTest"
scene.frame_start, scene.frame_end = 1, 96
scene.render.fps = 24
scene.render.resolution_x, scene.render.resolution_y = 960, 540
scene.render.engine = "CYCLES"
scene.cycles.samples = 32


def material(name, color, metallic=0.0, roughness=0.5, **inputs):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    for key, value in inputs.items():
        bsdf.inputs[key].default_value = value
    return m


def link(ob):
    scene.collection.objects.link(ob)
    return ob


def add(op, name, mat=None, **kw):
    op(**kw)
    ob = bpy.context.active_object
    ob.name = name
    if mat is not None:
        ob.data.materials.append(mat)
    return ob


# ---------------------------------------------------------------- textures

size = 256
yy, xx = np.mgrid[0:size, 0:size]
checker = ((xx // 32 + yy // 32) % 2).astype(np.float32)
pixels = np.zeros((size, size, 4), np.float32)
pixels[..., 0] = 0.15 + 0.7 * checker
pixels[..., 1] = 0.35 + 0.4 * checker
pixels[..., 2] = 0.2 + 0.1 * (yy / size)
pixels[..., 3] = 1
pixels[0:8, :, :3] = 1.0          # a pure white band: must not become transparent in Overbryce
img = bpy.data.images.new("Checker", size, size, alpha=False)
img.pixels.foreach_set(pixels.ravel())
img.pack()

ground_mat = bpy.data.materials.new("Ground")
ground_mat.use_nodes = True
nt = ground_mat.node_tree
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = img
mapping = nt.nodes.new("ShaderNodeMapping")
mapping.inputs["Scale"].default_value = (4, 4, 1)
uv = nt.nodes.new("ShaderNodeTexCoord")
nt.links.new(uv.outputs["UV"], mapping.inputs["Vector"])
nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
nt.links.new(tex.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])

noise_mat = bpy.data.materials.new("Marble")
noise_mat.use_nodes = True
nt = noise_mat.node_tree
noise = nt.nodes.new("ShaderNodeTexNoise")
noise.inputs["Scale"].default_value = 6
ramp = nt.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].color = (0.05, 0.1, 0.4, 1)
ramp.color_ramp.elements[1].color = (0.9, 0.85, 0.7, 1)
nt.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
nt.links.new(ramp.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])

# ---------------------------------------------------------------- objects

ground = add(bpy.ops.mesh.primitive_plane_add, "Ground", ground_mat, size=20)
red = material("RedMetal", (0.8, 0.1, 0.05), metallic=1.0, roughness=0.25)
sphere = add(bpy.ops.mesh.primitive_uv_sphere_add, "Ball", red, radius=1, location=(-4, 0, 1), segments=32, ring_count=16)
bpy.ops.object.shade_smooth()

blue = material("Blue", (0.1, 0.2, 0.8), roughness=0.4)
box = add(bpy.ops.mesh.primitive_cube_add, "RoundedBox", blue, size=1.6, location=(0, -3, 0.8))
bevel = box.modifiers.new("Bevel", "BEVEL")
bevel.width = 0.15
bevel.segments = 3
sub = box.modifiers.new("Subdivision", "SUBSURF")
sub.levels = sub.render_levels = 1
bpy.ops.object.shade_smooth()

green = material("Green", (0.1, 0.6, 0.15))
cyl = add(bpy.ops.mesh.primitive_cylinder_add, "Pillar", green, radius=0.5, depth=3, location=(4, 3, 1.5))
cone = add(bpy.ops.mesh.primitive_cone_add, "Cone", green, radius1=0.8, radius2=0, depth=1.6, location=(4, -3, 0.8))
yellow = material("Yellow", (0.9, 0.7, 0.1), roughness=0.3)
torus = add(bpy.ops.mesh.primitive_torus_add, "Ring", yellow, major_radius=1.2, minor_radius=0.25, location=(0, 4, 1.4),
            rotation=(math.radians(70), 0, 0))
disk = add(bpy.ops.mesh.primitive_circle_add, "Disk", yellow, radius=0.8, fill_type="NGON", location=(-6, -5, 0.01))

glass = material("Glass", (0.9, 0.95, 1.0), roughness=0.0, **{"Transmission Weight": 1.0, "IOR": 1.45})
lens = add(bpy.ops.mesh.primitive_ico_sphere_add, "GlassBall", glass, radius=0.7, subdivisions=3, location=(-1.5, 2, 0.7))
bpy.ops.object.shade_smooth()

glow = material("Glow", (1, 0.5, 0.1), **{"Emission Color": (1, 0.45, 0.1, 1), "Emission Strength": 6.0})
lamp_bulb = add(bpy.ops.mesh.primitive_uv_sphere_add, "Bulb", glow, radius=0.2, location=(2, 0, 3))

monkey = add(bpy.ops.mesh.primitive_monkey_add, "Suzanne", noise_mat, size=1.4, location=(1.5, -0.5, 1.0))
monkey.modifiers.new("Subdivision", "SUBSURF").render_levels = 1
bpy.ops.object.shade_smooth()

bpy.ops.object.text_add(location=(-5, 5, 0.01))
text = bpy.context.active_object
text.name = "Title"
text.data.body = "Overbryce"
text.data.extrude = 0.05
text.data.materials.append(yellow)

# a hierarchy: a spinning empty with two children, one of them with a child of its own
pivot = link(bpy.data.objects.new("Pivot", None))
pivot.location = (0, 0, 3.5)
moon = add(bpy.ops.mesh.primitive_uv_sphere_add, "Moon", blue, radius=0.3, location=(1.5, 0, 0))
moon.parent = pivot
moon.location = (1.5, 0, 0)
moonlet = add(bpy.ops.mesh.primitive_cube_add, "Moonlet", red, size=0.2)
moonlet.parent = moon
moonlet.location = (0.5, 0, 0)
moonlet.scale = (1, 2, 1)   # a static non-uniform scale: baked into the mesh
pivot.rotation_euler = (0, 0, 0)
pivot.keyframe_insert("rotation_euler", frame=1)
pivot.rotation_euler = (0, 0, math.radians(360))
pivot.keyframe_insert("rotation_euler", frame=96)

# keyframed motion and a hide_render toggle
sphere.keyframe_insert("location", frame=1)
sphere.location = (-4, -4, 2.5)
sphere.keyframe_insert("location", frame=48)
sphere.location = (-2, 0, 1)
sphere.keyframe_insert("location", frame=96)
box.rotation_euler = (0, 0, 0)
box.keyframe_insert("rotation_euler", frame=1)
box.rotation_euler = (0, 0, math.radians(90))
box.keyframe_insert("rotation_euler", frame=96)
cone.hide_render = False
cone.keyframe_insert("hide_render", frame=1)
cone.hide_render = True
cone.keyframe_insert("hide_render", frame=40)
cone.hide_render = False
cone.keyframe_insert("hide_render", frame=70)

# an animated shape key on a grid (a wave)
bpy.ops.mesh.primitive_grid_add(x_subdivisions=16, y_subdivisions=16, size=3, location=(5, 0, 0.3))
flag = bpy.context.active_object
flag.name = "Flag"
flag.data.materials.append(green)
flag.shape_key_add(name="Basis")
wave = flag.shape_key_add(name="Wave")
for v in wave.data:
    v.co.z += 0.4 * math.sin(v.co.x * 3)
wave.value = 0
wave.keyframe_insert("value", frame=1)
wave.value = 1
wave.keyframe_insert("value", frame=48)
wave.value = 0
wave.keyframe_insert("value", frame=96)

# an animated scale (vertex animation in Overbryce)
lamp_bulb.scale = (1, 1, 1)
lamp_bulb.keyframe_insert("scale", frame=1)
lamp_bulb.scale = (2, 2, 2)
lamp_bulb.keyframe_insert("scale", frame=96)

# a rigid armature: two bones, each mesh weighted fully to one of them
arm_data = bpy.data.armatures.new("Rig")
rig = link(bpy.data.objects.new("Rig", arm_data))
rig.location = (-3, 4, 0)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
upper = arm_data.edit_bones.new("Upper")
upper.head, upper.tail = (0, 0, 0), (0, 0, 1.5)
lower = arm_data.edit_bones.new("Lower")
lower.head, lower.tail = (0, 0, 1.5), (0, 0, 2.8)
lower.parent = upper
lower.use_connect = True
bpy.ops.object.mode_set(mode="OBJECT")
for bone, z0, z1 in (("Upper", 0, 1.5), ("Lower", 1.5, 2.8)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(-3, 4, (z0 + z1) / 2))
    part = bpy.context.active_object
    part.name = "Arm" + bone
    part.scale = (0.3, 0.3, (z1 - z0) / 2 * 0.95)
    bpy.ops.object.transform_apply(scale=True)
    part.data.materials.append(red if bone == "Upper" else yellow)
    group = part.vertex_groups.new(name=bone)
    group.add(range(len(part.data.vertices)), 1.0, "REPLACE")
    part.parent = rig
    part.matrix_parent_inverse = rig.matrix_world.inverted()
    part.modifiers.new("Armature", "ARMATURE").object = rig
pose = rig.pose.bones
pose["Upper"].rotation_mode = pose["Lower"].rotation_mode = "XYZ"
for frame, a, b in ((1, 0, 0), (48, 30, -60), (96, 0, 0)):
    pose["Upper"].rotation_euler = (math.radians(a), 0, 0)
    pose["Lower"].rotation_euler = (math.radians(b), 0, 0)
    pose["Upper"].keyframe_insert("rotation_euler", frame=frame)
    pose["Lower"].keyframe_insert("rotation_euler", frame=frame)

# ---------------------------------------------------------------- lights

def light(name, kind, location, energy, color=(1, 1, 1), rotation=(0, 0, 0), **props):
    data = bpy.data.lights.new(name, kind)
    data.energy = energy
    data.color = color
    for k, v in props.items():
        setattr(data, k, v)
    ob = link(bpy.data.objects.new(name, data))
    ob.location = location
    ob.rotation_euler = [math.radians(r) for r in rotation]
    return ob


light("Key", "POINT", (3, -4, 6), 1500, (1, 0.95, 0.85), shadow_soft_size=0.3)
spot = light("Spot", "SPOT", (-6, -6, 7), 3000, (0.7, 0.8, 1), rotation=(40, 0, -45), spot_size=math.radians(45), spot_blend=0.3)
spot.data.keyframe_insert("energy", frame=1)
spot.data.energy = 500
spot.data.keyframe_insert("energy", frame=96)
light("Sun", "SUN", (0, 0, 10), 2.0, (1, 0.9, 0.8), rotation=(50, 10, 30), angle=math.radians(2))
light("Fill", "AREA", (-5, 3, 4), 300, (0.8, 0.9, 1), rotation=(-50, 0, 0), size=2)

world = bpy.data.worlds.new("World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.07, 0.12, 1)
scene.world = world

# ---------------------------------------------------------------- cameras

def camera(name, location, target, lens=35):
    data = bpy.data.cameras.new(name)
    data.lens = lens
    ob = link(bpy.data.objects.new(name, data))
    ob.location = location
    direction = (np.array(target) - np.array(location)).astype(float)
    from mathutils import Vector
    ob.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()
    return ob


wide = camera("Wide", (12, -14, 9), (0, 0, 1))
close = camera("Close", (-7, -8, 3), (-2, 0, 1.2), lens=50)
close.data.dof.use_dof = True
close.data.dof.focus_distance = 8
close.data.dof.aperture_fstop = 2.8
wide.location = (12, -14, 9)
wide.keyframe_insert("location", frame=1)
wide.location = (14, -10, 7)
wide.keyframe_insert("location", frame=60)
scene.camera = wide
m = scene.timeline_markers.new("Wide", frame=1)
m.camera = wide
m = scene.timeline_markers.new("Close", frame=60)
m.camera = close

scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=target)
print("saved", target)
