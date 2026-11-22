"""Builds a Blender scene from an Overbryce Animator export.

    Overbryce.exe Lumina.ani --blender LuminaBlender
    blender -b --factory-startup --python overbryce_blender.py -- Lumina.json Lumina.blend

(Overbryce runs the second line itself when it finds Blender.) Written for Blender 4.2 and later.

What the export keeps, and how:
- the scene graph: a group is an empty for its rotation with a child empty ("name.pos") for its offset, because an
  Overbryce group moves its contents before it turns them; every element is an object with the Euler order of its
  matrix;
- the motion: Bezier keys at the key states whose handles reproduce the spline of the original exactly (segments
  that are not a cubic, such as a permanent orbit, have a key at every frame);
- the shapes, tessellated like the original; changing sizes are shape keys;
- the materials: one per texture, reading the element's colours from custom properties of its object (ob_dif,
  ob_alpha, ob_emi, ob_amb, ob_spec, ob_shin), which are animated;
- the ambient light and the fog of the key states: custom properties of the scene (sc_amb, sc_fogcol, sc_fog,
  sc_fogmode), applied in every material like OpenGL's fixed-function fog;
- the lights: a point light that fades gets a radius with soft falloff, which fades as 1 / (d^2 + R^2) like the
  original's 1 / (c + l d + q d^2) (exactly without the linear term), in EEVEE and Cycles alike; spot lights and
  lights that do not fade have a light node with the original attenuation for Cycles, while EEVEE (physical falloff
  only) is right at a reference distance: 3 units for a spot, the usual distance from the camera otherwise;
- the camera cuts: timeline markers bound to the cameras;
- the visibility of the elements over time (keyed hide_render / hide_viewport).

The original has no shadows; set SHADOWS = True below to let the lights cast them. Nor does light bounce in the
original, or glowing surfaces light others; INDIRECT = True turns both on in Cycles. The colours are used as they are,
like the original (view transform "Raw"); switch to "Standard" or "AgX" for a photographic look.
"""

import json
import math
import os
import sys

import bpy
import numpy as np

SHADOWS = False
INDIRECT = False                # True: Cycles bounces light and glowing surfaces light their surroundings
ENGINE = "BLENDER_EEVEE_NEXT"   # or "CYCLES" (exact light falloff, slower)
RESOLUTION = (1280, 720)

INTERPOLATION = {0: "BEZIER", 1: "LINEAR", 2: "CONSTANT"}


def log(text):
    print("Overbryce: " + text, flush=True)


class Export:
    def __init__(self, json_path):
        with open(json_path, encoding="utf-8") as f:
            self.data = json.load(f)
        if self.data.get("format") != "overbryce-blender":
            raise ValueError(json_path + " is not an Overbryce export")
        self.folder = os.path.dirname(os.path.abspath(json_path))
        with open(os.path.join(self.folder, self.data["binary"]), "rb") as f:
            self.blob = f.read()

    def floats(self, offset, count):
        return np.frombuffer(self.blob, dtype=np.float32, count=count, offset=int(offset))

    def ints(self, offset, count):
        return np.frombuffer(self.blob, dtype=np.int32, count=count, offset=int(offset))


# ====================================================================== scene

def setup_scene(ex):
    d = ex.data
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = d["name"]
    fps = float(d["fps"])
    scene.render.fps = int(round(fps))
    scene.render.fps_base = scene.render.fps / fps
    scene.frame_start = int(d["frame_start"])
    scene.frame_end = int(d["frame_end"])
    scene.render.resolution_x, scene.render.resolution_y = RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.engine = ENGINE
    scene.render.filepath = "//render/" + d["name"] + "_"
    scene.render.image_settings.file_format = "PNG"
    try:
        scene.eevee.taa_render_samples = 32
    except AttributeError:
        pass
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    # like the original: only the lights light the scene (no bounced light; glowing surfaces do not light others)
    if not INDIRECT:
        for name in ("max_bounces", "diffuse_bounces", "glossy_bounces", "transmission_bounces", "volume_bounces"):
            setattr(scene.cycles, name, 0)
        scene.cycles.transparent_max_bounces = 64
    try:
        scene.view_settings.view_transform = "Raw"
    except TypeError:
        scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    world = bpy.data.worlds.new("Space")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0, 0, 0, 1)
    scene.world = world

    scene["sc_amb"] = [0.2, 0.2, 0.2]
    scene["sc_fogcol"] = [0.0, 0.0, 0.0]
    scene["sc_fog"] = [0.0, 1.0, 1.0]   # start, end, density
    scene["sc_fogmode"] = 0.0           # 0 none, 1 linear, 2 exp, 3 exp2
    collection = bpy.data.collections.new(d["name"])
    scene.collection.children.link(collection)
    return scene, collection


# ====================================================================== materials

class NodeBuilder:
    def __init__(self, tree):
        self.tree = tree
        self.nodes = tree.nodes
        self.links = tree.links

    def node(self, kind, x, y, **props):
        n = self.nodes.new(kind)
        n.location = (x, y)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def connect(self, source, target):
        if isinstance(source, (int, float)):
            target.default_value = source
        else:
            self.links.new(source, target)

    def math(self, op, a, b, x, y, clamp=False):
        n = self.node("ShaderNodeMath", x, y, operation=op, use_clamp=clamp)
        self.connect(a, n.inputs[0])
        if b is not None:
            self.connect(b, n.inputs[1])
        return n.outputs[0]

    def vmath(self, op, a, b, x, y):
        n = self.node("ShaderNodeVectorMath", x, y, operation=op)
        self.connect(a, n.inputs[0])
        self.connect(b, n.inputs[1])
        return n.outputs[0]

    def attr(self, kind, name, x, y):
        return self.node("ShaderNodeAttribute", x, y, attribute_type=kind, attribute_name=name)


def make_material(name, image, spheremap):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = False
    if not INDIRECT:
        for owner in (m, getattr(m, "cycles", None)):
            if owner is not None and hasattr(owner, "emission_sampling"):
                owner.emission_sampling = "NONE"   # Cycles: the glow is seen, but lights nothing
    nb = NodeBuilder(m.node_tree)
    nb.nodes.clear()

    dif = nb.attr("OBJECT", "ob_dif", -1400, 400).outputs["Color"]
    alpha = nb.attr("OBJECT", "ob_alpha", -1400, 250).outputs["Fac"]
    emi = nb.attr("OBJECT", "ob_emi", -1400, 100).outputs["Color"]
    amb = nb.attr("OBJECT", "ob_amb", -1400, -50).outputs["Color"]
    spec = nb.attr("OBJECT", "ob_spec", -1400, -200).outputs["Color"]
    shin = nb.attr("OBJECT", "ob_shin", -1400, -350).outputs["Fac"]
    scene_amb = nb.attr("VIEW_LAYER", "sc_amb", -1400, -500).outputs["Color"]
    fog_col = nb.attr("VIEW_LAYER", "sc_fogcol", -1400, -650).outputs["Color"]
    fog = nb.attr("VIEW_LAYER", "sc_fog", -1400, -800).outputs["Vector"]
    fog_mode = nb.attr("VIEW_LAYER", "sc_fogmode", -1400, -950).outputs["Fac"]

    # emission plus ambient light (OpenGL: emission + ambient * (global ambient + the lights' ambient))
    glow = nb.vmath("ADD", emi, nb.vmath("MULTIPLY", amb, scene_amb, -1150, -100), -950, 0)
    base, opacity = dif, alpha
    if image is not None:
        if spheremap:
            # GL_SPHERE_MAP: the reflection vector in eye space
            tc = nb.node("ShaderNodeTexCoord", -1600, 700)
            vt = nb.node("ShaderNodeVectorTransform", -1400, 700, vector_type="VECTOR", convert_from="WORLD", convert_to="CAMERA")
            nb.connect(tc.outputs["Reflection"], vt.inputs[0])
            sep = nb.node("ShaderNodeSeparateXYZ", -1200, 700)
            nb.connect(vt.outputs[0], sep.inputs[0])
            x, y, z = sep.outputs
            zz = nb.math("ADD", z, 1.0, -1000, 600)
            s = nb.math("ADD", nb.math("ADD", nb.math("MULTIPLY", x, x, -1000, 800), nb.math("MULTIPLY", y, y, -1000, 700), -850, 750),
                        nb.math("MULTIPLY", zz, zz, -850, 600), -700, 700)
            mm = nb.math("MULTIPLY", nb.math("POWER", s, 0.5, -550, 700), 2.0, -400, 700)
            u = nb.math("ADD", nb.math("DIVIDE", x, mm, -250, 800), 0.5, -100, 800)
            v = nb.math("ADD", nb.math("DIVIDE", y, mm, -250, 650), 0.5, -100, 650)
            comb = nb.node("ShaderNodeCombineXYZ", 50, 700)
            nb.connect(u, comb.inputs[0])
            nb.connect(v, comb.inputs[1])
            coords = comb.outputs[0]
        else:
            coords = nb.node("ShaderNodeUVMap", -1000, 700).outputs[0]
        tex = nb.node("ShaderNodeTexImage", -800, 600, image=image, interpolation="Linear", extension="REPEAT")
        nb.connect(coords, tex.inputs["Vector"])
        # GL_MODULATE: the lit colour times the texel
        base = nb.vmath("MULTIPLY", dif, tex.outputs["Color"], -500, 450)
        glow = nb.vmath("MULTIPLY", glow, tex.outputs["Color"], -500, 0)
        opacity = nb.math("MULTIPLY", alpha, tex.outputs["Alpha"], -500, 250)

    # shininess 0..1 (times 128 = the Phong exponent) as a roughness
    roughness = nb.math("POWER", nb.math("DIVIDE", 2.0, nb.math("ADD", nb.math("MULTIPLY", shin, 128.0, -1150, -350), 2.0, -1000, -350), -850, -350), 0.5, -700, -350)
    bw = nb.node("ShaderNodeRGBToBW", -1150, -220)
    nb.connect(spec, bw.inputs[0])

    bsdf = nb.node("ShaderNodeBsdfPrincipled", -300, 300)
    nb.connect(base, bsdf.inputs["Base Color"])
    nb.connect(roughness, bsdf.inputs["Roughness"])
    nb.connect(nb.math("MULTIPLY", bw.outputs[0], 0.5, -950, -220), bsdf.inputs["Specular IOR Level"])
    nb.connect(spec, bsdf.inputs["Specular Tint"])
    nb.connect(glow, bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0

    # fixed-function fog by the eye-space depth: f = 1 no fog, 0 all fog
    cam = nb.node("ShaderNodeCameraData", -1150, -1100)
    depth = cam.outputs["View Z Depth"]
    sep = nb.node("ShaderNodeSeparateXYZ", -1150, -800)
    nb.connect(fog, sep.inputs[0])
    start, end, density = sep.outputs
    linear = nb.math("DIVIDE", nb.math("SUBTRACT", end, depth, -950, -750), nb.math("MAXIMUM", nb.math("SUBTRACT", end, start, -950, -850), 1e-6, -800, -850), -650, -800, clamp=True)
    dz = nb.math("MULTIPLY", density, depth, -950, -1000)
    exp1 = nb.math("EXPONENT", nb.math("MULTIPLY", dz, -1.0, -800, -1000), None, -650, -1000)
    exp2 = nb.math("EXPONENT", nb.math("MULTIPLY", nb.math("MULTIPLY", dz, dz, -800, -1100), -1.0, -700, -1100), None, -550, -1100)

    def mode(k, y):
        n = nb.node("ShaderNodeMath", -650, y, operation="COMPARE")
        nb.connect(fog_mode, n.inputs[0])
        n.inputs[1].default_value = float(k)
        n.inputs[2].default_value = 0.25
        return n.outputs[0]

    f = nb.math("ADD",
                nb.math("ADD", mode(0, -1250), nb.math("MULTIPLY", mode(1, -1350), linear, -450, -1300), -300, -1250),
                nb.math("ADD", nb.math("MULTIPLY", mode(2, -1450), exp1, -450, -1400), nb.math("MULTIPLY", mode(3, -1550), exp2, -450, -1500), -300, -1450),
                -150, -1300)
    fog_light = nb.node("ShaderNodeEmission", -300, -600)
    nb.connect(fog_col, fog_light.inputs["Color"])
    fogged = nb.node("ShaderNodeMixShader", 0, 0)
    nb.connect(f, fogged.inputs[0])
    nb.connect(fog_light.outputs[0], fogged.inputs[1])
    nb.connect(bsdf.outputs[0], fogged.inputs[2])

    clear = nb.node("ShaderNodeBsdfTransparent", 0, -200)
    final = nb.node("ShaderNodeMixShader", 200, 0)
    nb.connect(opacity, final.inputs[0])
    nb.connect(clear.outputs[0], final.inputs[1])
    nb.connect(fogged.outputs[0], final.inputs[2])
    out = nb.node("ShaderNodeOutputMaterial", 400, 0)
    nb.connect(final.outputs[0], out.inputs["Surface"])
    return m


# ====================================================================== animation

def resolve(target, path, index, ob, inner, aim, scene):
    """The datablock and data path a channel of the export drives."""
    if target == "obj":
        return ob, path
    if target == "inner":
        return inner, path
    if target == "data":
        return (ob.data, path) if ob is not None and ob.data is not None and hasattr(ob.data, path) else (None, None)
    if target == "target":
        return aim, path
    if target == "prop":
        return ob, '["%s"]' % path
    if target == "scene":
        return scene, '["%s"]' % path
    if target == "shape":
        keys = ob.data.shape_keys if ob is not None and ob.type == "MESH" else None
        return (keys, 'key_blocks["p%d"].value' % index) if keys is not None else (None, None)
    return None, None


def set_value(block, path, index, value):
    if path.startswith('["'):
        key = path[2:-2]
        if index < 0:
            block[key] = float(value)
        else:
            block[key][index] = float(value)
    elif path.startswith("key_blocks"):
        block.key_blocks[path.split('"')[1]].value = float(value)
    elif index < 0:
        setattr(block, path, float(value))
    else:
        getattr(block, path)[index] = float(value)


_enum_foreach = True


def add_curve(ex, block, path, index, offset, count):
    global _enum_foreach
    ad = block.animation_data or block.animation_data_create()
    if ad.action is None:
        ad.action = bpy.data.actions.new(block.name)
    fc = ad.action.fcurves.new(path, index=max(index, 0))
    k = ex.floats(offset, count * 7).reshape(count, 7)
    points = fc.keyframe_points
    points.add(count)
    interp = [INTERPOLATION[int(c)] for c in k[:, 6]]
    if _enum_foreach:
        try:
            points.foreach_set("handle_left_type", np.zeros(count, dtype=np.int32))   # FREE
            points.foreach_set("handle_right_type", np.zeros(count, dtype=np.int32))
            codes = {"CONSTANT": 0, "LINEAR": 1, "BEZIER": 2}
            points.foreach_set("interpolation", np.array([codes[i] for i in interp], dtype=np.int32))
        except (TypeError, RuntimeError):
            _enum_foreach = False
    if not _enum_foreach:
        for p, i in zip(points, interp):
            p.handle_left_type = "FREE"
            p.handle_right_type = "FREE"
            p.interpolation = i
    points.foreach_set("co", np.ascontiguousarray(k[:, 0:2]).ravel())
    points.foreach_set("handle_left", np.ascontiguousarray(k[:, 2:4]).ravel())
    points.foreach_set("handle_right", np.ascontiguousarray(k[:, 4:6]).ravel())
    fc.update()
    return count


def add_steps(block, path, frames_values):
    ad = block.animation_data or block.animation_data_create()
    if ad.action is None:
        ad.action = bpy.data.actions.new(block.name)
    fc = ad.action.fcurves.new(path)
    for frame, value in frames_values:
        p = fc.keyframe_points.insert(frame, value, options={"FAST"})
        p.interpolation = "CONSTANT"
    fc.update()


def ensure_arrays(o, ob, scene):
    """Creates the custom property arrays the channels key (an F-curve only drives an element that exists)."""
    sizes = {}
    for entry in list(o.get("init", [])) + list(o.get("curves", [])):
        target, path, index = entry[0], entry[1], entry[2]
        if target in ("prop", "scene") and index >= 0:
            block = ob if target == "prop" else scene
            if block is not None:
                key = (id(block), path)
                sizes[key] = (block, path, max(sizes.get(key, (None, None, -1))[2], index))
    for block, path, index in sizes.values():
        current = block.get(path)
        try:
            values = [float(x) for x in current] if current is not None else []
        except TypeError:
            values = [float(current)]
        if len(values) <= index:
            block[path] = values + [0.0] * (index + 1 - len(values))


def apply_channels(ex, o, ob, inner, aim, scene):
    ensure_arrays(o, ob, scene)
    for target, path, index, value in o.get("init", []):
        block, dp = resolve(target, path, index, ob, inner, aim, scene)
        if block is not None:
            set_value(block, dp, index, value)
    keys = 0
    for target, path, index, offset, count in o.get("curves", []):
        block, dp = resolve(target, path, index, ob, inner, aim, scene)
        if block is not None:
            keys += add_curve(ex, block, dp, index, offset, count)
    return keys


def apply_visibility(ob, visibility):
    pairs = [(visibility[i], visibility[i + 1]) for i in range(0, len(visibility), 2)]
    if len(pairs) == 1 and pairs[0][1] == 1 and pairs[0][0] <= 0:
        return
    ob.hide_render = ob.hide_viewport = pairs[0][1] == 0 or pairs[0][0] > 0
    steps = [(f, 0.0 if v else 1.0) for f, v in pairs]
    if pairs[0][0] > 0:
        steps.insert(0, (pairs[0][0] - 1, 1.0))
    add_steps(ob, "hide_render", steps)
    add_steps(ob, "hide_viewport", steps)


# ====================================================================== objects

def build_mesh(ex, m, name):
    me = bpy.data.meshes.new(name)
    nv, nt = m["vert_count"], m["tri_count"]
    me.vertices.add(nv)
    me.vertices.foreach_set("co", ex.floats(m["verts"], nv * 3))
    if nt > 0:
        me.loops.add(nt * 3)
        me.loops.foreach_set("vertex_index", ex.ints(m["tris"], nt * 3))
        me.polygons.add(nt)
        me.polygons.foreach_set("loop_start", np.arange(0, nt * 3, 3, dtype=np.int32))
        try:
            me.polygons.foreach_set("loop_total", np.full(nt, 3, dtype=np.int32))
        except (AttributeError, RuntimeError, TypeError):
            pass
        uv = me.uv_layers.new(name="UVMap")
        uv.data.foreach_set("uv", ex.floats(m["uvs"], nt * 6))
        me.polygons.foreach_set("use_smooth", np.ones(nt, dtype=bool))
    me.update()
    me.materials.append(None)
    return me


def falloff_nodes(light, falloff):
    """Cycles: the original attenuation 1 / (c + l d + q d^2) instead of the physical 1 / d^2 (EEVEE has only the latter
    and is right at the reference distance, where d^2 / (c + l d + q d^2) equals the given reference value)."""
    c, l, q, reference = falloff
    light.use_nodes = True
    nb = NodeBuilder(light.node_tree)
    emission = nb.nodes.get("Emission")
    if emission is None:
        return
    fo = nb.node("ShaderNodeLightFalloff", -800, 0)
    fo.inputs["Strength"].default_value = 1.0
    d2, d = fo.outputs["Constant"], fo.outputs["Linear"]   # d^2 and d for a strength of 1
    den = nb.math("ADD", nb.math("ADD", nb.math("MULTIPLY", d, float(l), -600, 100), nb.math("MULTIPLY", d2, float(q), -600, -50), -450, 50), float(c), -300, 50)
    ratio = nb.math("DIVIDE", d2, nb.math("MAXIMUM", den, 1e-6, -200, 50), -100, 0)
    nb.connect(nb.math("DIVIDE", ratio, max(float(reference), 1e-9), 50, 0), emission.inputs["Strength"])


def build(json_path, blend_path):
    ex = Export(json_path)
    d = ex.data
    scene, collection = setup_scene(ex)

    images = []
    for file in d["textures"]:
        img = bpy.data.images.load(os.path.join(ex.folder, file), check_existing=True)
        img.alpha_mode = "STRAIGHT"
        try:
            img.colorspace_settings.name = "Non-Color"   # the texels are used as they are, like OpenGL did
        except TypeError:
            pass
        images.append(img)
    materials = {}

    def material(tex, spheremap, translucent):
        key = (tex, spheremap, translucent)
        if key not in materials:
            name = "Overbryce" if tex < 0 else os.path.splitext(os.path.basename(d["textures"][tex]))[0]
            name += (" sphere map" if spheremap else "") + (" translucent" if translucent else "")
            m = make_material(name, images[tex] if tex >= 0 else None, spheremap)
            if translucent and hasattr(m, "surface_render_method"):
                m.surface_render_method = "BLENDED"   # smooth see-through in EEVEE (dithered is grainy)
            materials[key] = m
        return materials[key]

    meshes = {}
    objects, inners, aims = [], [], []
    keys = 0
    for o in d["objects"]:
        kind = o["kind"]
        parent = inners[o["parent"]] if o["parent"] >= 0 else None
        inner = aim = None
        if kind == "group":
            ob = bpy.data.objects.new(o["name"], None)
            ob.empty_display_size = 0.25
            collection.objects.link(ob)
            inner = ob
            if o.get("inner"):
                inner = bpy.data.objects.new(o["name"] + ".pos", None)
                inner.empty_display_size = 0.1
                collection.objects.link(inner)
                inner.parent = ob
        elif kind == "camera":
            cd = bpy.data.cameras.new(o["name"])
            cd.sensor_fit = "VERTICAL"
            cd.sensor_height = d["sensor_height"]
            cd.clip_start, cd.clip_end = o.get("clip", (d["clip_start"], d["clip_end"]))
            ob = bpy.data.objects.new(o["name"], cd)
            collection.objects.link(ob)
        elif kind == "light":
            ld = bpy.data.lights.new(o["name"], o["light"])
            # a light of radius R with soft falloff fades as 1 / (d^2 + R^2), like the original's attenuation
            ld.shadow_soft_size = o["radius"]
            ld.use_shadow = SHADOWS
            if hasattr(ld, "cycles") and hasattr(ld.cycles, "cast_shadow"):
                ld.cycles.cast_shadow = SHADOWS   # (the sun of the opening sits inside its glowing sphere)
            if hasattr(ld, "use_soft_falloff"):
                ld.use_soft_falloff = o["radius"] > 0
            if o["light"] == "SPOT":
                ld.show_cone = False
            if "falloff" in o:
                falloff_nodes(ld, o["falloff"])
            ob = bpy.data.objects.new(o["name"], ld)
            collection.objects.link(ob)
            if o["light"] == "SPOT":
                aim = bpy.data.objects.new(o["name"] + ".aim", None)
                aim.empty_display_size = 0.1
                collection.objects.link(aim)
                aim.parent = parent
                track = ob.constraints.new("DAMPED_TRACK")
                track.target = aim
                track.track_axis = "TRACK_NEGATIVE_Z"
        else:
            m = d["meshes"][o["mesh"]]
            if m["keys"]:
                me = build_mesh(ex, m, o["name"])
            else:
                me = meshes.get(o["mesh"]) or meshes.setdefault(o["mesh"], build_mesh(ex, m, o["name"]))
            ob = bpy.data.objects.new(o["name"], me)
            collection.objects.link(ob)
            ob.material_slots[0].link = "OBJECT"
            ob.material_slots[0].material = material(o["texture"], o["spheremap"], o.get("translucent", False))
            for name, value in (("ob_dif", [0.8, 0.8, 0.8]), ("ob_alpha", 1.0), ("ob_emi", [0.0, 0.0, 0.0]),
                                ("ob_amb", [0.6, 0.6, 0.6]), ("ob_spec", [0.0, 0.0, 0.0]), ("ob_shin", 0.0)):
                ob[name] = value
            if m["keys"]:
                ob.shape_key_add(name="Basis", from_mix=False)
                for param, offset in zip(m["keys"][0::2], m["keys"][1::2]):
                    sk = ob.shape_key_add(name="p%d" % param, from_mix=False)
                    sk.data.foreach_set("co", ex.floats(offset, m["vert_count"] * 3))
                    sk.slider_min, sk.slider_max = -10.0, 10.0
                    sk.value = 0.0
            if o.get("wire"):
                wire = ob.modifiers.new("Wire", "WIREFRAME")
                wire.thickness = 0.02
        ob.parent = parent
        # for the Blender import: the element as it was, and which objects only help to place it
        if "tag" in o:
            ob["ob_tag"] = o["tag"]
        if inner is not None and inner is not ob:
            inner["ob_tag"] = '{"role": "pos"}'
        if aim is not None:
            aim["ob_tag"] = '{"role": "aim"}'
        if "rotation_mode" in o:
            ob.rotation_mode = o["rotation_mode"]
        keys += apply_channels(ex, o, ob, inner, aim, scene)
        apply_visibility(ob, o["visibility"])
        objects.append(ob)
        inners.append(inner if inner is not None else ob)
        aims.append(aim)

    keys += apply_channels(ex, d["scene"], None, None, None, scene)
    if "settings" in d["scene"]:
        scene["ob_settings"] = d["scene"]["settings"]
        scene["ob_keytimes"] = [float(f) for f in d["scene"]["keytimes"]]

    markers = d["markers"]
    first_camera = None
    for frame, index in zip(markers[0::2], markers[1::2]):
        if index < 0:
            continue
        marker = scene.timeline_markers.new(objects[index].name, frame=int(frame))
        marker.camera = objects[index]
        first_camera = first_camera or objects[index]
    scene.camera = first_camera
    scene.frame_set(scene.frame_start)

    for w in d.get("warnings", []):
        log("warning: " + w)
    log("%d objects, %d meshes, %d materials, %d keys" % (len(objects), len(bpy.data.meshes), len(materials), keys))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend_path), compress=True)
    log("saved " + os.path.abspath(blend_path))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args:
        raise SystemExit("usage: blender -b --python overbryce_blender.py -- export.json [output.blend]")
    source = os.path.abspath(args[0])
    target = args[1] if len(args) > 1 else os.path.splitext(source)[0] + ".blend"
    build(source, target)
