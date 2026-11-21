# Plan: an offline ray tracer (step 2)

**Status: planned, not implemented.** This page records the design so that the work can start from it later. Step 1, the [modern real-time renderer](modern-rendering.md), is done. It already stores the material values that the ray tracer will use.

## Goal

Render exported frames (`--frames`, `--snapshot`, *File › Export frames*) with exact effects that the real-time renderers can only approximate:

- reflections of reflections, including flat mirrors without parallax errors;
- refraction through any number of glass surfaces, with total internal reflection;
- soft shadows from lights with a size;
- depth of field and motion blur;
- clean antialiasing with many samples per pixel.

It does **not** replace the editor's real-time view. The classic renderer stays the default and the reference for the 1998 look. A minute per frame is acceptable for export; the editor gets at most a progressive preview of one frame.

## Design principles

1. **Same scene, same light.** With every new effect turned off, a ray-traced frame must match the classic renderer: the same lighting formulas, fog, texturing, sphere-mapped torus and white-transparent texels. This gives a strong test (see *Validation*) and means that existing animations look as their authors meant, only better.
2. **No new data needed.** Old files render without changes. Reflectivity, refractive index, glow and casts-shadows (2.1) already describe most materials. New values are optional and use the extension block.
3. **Compatibility as in 2.1.** New settings go into the [extension block](ani-format.md#the-overbryce-21-extension-block) as new keys and a new record, so:
   - the 1998 program and 2.0 ignore them;
   - 2.1 keeps them when it saves, as a test already checks with an invented `Raytracer` record.
4. **UI-free and headless.** The tracer is a library that needs only `Overbryce.Core`: no OpenGL, no WinForms. It can be tested in xUnit and run on a machine without a GPU.
5. **No new dependencies.** It uses .NET only: `System.Numerics`, `System.Runtime.Intrinsics` and `Parallel.For`.

## Architecture

```text
Overbryce.Core          model, interpolation, transforms, .ani + extension block
  └ Geometry/           (moved here from App) the tessellation shared by both renderers
Overbryce.Raytrace      new: scene compiler, BVH, intersection, shading, sampler, film
Overbryce.App           UI, OpenGL renderers; calls Overbryce.Raytrace for export and preview
```

### 1. Shared tessellation

`Overbryce.App/Rendering/Geometry.cs` builds the triangles of every shape the way GLU did (slices, stacks, loops, Bezier grid). It uses OpenTK only for `PrimitiveType`.

- Move it to `Overbryce.Core/Geometry` with its own primitive-mode enum.
- The OpenGL renderer converts that enum to OpenTK.
- The ray tracer turns strips and fans into indexed triangles.

Both renderers then see exactly the same facets.

### 2. Scene compiler

For a time `t`:

1. `Animation.Evaluate(t)` gives the scene.
2. The scene is flattened with `Transforms.World` into a list of instances:
   - the world matrix and its inverse;
   - the material: colours, shininess, alpha, texture, reflectivity, IOR, glow, casts-shadows;
   - a reference to the object-space geometry.
3. Lights get world positions and directions, the same as `SceneRenderer.SetupLights`.
4. The camera gets its world position, its orientation and its vertical field of view.

**Textures** come through a provider delegate (`Func<string, TextureImage?>`). The App supplies the decoder with the same rules as `TextureCache`, so the library itself needs no image decoding.

### 3. Geometry modes

| Mode | Shapes | Use |
|---|---|---|
| **Faceted** (default) | The shared tessellation as triangles | Exact match with the real-time silhouettes; one intersection routine |
| **Smooth** (option) | Analytic surfaces | Round silhouettes without facets, smaller memory |

The smooth mode covers each shape analytically, except the Bezier mesh, which stays triangles:

| Shape | Smooth intersection |
|---|---|
| Sphere | Quadratic |
| Truncated cone | Quadric without caps, like `gluCylinder` |
| Disk, ring or sector | Plane test plus radius and angle |
| Box | Slabs |
| Rectangle, triangle | Plane test |
| Torus | Quartic, solved with a robust closed-form or Newton-refined solver |
| Bezier mesh | Triangles of the grid, or a finer adaptive tessellation |

Normals interpolate like the GL normals in faceted mode; in smooth mode they are exact.

### 4. Acceleration

A **two-level BVH**:

- **Bottom level:** one BVH per distinct object-space geometry, built with binned SAH. Shapes with identical parameters share one, so a forest of 400 equal mushrooms costs one BVH. A cache keyed by the shape parameters keeps them across frames.
- **Top level:** a BVH over the instances' world bounds, rebuilt every frame. That takes milliseconds for Lumina's 2,700 elements.

Rays are transformed into object space by the instance's inverse matrix, so there is no need to re-tessellate when something moves.

### 5. Shading

The classic lighting, evaluated per hit:

- **Fixed-function OpenGL 1.1 terms:** ambient, diffuse, Blinn-Phong specular with exponent shininess × 128, spot exponent and cutoff, constant/linear/quadratic attenuation, emission × glow and the global ambient of 0.2. The code is ported from the GLSL in `ShaderProgram.cs` and kept in the same order.
- **Fog:** the same three formulas on the eye-space depth of the primary hit. For secondary rays, the distance along the ray is used.
- **Textures:**
  - texture coordinates as generated by the tessellation;
  - the torus sphere map, computed from the view direction and the normal as the GL shader does;
  - white texels are transparent: the ray continues.

Extensions on top:

- **Shadow rays** to each light whose *Casts shadows* is on.
  - Only elements with *Casts shadows* block.
  - Glass and half-transparent elements tint the shadow instead of blocking it fully.
  - A light with a radius (new key `lightRadius`) is sampled as a sphere, which gives physically soft shadows. Radius 0 gives hard shadows.
- **Reflection:** a mirror ray weighted by *Reflectivity* and Schlick's Fresnel, the same mix as the modern renderer. A new optional `roughness` key spreads it into glossy reflection with several samples.
- **Refraction:** if the refractive index is above 1, a refracted ray follows Snell's law. Entering or leaving is decided by the normal's side. Total internal reflection is handled, and the colour is absorbed by distance with the Beer–Lambert law, using the diffuse colour (new optional `absorption` key).
- **Transparency** (diffuse alpha below 1, without glass) is a straight-through ray blended by alpha. It is correct regardless of drawing order, which OpenGL blending is not.
- **Ambient occlusion** (optional): a few hemisphere rays over a short distance replace the screen-space version. They darken only the ambient term, as in the modern renderer.
- **Depth:** at most `bounces` levels (default 6). Russian roulette below a small weight stops the rest.

**Global illumination** (light bouncing off diffuse surfaces) is out of scope for the first version. It would change the 1998 look, and the per-element *emission* already plays that role. It can come later as a mode with a different name.

### 6. Camera and sampling

- **Antialiasing:** `samples` per pixel (default 16), stratified and jittered, with a fixed seed per pixel and frame so that renders are deterministic.
- **Depth of field:** camera keys `aperture` (0 = pinhole) and `focus` (distance). Rays start on a thin-lens disk.
- **Motion blur:** a `shutter` key (0..1 of a frame). Samples are spread over the shutter time. The scene is evaluated at a few times in the shutter (default 3) and each instance's matrix is interpolated between them, so moving and rotating elements blur without evaluating the animation for every sample.
- **Pixel filter:** box at first; a Gaussian or Blackman-Harris filter later.
- **Output:** linear floating-point film, then the same exposure, tone curve and optional bloom as the modern renderer. A ray-traced frame and a modern frame of the same scene then look alike, and the export settings can share one set of controls.

### 7. Performance

- **Tiles:** 32×32 pixel tiles rendered with `Parallel.For`, on all cores.
- **Single rays first:** `System.Numerics.Vector3` and `float`. Packets of 4–8 coherent primary rays with `Vector256` come later if profiling shows the BVH traversal dominates.
- **Self-intersection:** offset the ray origin along the normal by a scaled epsilon, as in the shadow maps of step 1.
- **Progress and cancelling:** a progress callback per tile and cancelling with a `CancellationToken`, so export can be stopped with Esc as today.

**Expected cost** is a goal to measure against, not a promise: 1280×720 at 16 samples is about 15 million camera rays plus secondary rays. On an 8-core laptop that should be well under a minute for the 1998 samples, and a few minutes for Lumina's jungle.

## File format additions

All of these are optional keys in the existing extension block. Absent keys mean the defaults, which reproduce the modern look.

| Record | Key | Default | Meaning |
|---|---|---|---|
| `Raytrace` (new record) | `samples` | 16 | samples per pixel |
| | `bounces` | 6 | maximum ray depth |
| | `geometry` | `faceted` | `faceted` or `smooth` |
| | `ao`, `aoRays`, `aoDistance` | 0, 4, 0.5 | ray-traced ambient occlusion |
| | `shutter`, `blurSteps` | 0, 3 | motion blur |
| `Element` | `roughness` | 0 | glossy reflection and frosted glass |
| | `absorption` | 0 | colour absorption in glass per scene unit |
| `Element` on a light | `lightRadius` | 0 | size of the light for soft shadows |
| `Element` on a camera | `aperture`, `focus` | 0, 10 | depth of field |

The element keys are interpolated between key states like the 2.1 values. A focus pull, for example, animates by itself.

## Integration

- **Command line:** `--raytrace` next to `--modern` and `--classic`, for `--snapshot` and `--frames`. Further options are `--samples N` and `--bounces N`, which override the file.

  ```powershell
  Overbryce.exe Scenes\Lumina\Lumina.ani --frames frames --size 1280x720 --raytrace --samples 32
  ```

- **Editor:**
  - *File › Rendering settings* gets a choice of renderer for export: classic, modern or ray traced.
  - *Animation › Ray tracing settings* edits the `Raytrace` record, like the modern settings dialog.
  - The property grid gains the new keys in the *9 Modern rendering* category. *Light radius* goes under *8 Light* and the camera keys under the camera.
- **Preview:** *View › Ray trace this frame* (F9) renders the current frame progressively into the view, first at 1 sample, then refining, until any edit or Esc.
- **Blender export** (optional, independent): map the 2.1 values onto the Principled BSDF in `overbryce_blender.py`:
  - reflectivity → metallic or specular;
  - IOR → transmission and IOR;
  - glow → emission strength;
  - casts-shadows → the object's shadow visibility.

## Validation

1. **Unit tests** for every intersection routine: hits, misses, grazing rays, inside starts, and the torus's four roots. They also check that the BVH traversal finds the same nearest hit as a brute-force loop over random scenes.
2. **Parity with the classic renderer.** With reflections, refraction and shadows off and 1 centred sample per pixel, a ray-traced frame of every 1998 sample and of Lumina frames must match the classic OpenGL frame within a tolerance.
   - The test uses the existing `--snapshot --classic` output, with a mean difference below about 1 % and outliers only on silhouette edges.
   - This pins down the lighting, fog, textures, transforms and camera.
3. **Depth parity.** In faceted mode, the primary-hit depth must match the OpenGL depth buffer. This checks that the shared tessellation is used.
4. **Known images.** A mirror box seen in a mirror, a glass sphere over a checkerboard (inverted image, total internal reflection ring) and a soft shadow width versus light radius are checked against computed values.
5. **Determinism.** The same frame rendered twice is byte-identical.
6. **Compatibility.**
   - Files with the new keys open in 2.1 and keep them; a test covers the unknown-key round trip already.
   - Files without them render as before.

## Milestones

| # | Milestone | Done when |
|---|---|---|
| 1 | Move the tessellation into Core; the OpenGL renderer uses it | classic renders unchanged (pixel diff against 2.1) |
| 2 | `Overbryce.Raytrace`: scene compiler, triangles, two-level BVH, primary rays, classic shading, fog, textures | parity tests pass on all 1998 samples |
| 3 | Shadow rays, reflection, refraction, transparency, antialiasing | known-image tests pass; Lumina frames render |
| 4 | Command line `--raytrace`, export settings, progress and cancel | the frames of a whole animation export |
| 5 | Extension keys: light radius, roughness, absorption, camera DOF, shutter; property grid and settings dialog | round-trip tests; old files unchanged |
| 6 | Smooth analytic geometry, ray-traced AO, progressive preview (F9) | visual check; parity holds in faceted mode |
| 7 | Performance: packets, SIMD, BVH tuning | Lumina's jungle under a target time per frame |

Later, outside this plan:

- **GPU tracing** with OpenGL 4.3 compute shaders where the card supports them, using the same BVH layout (no new dependency; the classic and modern renderers stay on 3.3);
- **path-traced global illumination** as a fourth renderer;
- **a denoiser.**

## Risks and open questions

- **Exact parity of the classic look** depends on details of the GLSL emulation, such as two-sided lighting, the order of clamping and sphere-map orientation. The port is mechanical, and the parity test catches mistakes; budget time for it in milestone 2.
- **Memory for Lumina.** The jungle has thousands of instances, but most share a few hundred distinct shapes, so BVH sharing keeps it small. The largest Bezier meshes may need adaptive tessellation.
- **Alpha-tested foliage** needs a texture lookup at every candidate hit. That is slower than opaque triangles, but correct.
- **Motion blur with permanent rotation:** interpolating matrices between shutter samples assumes small rotations per frame. Fast spins (the 1998 `Disky.ani`) need more `blurSteps`.
