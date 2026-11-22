# Import from Blender

Overbryce 2.4 reads a Blender scene (`.blend`) and turns it into an animation: its objects, their motion, materials, lights, cameras, world and settings. What does not map onto the 1998 model is approximated as closely as the model allows, and the import writes down everything it approximated or left out.

In the editor: **File › Import › Blender scene…** asks for the `.blend` file and for the `.ani` file to save. It opens the result and shows the report. On the command line:

```powershell
Overbryce.exe scene.blend --import-blender out\scene.ani [--static] [--axis auto|yup|zup] [--tolerance 0.001]
```

| Option | Meaning |
|---|---|
| `--static` | only the first frame, as one key state |
| `--axis` | `zup` turns Blender's Z up into Overbryce's Y up; `yup` takes the coordinates as they are; `auto` (default) does the first unless the scene was exported by Overbryce |
| `--tolerance` | how far the animation between key states may stray from Blender's, as a part of the scene's size (default 0.001); `0` makes every frame a key state |

The import writes next to the `.ani`:

| File | Contents |
|---|---|
| `scene.ani` | the animation |
| `scene.meshes` | the polygon meshes (see [the file format](ani-format.md#polygon-meshes-24)) |
| `scene_*.bmp` | the textures |
| `scene.import.txt` | the report |

Blender is found as for the export: the `BLENDER` environment variable, then the newest version under `Program Files\Blender Foundation`. It was tested with Blender 4.2.

## How it works

1. **Blender reads the scene.** Overbryce runs Blender in the background with `overbryce_import.py` (built into the program). The script steps through the frames and reads the *evaluated* scene, after modifiers, geometry nodes, constraints, drivers, rigs and simulations. It writes a JSON file and a binary file with the arrays.
2. **Overbryce builds the animation** (`BlenderSceneImporter` in `Overbryce.Core`). It needs no Blender, so the unit tests build the interchange file in memory.

## What is imported

- **Hierarchy.** An object with children (an empty, an armature, a mesh with children) becomes a group. Its own geometry becomes an element inside the group.
- **Placement.** Every world matrix is split into a turn and a stretch (a polar decomposition). The turn and position become the element's. The stretch (scale, shear, a mirror) is baked into the polygon mesh, so non-uniform and negative scale come out right. A stretch that changes over time morphs the mesh.
- **Geometry.**
  - Meshes, curves, text, surfaces and metaballs all become polygon meshes after their modifiers and geometry nodes, with one element per material slot.
  - A plain box or rectangle becomes Overbryce's own box or rectangle when it is untextured, centred and does not change shape.
  - Instances from geometry nodes, particles and collection instances become elements of their own, up to 4,000.
- **Changing shape.** Shape keys, deforming modifiers, soft rigs, cloth and baked simulations become vertex animation: a polygon mesh per key state, morphed between them.
- **Rigs.** A mesh that a rig (or a hook, a lattice …) only moves as a whole gets that motion as its placement. Objects parented to bones simply follow them.
- **Materials.**
  - The Principled BSDF and the plain diffuse, glossy, glass, emission, transparent, mix and add shaders become Phong colours, plus the modern values: reflectivity, refractive index, glow and roughness.
  - The base colour's image becomes the texture, with the UV transform of a Mapping node. Pure white texels are nudged to 254, because Overbryce draws pure white as see-through. Where the image's alpha is used, see-through texels become pure white.
  - Animated materials are sampled per frame.
- **Lights.**
  - **Point and spot lights** get an attenuation fitted to Blender's falloff. A spot gets its cutoff from its size, and an exponent from its blend.
  - **A sun** becomes a distant point light, 20 scene radii away.
  - **An area light** becomes a wide spot with a light radius of its size.
  - All lights are scaled by one factor that matches Blender's brightness on the scene's own surfaces. Overbryce adds the displayed light of each light, while Blender adds the light before the view transform.
  - Overbryce has eight lights: per key state the eight brightest at the scene's centre are kept.
- **Cameras.** The field of view, clip range and depth of field (aperture and focus) are imported. The active camera follows `scene.camera`, so camera markers become cuts.
- **Visibility.** Objects hidden from the render (`hide_render`, also animated) are missing from those key states.
- **World.**
  - The world colour lights the scene as ambient light. It is also a glowing sphere *Sky* around the active camera, because Overbryce clears the picture to black.
  - A world volume becomes exponential fog.
- **Colour.** AgX and Filmic views are approximated by AgX's curve. Colours, lights and textures are converted so that a surface looks as bright as in Blender. The Standard view uses the plain display curve.
- **Settings.** The frame rate becomes the animation's. Modern rendering is switched on, with the exposure from Blender's. The ray tracing samples, bounces and motion blur come from Cycles, and the texture size from the largest image.

## Key states

Overbryce animates by key states with a spline between them, not by per-frame curves. The import samples every frame and then keeps as few frames as the spline needs to follow Blender within the tolerance:

- **Fixed key states.** The first and last frame, every change of visibility, every camera cut, and both frames of a jump are always key states. A jump is an element that leaps from one frame to the next; its key states get *Stop at this state*, so the spline does not swing around it.
- **Added key states.** The worst in-between frame of each segment is added while it strays more than the tolerance. Positions are compared against the tolerance times the scene's size, colours to within 0.01.

The test scene below (96 frames) needs 54 key states.

## Round trip: scenes exported by Overbryce

A scene that Overbryce exported (see [Export to Blender](blender-export.md)), and maybe edited in Blender, comes back as the elements it was made from:

- **The tags.** The export tags every object with its element. The element is stored as `.ani` text, with the key states it is in and stops at. Values Blender has no place for are kept in custom properties, such as attenuations, alphas and shape sizes. The scene keeps the animation's settings and the frames of its key states.
- **The import.** It samples Blender at exactly those moments. It rebuilds each tagged element at each key state with the place, rotation, colours and sizes Blender has then. Blender has the permanent rotation and orbit in its curves, so the import takes them off again.
- **Edits in Blender.**
  - Moving, turning and recolouring (the `ob_*` properties) come back.
  - An element that was scaled, or whose mesh was edited, becomes a polygon mesh.
  - New objects are imported like any Blender object, at the key states of the export.
  - Changing the timing of keys is not followed: the key states stay the export's.

Gule, Disky, Meshe, Mesto and Scena from the 1998 thesis render pixel-identical after a round trip, in the classic and the modern renderer. Lumina does too, except for one texture: it differs by 0.7 levels in 255, because it is resampled on the way through Blender's PNG copy.

## Fidelity

`tools/BlenderTest/make_test_scene.py` builds a test scene in Blender: a hierarchy, a scaled and mirrored object, a curve and text, an instanced array, a shape-key morph, a rig, metal, glass, emission, a texture, point, spot, sun and area lights, two cameras with a cut, and a world colour. `tools/BlenderTest/image_diff.py` compares two renders.

At 2 s (frame 49) of the test scene, the mean difference from the Cycles render (AgX view) is 17 of 255 in the classic and modern renderers, and 16.7 ray traced. It comes from the approximations below.

## What is approximated or left out

The report lists these per scene. In general:

- **Materials.**
  - Reflections are not tinted, so a coloured metal reflects grey.
  - Procedural textures and other node setups are replaced by their plain value (not baked into textures).
  - Subsurface scattering, sheen, clearcoat and displacement are left out.
- **Lights.**
  - Lights add up in display values in Overbryce; the calibration makes the scene's typical surfaces right.
  - Blender's spot blend becomes an OpenGL spot exponent.
  - A sun and an area light are approximated as described above.
- **World.** An environment image lights the scene with its average colour; the sky sphere shows only the world colour.
- **Cameras.** Lens shift is left out. An orthographic or panoramic camera becomes a perspective one.
- **Not imported:** Grease Pencil, volumes (except the world's), hair curves, and more than 8 lights at once.
- **Meshes that change their number of vertices** (some simulations and geometry nodes) keep their first shape.
