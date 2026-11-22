# Export to Blender

Overbryce can turn an animation into a Blender scene (`.blend`). The scene can then be rendered in EEVEE or Cycles, or edited further.

```powershell
Overbryce.exe Scenes\Lumina\Lumina.ani --blender LuminaBlender
```

This writes into the folder:

| File | Contents |
|---|---|
| `Lumina.json`, `Lumina.bin` | the exported scene: objects, animation curves, meshes |
| `textures\` | the bitmaps as PNG (pure white texels are transparent, as in Overbryce) |
| `overbryce_blender.py` | the Blender script that builds the scene from the files above |
| `Lumina.blend` | the scene, built by running Blender in the background (textures packed in) |
| `Lumina.log` | Blender's output from that build |

Overbryce looks for Blender in the `BLENDER` environment variable (the path to `blender.exe`), then for the newest version under `Program Files\Blender Foundation`. If it finds none, or with `--no-blend`, it writes everything except the `.blend`. You can build the scene later yourself:

```powershell
blender -b --factory-startup --python overbryce_blender.py -- Lumina.json Lumina.blend
```

The script was tested with Blender 4.2. For Lumina the export takes about a minute and the build a few seconds.

## What is exported

- **Hierarchy.** Every group becomes an empty and every element an object under it. An element that appears in several key states is a single object, matched by its name within its group.
- **Motion.** Overbryce interpolates each segment between two key states with a non-uniform Catmull-Rom spline, which is a cubic in time. A cubic is exactly a Bezier curve, so every segment becomes one Bezier span of a Blender F-curve, with the handles computed from the spline. Segments that are not cubic (eased or with stopping) are sampled at every frame. Blender plays the motion exactly as Overbryce does, not approximately.
- **Rotation order.** Groups rotate after they move, elements before (Blender Euler `ZYX`), cameras in their own order (`ZXY`).
- **Visibility.** An element that is missing from a key state is hidden (render and viewport) for that segment.
- **Cameras.** Every camera is a Blender camera with the same field of view and clip range. The active camera of each segment is set by timeline markers bound to cameras, so the cuts match.
- **Shapes.** Spheres, cones, disks, boxes, rectangles, triangles, tori and meshes are tessellated the way Overbryce draws them. Animated shape parameters (a sphere's radius, a cone's height, …) become shape keys driven by curves.
- **Materials.** One shader per texture reads the element's colours from object properties (`ob_dif`, `ob_amb`, `ob_emi`, `ob_spec`, `ob_shin`, `ob_alpha`). The colours are animated per object while the materials stay shared. The torus's sphere mapping, transparency, wire frame and fog are reproduced. The scene's ambient light and fog are scene properties (`sc_amb`, `sc_fogcol`, `sc_fog`, `sc_fogmode`).
- **Lights.** Point lights and spot lights, with their animated colour, position, direction and switching on and off:
  - **Point lights that fade.** They use Blender's soft falloff, `1 / (d² + R²)`. This matches the original attenuation `1 / (c + l·d + q·d²)` exactly when there is no linear term, and closely otherwise.
  - **Spot lights and lights that do not fade.** In Cycles a light node gives them the original attenuation at every distance. EEVEE has only the physical falloff, so it is right at a reference distance: 3 units for a spot, the light's usual distance from the camera otherwise.
- **Tags for the way back.** Since 2.4 the scene carries what [the import](blender-import.md#round-trip-scenes-exported-by-overbryce) needs to rebuild the animation:

  | Property | Contents |
  |---|---|
  | `ob_tag` (object) | JSON: the element as `.ani` text, the key states it is in and stops at, its vertex count, and textures that change between states |
  | `ob_ext` (object) | animated: the emission, ambient and specular alphas, reflectivity, refractive index, glow, roughness, absorption, a disk's sweep angle |
  | `ob_light` (light) | animated: the direction, colours, attenuation, cutoff, exponent, light radius and switching on and off |
  | `ob_cam` (camera) | animated: the field of view, aperture, focus and clip range |
  | `ob_shape` (object) | animated: every shape parameter (radius, sizes, mesh points, …) |
  | `ob_settings` (scene) | the animation's settings as `.ani` text, without elements |
  | `ob_keytimes` (scene) | the Blender frame of every key state |

  Group offsets and aim targets are empties tagged `{"role":"pos"}` or `{"role":"aim"}`.

## Render settings

The script sets up the scene to look like Overbryce rather than like a physically based render:

- the view transform is **Raw**, so colours are not tone-mapped;
- lights cast **no shadows**, as in OpenGL 1.1;
- in Cycles, **light does not bounce** and emissive surfaces do not light others;
- 1280×720, EEVEE (32 samples); Cycles is set to 64 samples with denoising, if you switch to it.

Shadows and bounced light are switched on at the top of `overbryce_blender.py` (`SHADOWS`, `INDIRECT`). Turning them on makes the result look more like a modern render.

## Known differences

- **Overlapping lights.** OpenGL clamps the summed lighting of a surface to 1 before multiplying it by the texture, but Blender does not clamp. Where several bright lights overlap, such as on Lumina's portal ring, Blender is lighter.
- **Spot exponent.** Blender's spot lights have a uniform cone with a soft edge. They have no equivalent of the OpenGL spot exponent, which concentrates the light towards the axis.
- **Highlights.** OpenGL's Phong highlight is approximated by the Principled BSDF: its roughness comes from the shininess and its specular level from the specular colour. Highlights have a similar size and position, but not exactly the same shape.
