"""Reads a Blender scene for Overbryce Animator (the reverse of overbryce_blender.py).

    blender -b scene.blend --factory-startup --python overbryce_import.py -- out.json [options]

Options:
    --frames START:END   the frames to sample (default: the scene's frame range)
    --static             sample only the first frame (no animation)
    --textures DIR       where the textures go, as 24-bit BMP files (default: next to out.json)
    --prefix NAME        prefix of the texture file names (default: none)
    --max-texture N      largest texture side in texels (default 1024)

Overbryce.exe runs this itself (--import-blender). It writes out.json and out.bin. Everything is taken from the
evaluated scene, so modifiers, geometry nodes, constraints, drivers, rigs and simulations are already applied:

- per object and frame: the world matrix and whether it is rendered; per light, camera and animated material its
  values; per deforming mesh its vertex positions (only for meshes that really change);
- per mesh (first frame it appears in): the triangles with a normal and texture coordinates per corner, split by
  material slot;
- per material: the Principled BSDF (or Diffuse / Glossy / Glass / Emission) inputs, and its base colour image,
  converted to BMP (pure white is transparent in Overbryce, so white texels become 254 unless the image's alpha cuts
  a hole there);
- the world colour (and environment image), the render settings, the timeline markers and the active camera per
  frame.

The C# side (BlenderSceneImporter) turns this into key states. This script changes the scene in memory only (it
makes the viewport show what renders) and never saves it.
"""

import json
import math
import os
import struct
import sys

import bpy
import numpy as np

FORMAT = "overbryce-blender-scene"
VERSION = 1
CORNER_PRECISION = 1e4      # normals within 1e-4 and texture coordinates within 1e-5 are one vertex
DEFORM_PROBES = 4           # frames compared to find meshes that change shape


def log(text):
    print("Overbryce: " + text, flush=True)


def set_frame(scene, frame):
    """Goes to a frame, which may lie between two (the key states of an Overbryce export)."""
    whole = int(math.floor(frame))
    scene.frame_set(whole, subframe=float(frame) - whole)


class Options:
    def __init__(self, argv):
        args = argv[argv.index("--") + 1:] if "--" in argv else []
        if not args:
            raise SystemExit("usage: blender -b scene.blend --python overbryce_import.py -- out.json [options]")
        self.json = os.path.abspath(args[0])
        self.frames = None
        self.static = False
        self.textures = os.path.dirname(self.json)
        self.prefix = ""
        self.max_texture = 1024
        i = 1
        while i < len(args):
            a = args[i]
            if a == "--static":
                self.static = True
            elif a == "--frames" and i + 1 < len(args):
                i += 1
                start, end = args[i].split(":")
                self.frames = (int(start), int(end))
            elif a == "--textures" and i + 1 < len(args):
                i += 1
                self.textures = os.path.abspath(args[i])
            elif a == "--prefix" and i + 1 < len(args):
                i += 1
                self.prefix = args[i]
            elif a == "--max-texture" and i + 1 < len(args):
                i += 1
                self.max_texture = max(16, int(args[i]))
            i += 1


class Blob:
    """The binary side file: little-endian arrays, addressed by byte offset."""

    def __init__(self, path):
        self.file = open(path, "wb")
        self.offset = 0

    def write(self, array, dtype):
        data = np.ascontiguousarray(array, dtype=dtype).tobytes()
        at = self.offset
        self.file.write(data)
        self.offset += len(data)
        return at

    def floats(self, array):
        return self.write(array, "<f4")

    def ints(self, array):
        return self.write(array, "<i4")

    def close(self):
        self.file.close()


# ====================================================================== preparation

def has_fcurve(idblock, path):
    ad = getattr(idblock, "animation_data", None)
    if ad is None:
        return False
    if ad.action is not None and any(fc.data_path == path for fc in ad.action.fcurves):
        return True
    return any(d.data_path == path for d in ad.drivers)


def prepare(scene, notes):
    """Makes the viewport depsgraph evaluate what a render shows: render visibility, render levels, render modifiers."""
    view_layer = bpy.context.view_layer

    def unhide(lc):
        try:
            lc.hide_viewport = False
        except AttributeError:
            pass
        for child in lc.children:
            unhide(child)
    unhide(view_layer.layer_collection)
    for coll in bpy.data.collections:
        coll.hide_viewport = coll.hide_render

    for ob in scene.objects:
        animated = has_fcurve(ob, "hide_render")
        ob.hide_viewport = False if animated else ob.hide_render
        try:
            ob.hide_set(False)
        except RuntimeError:
            pass
        for mod in ob.modifiers:
            mod.show_viewport = mod.show_render
            if mod.type == "SUBSURF":
                mod.levels = mod.render_levels
            elif mod.type == "MULTIRES":
                mod.levels = mod.render_levels
        for ps in getattr(ob, "particle_systems", []):
            s = ps.settings
            s.display_percentage = 100
            if s.child_type != "NONE":
                s.child_nbr = s.rendered_child_count
            if s.type == "HAIR" and s.render_type == "PATH":
                notes.append("dropped|Hair of %s (particle system %s)" % (ob.name, ps.name))
        data = ob.data
        if ob.type in ("CURVE", "FONT", "SURFACE") and getattr(data, "render_resolution_u", 0) > 0:
            data.resolution_u = data.render_resolution_u
        if ob.type == "META" and getattr(data, "render_resolution", 0) > 0:
            data.resolution = data.render_resolution
        if ob.type == "GPENCIL" or ob.type == "GREASEPENCIL":
            notes.append("dropped|Grease Pencil object %s" % ob.name)
        if ob.type == "VOLUME":
            notes.append("dropped|Volume object %s" % ob.name)
        if ob.type == "CURVES":
            notes.append("dropped|Hair curves %s" % ob.name)
    view_layer.update()


# ====================================================================== textures

class Textures:
    def __init__(self, options):
        self.options = options
        self.list = []
        self.index = {}
        os.makedirs(options.textures, exist_ok=True)

    def add(self, image, use_alpha, notes):
        key = (image.name, bool(use_alpha))
        if key in self.index:
            return self.index[key]
        index = -1
        try:
            index = self._convert(image, use_alpha)
        except Exception as ex:  # an image Blender cannot read (missing file, unknown format)
            notes.append("approximated|Texture %s could not be read (%s): the base colour is used" % (image.name, ex))
        self.index[key] = index
        return index

    def _convert(self, image, use_alpha):
        w, h = image.size
        if w == 0 or h == 0:
            raise ValueError("no pixels")
        px = np.empty(w * h * 4, np.float32)
        image.pixels.foreach_get(px)
        px = px.reshape(h, w, 4)
        if image.is_float:
            # linear scene values: to display values like Blender's Standard view transform
            c = np.clip(px[..., :3], 0, 1)
            px[..., :3] = np.where(c <= 0.0031308, 12.92 * c, 1.055 * np.power(c, 1 / 2.4) - 0.055)
        px = self._fit(px)
        h, w = px.shape[:2]
        rgb = np.clip(np.round(px[..., :3] * 255), 0, 255).astype(np.uint8)
        white = (rgb[..., 0] == 255) & (rgb[..., 1] == 255) & (rgb[..., 2] == 255)
        rgb[white] = 254                      # pure white is transparent in Overbryce
        if use_alpha:
            rgb[px[..., 3] < 0.5] = 255       # ... which is what a cut-out needs
        base = bpy.path.clean_name(os.path.splitext(image.name)[0]) or "texture"
        name = "%s%s%s.bmp" % (self.options.prefix, base, "_a" if use_alpha else "")
        n = 1
        while name in (t["file"] for t in self.list):
            n += 1
            name = "%s%s%s_%d.bmp" % (self.options.prefix, base, "_a" if use_alpha else "", n)
        write_bmp(os.path.join(self.options.textures, name), rgb)
        self.list.append({"file": name, "width": int(w), "height": int(h), "alpha": bool(use_alpha), "image": image.name})
        return len(self.list) - 1

    def _fit(self, px):
        h, w = px.shape[:2]
        k = int(math.ceil(max(w, h) / float(self.options.max_texture)))
        if k <= 1:
            return px
        h2, w2 = h // k, w // k
        return px[:h2 * k, :w2 * k].reshape(h2, k, w2, k, 4).mean(axis=(1, 3))


def write_bmp(path, rgb):
    """A 24-bit BMP, bottom row first (as Blender stores pixels)."""
    h, w = rgb.shape[:2]
    row = (w * 3 + 3) & ~3
    data = np.zeros((h, row), np.uint8)
    data[:, :w * 3] = rgb[:, :, ::-1].reshape(h, w * 3)
    with open(path, "wb") as f:
        f.write(struct.pack("<2sIHHI", b"BM", 54 + row * h, 0, 0, 54))
        f.write(struct.pack("<IiiHHIIiiII", 40, w, h, 1, 24, 0, row * h, 2835, 2835, 0, 0))
        f.write(data.tobytes())


# ====================================================================== materials

def linked_from(socket):
    """The node and output socket that feed an input, skipping reroutes and muted links."""
    links = [l for l in socket.links if not l.is_muted] if socket.is_linked else []
    if not links:
        return None, None
    node, out = links[0].from_node, links[0].from_socket
    while node.type == "REROUTE":
        if not node.inputs[0].is_linked:
            return None, None
        l = node.inputs[0].links[0]
        node, out = l.from_node, l.from_socket
    return node, out


def find_image(node, depth=0):
    """The first image texture upstream of a node (through mix, gamma, hue and similar nodes)."""
    if node is None or depth > 8:
        return None
    if node.type == "TEX_IMAGE" and node.image is not None:
        return node
    for inp in node.inputs:
        src, _ = linked_from(inp)
        found = find_image(src, depth + 1)
        if found is not None:
            return found
    return None


def uv_transform(image_node):
    """A Mapping node between the texture coordinates and the image: its translation, rotation and scale."""
    src, _ = linked_from(image_node.inputs["Vector"])
    if src is None or src.type != "MAPPING":
        return None
    def val(name):
        inp = src.inputs.get(name)
        return list(inp.default_value) if inp is not None and not inp.is_linked else None
    loc, rot, scale = val("Location"), val("Rotation"), val("Scale")
    if loc is None or rot is None or scale is None:
        return None
    return {"type": src.vector_type, "location": loc[:2], "rotation": rot[2], "scale": scale[:2]}


def rgb(value):
    return [float(value[0]), float(value[1]), float(value[2])]


class MaterialReader:
    def __init__(self, textures, notes):
        self.textures = textures
        self.notes = notes

    def read(self, mat):
        m = {"name": mat.name, "base": [0.8, 0.8, 0.8], "alpha": 1.0, "metallic": 0.0, "roughness": 0.5, "ior": 1.45,
             "transmission": 0.0, "emission": [0.0, 0.0, 0.0], "emission_strength": 0.0, "specular": 0.5,
             "texture": -1, "uv": None, "procedural": [], "unsupported": [], "shader": "none"}
        if not mat.use_nodes or mat.node_tree is None:
            m["base"] = rgb(mat.diffuse_color)
            m["alpha"] = float(mat.diffuse_color[3])
            m["metallic"] = float(mat.metallic)
            m["roughness"] = float(mat.roughness)
            m["shader"] = "viewport"
            return m
        out = None
        for target in ("ALL", "CYCLES", "EEVEE"):
            try:
                out = mat.node_tree.get_output_node(target)
            except Exception:
                out = None
            if out is not None:
                break
        if out is None:
            return m
        shader, _ = linked_from(out.inputs["Surface"])
        nodes = mat.node_tree.nodes
        if any(n.type == "ATTRIBUTE" and n.attribute_name == "ob_dif" for n in nodes):
            # a material of the Overbryce export: the colours are the object's (ob_dif ...), only its image matters
            m["shader"] = "overbryce"
            image = next((n for n in nodes if n.type == "TEX_IMAGE" and n.image is not None), None)
            if image is not None:
                m["texture"] = self.textures.add(image.image, True, self.notes)
            return m
        self._shader(shader, m, 1.0)
        if out.inputs.get("Displacement") is not None and out.inputs["Displacement"].is_linked:
            m["unsupported"].append("displacement")
        return m

    def _color(self, node, name, m, field, weight):
        inp = node.inputs.get(name)
        if inp is None:
            return None
        if inp.is_linked:
            src, sock = linked_from(inp)
            image = find_image(src)
            if image is not None and field == "base" and m["texture"] < 0:
                alpha_used = self._alpha_from(node, image)
                m["texture"] = self.textures.add(image.image, alpha_used, self.notes)
                m["uv"] = uv_transform(image)
                if src is not image:
                    m["unsupported"].append("nodes between the image and " + name)
                return [1.0, 1.0, 1.0]
            m["procedural"].append(name)
            return rgb(inp.default_value) if hasattr(inp.default_value, "__len__") else None
        v = inp.default_value
        return rgb(v) if hasattr(v, "__len__") else float(v)

    def _alpha_from(self, node, image):
        inp = node.inputs.get("Alpha")
        if inp is None or not inp.is_linked:
            return False
        src, sock = linked_from(inp)
        return src is image and sock.name == "Alpha"

    def _value(self, node, name, m, default):
        inp = node.inputs.get(name)
        if inp is None:
            return default
        if inp.is_linked:
            src, _ = linked_from(inp)
            if name not in ("Alpha",) and not (src is not None and src.type == "TEX_IMAGE" and name == "Alpha"):
                m["unsupported"].append("a texture on " + name)
            return float(inp.default_value) if not hasattr(inp.default_value, "__len__") else default
        v = inp.default_value
        return float(v) if not hasattr(v, "__len__") else default

    def _shader(self, node, m, weight):
        if node is None:
            return
        t = node.type
        if t == "MIX_SHADER":
            fac_in = node.inputs[0]
            fac = 0.5 if fac_in.is_linked else float(fac_in.default_value)
            a, _ = linked_from(node.inputs[1])
            b, _ = linked_from(node.inputs[2])
            # a transparent part sets the opacity; otherwise the stronger of the two shaders wins
            if a is not None and a.type == "BSDF_TRANSPARENT":
                m["alpha"] *= fac
                self._shader(b, m, weight)
            elif b is not None and b.type == "BSDF_TRANSPARENT":
                m["alpha"] *= 1 - fac
                self._shader(a, m, weight)
            else:
                if fac_in.is_linked:
                    m["unsupported"].append("a mix of shaders by a texture")
                self._shader(a if fac <= 0.5 else b, m, weight)
            return
        if t == "ADD_SHADER":
            a, _ = linked_from(node.inputs[0])
            b, _ = linked_from(node.inputs[1])
            for s in (a, b):
                if s is not None and s.type == "EMISSION":
                    self._emission(s, m)
                else:
                    self._shader(s, m, weight)
            return
        m["shader"] = t.lower()
        if t == "BSDF_PRINCIPLED":
            m["base"] = self._color(node, "Base Color", m, "base", weight) or m["base"]
            m["metallic"] = self._value(node, "Metallic", m, 0.0)
            m["roughness"] = self._value(node, "Roughness", m, 0.5)
            m["ior"] = self._value(node, "IOR", m, 1.45)
            m["alpha"] *= self._value(node, "Alpha", m, 1.0)
            m["transmission"] = self._value(node, "Transmission Weight", m, 0.0) if node.inputs.get("Transmission Weight") else self._value(node, "Transmission", m, 0.0)
            m["specular"] = self._value(node, "Specular IOR Level", m, 0.5) if node.inputs.get("Specular IOR Level") else self._value(node, "Specular", m, 0.5)
            em = self._color(node, "Emission Color", m, "emission", weight) if node.inputs.get("Emission Color") else self._color(node, "Emission", m, "emission", weight)
            if em is not None:
                m["emission"] = em
            m["emission_strength"] = self._value(node, "Emission Strength", m, 1.0 if node.inputs.get("Emission Strength") is None else 0.0)
            for name in ("Normal", "Coat Weight", "Sheen Weight", "Subsurface Weight"):
                inp = node.inputs.get(name)
                if inp is not None and (inp.is_linked if name == "Normal" else (not inp.is_linked and float(inp.default_value) > 0.01)):
                    m["unsupported"].append(name.replace(" Weight", "").lower())
        elif t == "BSDF_DIFFUSE":
            m["base"] = self._color(node, "Color", m, "base", weight) or m["base"]
            m["roughness"] = 1.0
            m["specular"] = 0.0
        elif t in ("BSDF_GLOSSY", "BSDF_ANISOTROPIC"):
            m["base"] = self._color(node, "Color", m, "base", weight) or m["base"]
            m["metallic"] = 1.0
            m["roughness"] = self._value(node, "Roughness", m, 0.5)
        elif t in ("BSDF_GLASS", "BSDF_REFRACTION"):
            m["base"] = self._color(node, "Color", m, "base", weight) or m["base"]
            m["transmission"] = 1.0
            m["roughness"] = self._value(node, "Roughness", m, 0.0)
            m["ior"] = self._value(node, "IOR", m, 1.45)
        elif t == "EMISSION":
            m["base"] = [0.0, 0.0, 0.0]
            m["specular"] = 0.0
            self._emission(node, m)
        elif t == "BSDF_TRANSPARENT":
            m["alpha"] = 0.0
        elif t == "BACKGROUND":
            self._emission(node, m)
        else:
            m["unsupported"].append("the %s shader" % node.bl_label)

    def _emission(self, node, m):
        c = self._color(node, "Color", m, "emission", 1.0)
        if c is not None:
            m["emission"] = c
        m["emission_strength"] = self._value(node, "Strength", m, 1.0)


MATERIAL_TRACK = ("base", "alpha", "metallic", "roughness", "ior", "transmission", "emission", "emission_strength", "specular")


def material_numbers(m):
    """The animatable values of a material as one flat list (MATERIAL_TRACK order)."""
    out = []
    for k in MATERIAL_TRACK:
        v = m[k]
        out.extend(v if isinstance(v, list) else [v])
    return out


def is_animated(idblock):
    ad = getattr(idblock, "animation_data", None)
    return ad is not None and (ad.action is not None or len(ad.drivers) > 0)


# ====================================================================== meshes

def extract_mesh(ob, depsgraph):
    """The evaluated mesh of an object: positions per vertex, corners (vertex, normal, uv), triangles per slot."""
    try:
        me = ob.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
    except RuntimeError:
        return None
    if me is None:
        return None
    try:
        me.calc_loop_triangles()
        nv, nl, nt = len(me.vertices), len(me.loops), len(me.loop_triangles)
        if nt == 0:
            return None
        co = np.empty(nv * 3, np.float32)
        me.vertices.foreach_get("co", co)
        loop_vert = np.empty(nl, np.int32)
        me.loops.foreach_get("vertex_index", loop_vert)
        tri_loops = np.empty(nt * 3, np.int32)
        me.loop_triangles.foreach_get("loops", tri_loops)
        tri_mat = np.empty(nt, np.int32)
        me.loop_triangles.foreach_get("material_index", tri_mat)
        normals = np.empty(nl * 3, np.float32)
        me.corner_normals.foreach_get("vector", normals)
        uv_layer = next((l for l in me.uv_layers if l.active_render), None) or me.uv_layers.active
        uvs = None
        if uv_layer is not None:
            uvs = np.empty(nl * 2, np.float32)
            uv_layer.data.foreach_get("uv", uvs)
        # corners with the same vertex, normal and texture coordinates are one vertex
        key = [loop_vert.astype(np.int64)[:, None], np.round(normals.reshape(-1, 3) * CORNER_PRECISION).astype(np.int64)]
        if uvs is not None:
            key.append(np.round(uvs.reshape(-1, 2) * CORNER_PRECISION * 10).astype(np.int64))
        key = np.concatenate(key, axis=1)
        _, first, inverse = np.unique(key, axis=0, return_index=True, return_inverse=True)
        inverse = inverse.ravel()
        slots = []
        for slot in ob.material_slots:
            slots.append(slot.material)
        if not slots:
            slots = [mat for mat in me.materials]
        return {
            "co": co,
            "corner_vertex": loop_vert[first],
            "corner_normal": normals.reshape(-1, 3)[first].ravel(),
            "corner_uv": uvs.reshape(-1, 2)[first].ravel() if uvs is not None else None,
            "tris": inverse[tri_loops].astype(np.int32),
            "tri_mat": np.clip(tri_mat, 0, max(0, len(slots) - 1)) if slots else np.zeros(nt, np.int32),
            "slots": slots,
        }
    finally:
        ob.to_mesh_clear()


def mesh_positions(ob, depsgraph):
    try:
        me = ob.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
    except RuntimeError:
        return None
    if me is None:
        return None
    try:
        co = np.empty(len(me.vertices) * 3, np.float32)
        me.vertices.foreach_get("co", co)
        return co
    finally:
        ob.to_mesh_clear()


GEOMETRY = ("MESH", "CURVE", "SURFACE", "FONT", "META")


def may_deform(ob):
    """Whether the evaluated shape of an object can change over time (checked by sampling)."""
    if ob.type not in GEOMETRY:
        return False
    if ob.type != "MESH":
        return is_animated(ob.data) or len(ob.modifiers) > 0
    keys = ob.data.shape_keys
    if keys is not None and is_animated(keys):
        return True
    return len(ob.modifiers) > 0 or ob.parent_type in ("ARMATURE", "LATTICE")


# ====================================================================== lights, cameras, world

LIGHT_TYPES = {"POINT": 0, "SPOT": 1, "SUN": 2, "AREA": 3}


def light_numbers(light):
    """type, colour (3), power, spot size, spot blend, radius, area size (2), sun angle, casts shadows, soft falloff."""
    size_y = light.size_y if light.type == "AREA" and light.shape in ("RECTANGLE", "ELLIPSE") else getattr(light, "size", 0.0)
    return [LIGHT_TYPES.get(light.type, 0), *rgb(light.color), float(light.energy),
            float(getattr(light, "spot_size", 0.0)), float(getattr(light, "spot_blend", 0.0)),
            float(getattr(light, "shadow_soft_size", 0.0)), float(getattr(light, "size", 0.0)), float(size_y),
            float(getattr(light, "angle", 0.0)), 1.0 if light.use_shadow else 0.0,
            1.0 if getattr(light, "use_soft_falloff", True) else 0.0]


def camera_numbers(ob, scene):
    """vertical fov (rad), clip start, clip end, focus distance, aperture radius, uses dof, type, shift x, shift y, ortho scale."""
    cam = ob.data
    r = scene.render
    rx = r.resolution_x * r.pixel_aspect_x
    ry = r.resolution_y * r.pixel_aspect_y
    aspect = rx / max(ry, 1e-6)
    fit = cam.sensor_fit
    sensor = cam.sensor_width
    if fit == "AUTO":
        fit = "HORIZONTAL" if rx >= ry else "VERTICAL"
    elif fit == "VERTICAL":
        sensor = cam.sensor_height
    lens = max(cam.lens, 1e-3)
    if fit == "HORIZONTAL":
        fov_y = 2 * math.atan(math.tan(math.atan(sensor / (2 * lens))) / aspect)
    else:
        fov_y = 2 * math.atan(sensor / (2 * lens))
    dof = cam.dof
    focus = dof.focus_distance
    if dof.focus_object is not None:
        fo = dof.focus_object.matrix_world.translation
        to = ob.matrix_world.inverted() @ fo
        focus = max(-to.z, 1e-3)
    unit = scene.unit_settings.scale_length or 1.0
    aperture = (cam.lens / 1000.0) / (2 * max(dof.aperture_fstop, 1e-3)) / unit
    kind = {"PERSP": 0, "ORTHO": 1, "PANO": 2}.get(cam.type, 0)
    return [fov_y, cam.clip_start, cam.clip_end, focus, aperture, 1.0 if dof.use_dof else 0.0, kind, cam.shift_x, cam.shift_y,
            cam.ortho_scale]


def world_info(scene, textures, notes):
    w = {"color": [0.0, 0.0, 0.0], "strength": 1.0, "texture": -1, "volume": None, "animated": False}
    world = scene.world
    if world is None:
        return w
    w["animated"] = is_animated(world) or (world.node_tree is not None and is_animated(world.node_tree))
    if not world.use_nodes or world.node_tree is None:
        w["color"] = rgb(world.color)
        return w
    out = world.node_tree.get_output_node("ALL") or world.node_tree.get_output_node("CYCLES")
    if out is None:
        return w
    bg, _ = linked_from(out.inputs["Surface"])
    if bg is not None and bg.type == "BACKGROUND":
        c = bg.inputs["Color"]
        w["strength"] = float(bg.inputs["Strength"].default_value)
        if c.is_linked:
            src, _ = linked_from(c)
            if src is not None and src.type == "TEX_ENVIRONMENT" and src.image is not None:
                w["texture"] = textures.add(src.image, False, notes)
                w["projection"] = src.projection
                w["color"] = average_color(src.image)
            else:
                notes.append("approximated|The world shader: its plain colour is used")
                w["color"] = rgb(c.default_value)
        else:
            w["color"] = rgb(c.default_value)
    elif bg is not None:
        notes.append("approximated|The world shader (%s): black is used" % bg.bl_label)
    vol, _ = linked_from(out.inputs["Volume"]) if out.inputs.get("Volume") is not None else (None, None)
    if vol is not None:
        density = vol.inputs.get("Density")
        color = vol.inputs.get("Color")
        w["volume"] = {"density": float(density.default_value) if density is not None else 0.1,
                       "color": rgb(color.default_value) if color is not None else [0.5, 0.5, 0.5]}
    return w


def average_color(image):
    try:
        w, h = image.size
        px = np.empty(w * h * 4, np.float32)
        image.pixels.foreach_get(px)
        return [float(x) for x in px.reshape(-1, 4)[:, :3].mean(axis=0)]
    except Exception:
        return [0.5, 0.5, 0.5]


def world_numbers(scene):
    """The world's plain colour and strength in the current frame (when they are animated)."""
    world = scene.world
    if world is None or world.node_tree is None:
        return [0.0, 0.0, 0.0, 1.0]
    out = world.node_tree.get_output_node("ALL") or world.node_tree.get_output_node("CYCLES")
    bg, _ = linked_from(out.inputs["Surface"]) if out is not None else (None, None)
    if bg is None or bg.type != "BACKGROUND":
        return [0.0, 0.0, 0.0, 1.0]
    return [*rgb(bg.inputs["Color"].default_value), float(bg.inputs["Strength"].default_value)]


# ====================================================================== sampling

OVERBRYCE_PROPS = ("ob_dif", "ob_amb", "ob_emi", "ob_spec", "ob_shin", "ob_alpha", "ob_ext", "ob_light", "ob_cam",
                   "ob_shape", "ob_order", "ob_tag")


def props_of(ob):
    """The custom properties the Overbryce export puts on objects (for an exact round trip)."""
    out = {}
    for k in OVERBRYCE_PROPS:
        if k in ob.keys():
            v = ob[k]
            if isinstance(v, str):
                out[k] = v
                continue
            try:
                out[k] = [float(x) for x in v]
            except (TypeError, ValueError):
                out[k] = float(v) if isinstance(v, (int, float)) else str(v)
    return out


class Record:
    """One object (or instance) over the sampled frames."""

    def __init__(self, key, name, kind, frames):
        self.key = key
        self.name = name
        self.kind = kind
        self.parent = None
        self.matrices = np.full((frames, 16), np.nan, np.float32)
        self.visible = np.zeros(frames, np.uint8)
        self.mesh = -1
        self.data = None            # per-frame values of lights and cameras
        self.props = None           # per-frame Overbryce custom properties
        self.deform = None          # (frame index -> vertex positions)
        self.static_props = {}
        self.instance = False
        self.original = None


def sample(scene, options, notes):
    blob_path = os.path.splitext(options.json)[0] + ".bin"
    blob = Blob(blob_path)
    textures = Textures(options)
    materials = MaterialReader(textures, notes)

    start, end = options.frames or (scene.frame_start, scene.frame_end)
    if options.static:
        end = start
    frames = list(range(start, end + 1, max(1, scene.frame_step)))
    if not frames:
        frames = [scene.frame_current]
    # a scene exported by Overbryce keeps the key states of the export: only their moments are sampled (they may fall
    # between frames)
    keytimes = scene.get("ob_keytimes")
    if keytimes is not None and not options.static and not options.frames:
        moments = {}
        for k in keytimes:
            k = float(k)
            if start - 1e-4 <= k <= end + 1e-4:
                moments.setdefault(round(k, 4), k)
        if moments:
            frames = [moments[k] for k in sorted(moments)]
    nf = len(frames)
    log("sampling %d frames (%d..%d)" % (nf, frames[0], frames[-1]))

    # which geometry changes shape: compare a few frames
    probes = sorted(set([frames[0], frames[-1]] + [frames[(len(frames) - 1) * k // (DEFORM_PROBES - 1)] for k in range(DEFORM_PROBES)]))
    candidates = [ob for ob in scene.objects if may_deform(ob)]
    first_shape, deforming, topology_changes = {}, set(), set()
    if nf > 1 and candidates:
        for f in probes:
            set_frame(scene, f)
            dg = bpy.context.evaluated_depsgraph_get()
            for ob in candidates:
                co = mesh_positions(ob.evaluated_get(dg), dg)
                if co is None:
                    continue
                ref = first_shape.setdefault(ob.name, co)
                if len(ref) != len(co):
                    topology_changes.add(ob.name)
                elif ref is not co and np.max(np.abs(ref - co)) > 1e-6:
                    deforming.add(ob.name)
        for name in sorted(topology_changes):
            notes.append("approximated|%s changes its number of vertices over time: its first shape is kept" % name)
        deforming -= topology_changes
    log("%d of %d candidate meshes deform" % (len(deforming), len(candidates)))

    records = {}
    order = []
    meshes = []
    mesh_index = {}
    material_list = []
    material_index = {}
    animated_materials = {}
    camera_per_frame = []
    world_frames = []
    world = world_info(scene, textures, notes)

    def material_of(mat):
        if mat is None:
            return -1
        if mat.name not in material_index:
            m = materials.read(mat)
            material_index[mat.name] = len(material_list)
            material_list.append(m)
            if is_animated(mat) or (mat.node_tree is not None and is_animated(mat.node_tree)):
                animated_materials[mat.name] = (mat, np.zeros((nf, len(material_numbers(m))), np.float32))
        return material_index[mat.name]

    def add_mesh(ob, dg, ptr_cache):
        ptr = ob.data.as_pointer() if ob.data is not None and ob.type == "MESH" and not ob.modifiers else None
        if ptr is not None and ptr in ptr_cache:
            return ptr_cache[ptr]
        mk = ("data", ob.original.data.name) if ob.type == "MESH" and not ob.modifiers and ob.data.shape_keys is None and not ob.is_instancer else None
        if mk is not None and mk in mesh_index:
            return mesh_index[mk]
        m = extract_mesh(ob, dg)
        if m is None:
            return -1
        entry = {
            "vertex_count": int(len(m["co"]) // 3),
            "positions": blob.floats(m["co"]),
            "corner_count": int(len(m["corner_vertex"])),
            "corner_vertex": blob.ints(m["corner_vertex"]),
            "corner_normal": blob.floats(m["corner_normal"]),
            "corner_uv": blob.floats(m["corner_uv"]) if m["corner_uv"] is not None else -1,
            "triangle_count": int(len(m["tris"]) // 3),
            "triangles": blob.ints(m["tris"]),
            "triangle_slot": blob.ints(m["tri_mat"]),
            "slots": [material_of(mat) for mat in m["slots"]] or [-1],
        }
        meshes.append(entry)
        index = len(meshes) - 1
        if mk is not None:
            mesh_index[mk] = index
        if ptr is not None:
            ptr_cache[ptr] = index
        return index

    for fi, frame in enumerate(frames):
        set_frame(scene, frame)
        dg = bpy.context.evaluated_depsgraph_get()
        ptr_cache = {}
        camera_per_frame.append(scene.camera.name if scene.camera is not None else "")
        if world["animated"]:
            world_frames.append(world_numbers(scene))
        for mat_name, (mat, track) in animated_materials.items():
            track[fi] = material_numbers(materials.read(mat))
        for inst in dg.object_instances:
            ob = inst.object
            if inst.is_instance and ob.original == inst.parent.original and ob.original.type in ("CURVE", "FONT", "SURFACE", "META"):
                continue  # the evaluated mesh of a curve or text: to_mesh of the object itself already has it
            if inst.is_instance:
                parent = inst.parent.original
                key = "%s|%s|%s" % (parent.name, ob.original.name if ob.original else ob.name, ".".join(str(i) for i in inst.persistent_id))
            else:
                parent = None
                key = ob.original.name
            rec = records.get(key)
            if rec is None:
                original = ob.original
                rec = Record(key, original.name if not inst.is_instance else original.name, ob.type, nf)
                rec.instance = inst.is_instance
                rec.original = original
                if inst.is_instance:
                    rec.parent = parent.name
                elif original.parent is not None:
                    rec.parent = original.parent.name
                    if original.parent_type == "BONE" and original.parent_bone:
                        rec.static_props["parent_bone"] = original.parent_bone
                records[key] = rec
                order.append(rec)
                if ob.type in GEOMETRY and (inst.show_self or inst.is_instance):
                    rec.mesh = add_mesh(ob, dg, ptr_cache)
                if ob.type == "LIGHT":
                    rec.data = np.zeros((nf, len(light_numbers(ob.data))), np.float32)
                elif ob.type == "CAMERA":
                    rec.data = np.zeros((nf, len(camera_numbers(ob, scene))), np.float32)
                p = props_of(original)
                if p:
                    rec.static_props["overbryce"] = {k: v for k, v in p.items() if isinstance(v, str)}
                    numeric = [k for k, v in p.items() if not isinstance(v, str)]
                    if numeric:
                        rec.props = {k: np.zeros((nf, len(p[k]) if isinstance(p[k], list) else 1), np.float32) for k in numeric}
                    if "ob_tag" in p and not inst.is_instance:
                        # the object's own animated values (exact, unlike a decomposed matrix): location, rotation, scale
                        rec.props = rec.props or {}
                        rec.props["_local"] = np.zeros((nf, 9), np.float32)
                if not inst.is_instance and ob.original.name in deforming and rec.mesh >= 0:
                    rec.deform = {}
            if not inst.is_instance and not inst.show_self and ob.type in GEOMETRY:
                rec.static_props["hidden_instancer"] = True
            rec.matrices[fi] = np.array(inst.matrix_world, dtype=np.float32).ravel()
            if inst.is_instance:
                visible = not inst.parent.original.hide_render   # instances show while their instancer does
            else:
                visible = not rec.original.hide_render
            rec.visible[fi] = 1 if visible else 0
            if rec.data is not None:
                rec.data[fi] = light_numbers(ob.data) if ob.type == "LIGHT" else camera_numbers(ob.original, scene)
            if rec.props is not None:
                for k, track in rec.props.items():
                    if k == "_local":
                        o_ = rec.original
                        track[fi] = list(o_.location) + list(o_.rotation_euler) + list(o_.scale)
                        continue
                    v = rec.original.get(k)
                    if v is not None:
                        try:
                            track[fi] = [float(x) for x in v]
                        except TypeError:
                            track[fi] = [float(v)]
            if rec.deform is not None:
                co = mesh_positions(ob, dg)
                if co is not None:
                    rec.deform[fi] = blob.floats(co)
        if fi % 10 == 0 or fi == nf - 1:
            log("frame %d (%d/%d): %d objects" % (frame, fi + 1, nf, len(records)))

    # ------------------------------------------------------------------ the file
    by_name = {}
    for i, rec in enumerate(order):
        if not rec.instance:
            by_name[rec.key] = i

    objects = []
    for rec in order:
        o = {"name": rec.name, "kind": rec.kind, "instance": rec.instance,
             "parent": by_name.get(rec.parent, -1) if rec.parent is not None else -1,
             "mesh": rec.mesh,
             "matrices": blob.floats(rec.matrices),
             "visible": blob.write(rec.visible, np.uint8)}
        o.update(rec.static_props)
        if rec.data is not None:
            o["data"] = blob.floats(rec.data)
            o["data_width"] = int(rec.data.shape[1])
        if rec.props:
            o["props"] = {k: [blob.floats(v), int(v.shape[1])] for k, v in rec.props.items()}
        if rec.deform:
            o["deform"] = [rec.deform.get(fi, -1) for fi in range(nf)]
        if rec.original is not None and rec.original.type == "ARMATURE":
            o["bones"] = [b.name for b in rec.original.data.bones]
        objects.append(o)

    for name, (mat, track) in animated_materials.items():
        material_list[material_index[name]]["track"] = blob.floats(track)
        material_list[material_index[name]]["track_width"] = int(track.shape[1])
    if world_frames:
        world["track"] = blob.floats(np.array(world_frames, np.float32))

    markers = sorted((m.frame, m.camera.name) for m in scene.timeline_markers if m.camera is not None)
    blob.close()
    return {
        "frames": frames,
        "objects": objects,
        "meshes": meshes,
        "materials": material_list,
        "material_track": list(MATERIAL_TRACK),
        "textures": textures.list,
        "world": world,
        "cameras": camera_per_frame,
        "markers": [[f, c] for f, c in markers],
        "binary": os.path.basename(blob_path),
    }


def scene_info(scene):
    r = scene.render
    info = {
        "name": scene.name,
        "file": bpy.data.filepath,
        "blender": bpy.app.version_string,
        "fps": r.fps / (r.fps_base or 1.0),
        "frame_start": scene.frame_start,
        "frame_end": scene.frame_end,
        "resolution": [r.resolution_x * r.resolution_percentage // 100, r.resolution_y * r.resolution_percentage // 100],
        "engine": r.engine,
        "unit_scale": scene.unit_settings.scale_length,
        "view_transform": scene.view_settings.view_transform,
        "exposure": scene.view_settings.exposure,
        "motion_blur": r.use_motion_blur,
        "shutter": r.motion_blur_shutter,
        "cycles_samples": getattr(scene.cycles, "samples", 0) if hasattr(scene, "cycles") else 0,
        "cycles_bounces": getattr(scene.cycles, "max_bounces", 0) if hasattr(scene, "cycles") else 0,
        "eevee_ao": bool(getattr(scene.eevee, "use_gtao", False)),
        "eevee_bloom": bool(getattr(scene.eevee, "use_bloom", False)),
        "props": {},
    }
    for k in ("sc_amb", "sc_fogcol", "sc_fog", "sc_fogmode", "ob_settings", "ob_keytimes"):
        if k in scene.keys():
            v = scene[k]
            if isinstance(v, str):
                info["props"][k] = v
                continue
            try:
                info["props"][k] = [float(x) for x in v]
            except TypeError:
                info["props"][k] = float(v)
    return info


def main():
    options = Options(sys.argv)
    scene = bpy.context.scene
    notes = []
    prepare(scene, notes)
    data = {"format": FORMAT, "version": VERSION, "scene": scene_info(scene)}
    data.update(sample(scene, options, notes))
    data["notes"] = notes
    with open(options.json, "w", encoding="utf-8") as f:
        json.dump(data, f)
    log("%d objects, %d meshes, %d materials, %d textures -> %s" % (len(data["objects"]), len(data["meshes"]),
                                                                  len(data["materials"]), len(data["textures"]), options.json))


if __name__ == "__main__":
    main()
