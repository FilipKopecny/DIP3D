# Modern rendering

Overbryce 2.1 has two renderers.

- **Classic** is the look of 1998: the OpenGL 1.1 fixed-function pipeline (eight lights, fog, sphere-mapped torus), emulated with shaders. It stays the default, and it is what the 1998 sample files were made for.
- **Modern** draws the same scene with techniques that the 1998 hardware could not run in real time:
  - HDR lighting with bloom;
  - soft shadows;
  - ambient occlusion;
  - reflections;
  - refracting glass;
  - glowing materials.

Both run on OpenGL 3.3, so no new hardware requirement was added.

| Classic | Modern |
|---|---|
| ![Lumina at 46 s, classic](images/lumina-46-classic.png) | ![Lumina at 46 s, modern](images/lumina-46-modern.png) |
| ![Lumina at 54 s, classic](images/lumina-54-classic.png) | ![Lumina at 54 s, modern](images/lumina-54-modern.png) |

## Switching

- **In the editor:** *View › Modern rendering* (Ctrl+M) switches the view. The status bar then starts with "Modern". Frame export (Ctrl+E) uses the renderer shown in the view.
- **Per animation:** *Animation › Modern rendering settings* edits the settings below, with a live preview; Cancel restores the previous values. *Open in modern mode* makes the animation open in modern mode.
- **On the command line:** `--modern` or `--classic` overrides the animation's setting for `--snapshot` and `--frames`.

```powershell
Overbryce.exe Scenes\Lumina\Lumina.ani --snapshot lumina.png --time 46 --size 1280x720 --modern
```

## Element properties

Every shape has a new category in the property grid, *9 Modern rendering*. The classic renderer ignores these properties. Like every other numeric property, they are interpolated between key states.

| Property | Range | Default | Effect |
|---|---|---|---|
| Reflectivity | 0..1 | 0 | Mirror reflection of the surroundings, mixed with the material's own colour. It is stronger at grazing angles (Fresnel). |
| Refractive index | ≥ 1 | 1 | Above 1 the element is glass. It bends what is behind it (1.33 water, 1.45–1.5 glass, 2.4 diamond) and reflects more at grazing angles. Its opacity (diffuse alpha) controls how much of its own colour shows. |
| Glow | ≥ 0 | 1 | Multiplies the emission colour. Values above 1 push the colour beyond white, so bloom makes it glow. |
| Casts shadows | yes/no | yes | The element casts shadows. On a **light**, this property is in the *8 Light* category and controls whether the light casts shadows at all. |

**Tips**
- Turn off *Casts shadows* for elements that stand for light sources, such as a lamp bulb or a sun sphere around a point light. Otherwise their surface blocks the light. Elements that enclose a light are skipped automatically.
- Elements with an opacity below 0.5 and glass elements never cast shadows.

## Animation settings

| Group | Setting | Default | Meaning |
|---|---|---|---|
| General | Open in modern mode | off | Show the animation in modern mode when it is opened |
| | Exposure | 1 | Brightness before the highlights are compressed. 1 matches the classic brightness |
| | Classic texture lighting | on | The light on a textured surface is clamped at 1 before the texture is applied, as in the 1998 OpenGL. See below |
| Bloom | On, Strength, Threshold | on, 0.5, 0.9 | Bright parts glow. The threshold is the brightness where the glow starts |
| Shadows | On, Softness, Lights with shadows, Map size | on, 1, 8, 1024 | Soft shadow edge width; up to 8 lights; resolution of a shadow-map face (a distant light's focused map has twice that) |
| Ambient occlusion | On, Radius, Strength | on, 0.5, 1 | Darker creases and contacts. Radius is in scene units |
| Reflections | On, Probes, Probe size | on, 4, 256 | Reflections and glass. Probes is how many elements get their own view of the surroundings per frame |

### Classic texture lighting

OpenGL 1.1 clamped the summed lighting of a vertex to 1 and only then multiplied it by the texture (GL_MODULATE). So a texture could never get brighter than itself, however strong the lights were. The 1998 scenes rely on this: their lights are often set far above 1. In Mesto, the backdrop photo gets about 4 (emission, ambient, two strong lights and their specular). The classic renderer shows the photo as it is; without the clamp, the modern renderer and the ray tracer washed it out to white.

*Classic texture lighting* (on by default, and for every file without the setting) restores the clamp for textured surfaces only:

- untextured surfaces keep the HDR light, so highlights still bloom;
- glow and the emission of light bodies are not affected;
- shadows, occlusion and reflections are applied after the clamp, as before.

Lumina was lit for the modern renderer and turns it off (`classicTextureLight=0`), so bright light can wash its textures out. Turn it off for a new scene that should behave the same way.

## How a frame is drawn

The code is in `src/Overbryce.App/Rendering`: `SceneRenderer.Modern.cs` and `ModernPipeline.cs`. The lighting shader is shared with the classic renderer, in `ShaderProgram.cs`.

1. **Shadow maps.** Up to eight lights with *Casts shadows* are chosen per frame (*Lights with shadows*, 8 by default: all the lights OpenGL had). They are ranked by brightness × attenuation at the camera, so the lights that matter in the shot win.
   - Each chosen light renders a cube map of distances. A spot light skips the cube faces outside its cone.
   - A cube map spreads its texels over all directions, so for a light far from the shot (a sun 50 units away) they are coarse where the camera looks: about 0.1 units with 1024 texels a face, too coarse for the shadow of a character half a unit across. So the part of the scene the camera sees is fitted with a sphere: the view frustum from the near plane to the depth that holds 90 % of the visible lit elements (stars and a sky around the camera do not count). A light more than 1.5 radii from its centre also renders a *focused map*: one perspective view (twice *Map size*, at most 4096) from the light, just wide enough for the sphere. Inside the sphere the shader uses the focused map, and it blends into the cube map over the outer 15 % of the radius.
   - The lighting shader samples it with 12-tap percentage-closer filtering and a normal offset. The edge width grows with *Softness* and with the distance from the light.
2. **Reflection probes.** The elements with reflectivity or glass that are largest on screen each get a cube map of their surroundings, drawn from their centre without themselves (up to *Probes*). Other reflecting elements use the nearest probe. The probes are drawn with lighting and shadows but without reflections, so there is one bounce.
3. **Ambient occlusion.** Normals and depth of the opaque elements are drawn first. Screen-space ambient occlusion (16 samples in a hemisphere, 4×4 noise, blurred) darkens creases and contacts. It is applied fully to ambient light and at 40 % to direct light.
4. **Main pass.** The scene is drawn into a 16-bit floating-point buffer with multisampling: 4 samples in the view, 8 for export, none when antialiasing is off in the rendering settings.
   - The lighting is the classic one, with no clamp at 1, except on textured surfaces while *Classic texture lighting* is on.
   - Then shadows, occlusion, glow, Schlick-Fresnel reflection and the refraction of glass are added.
5. **Post-processing.** The image is resolved and a dual-filter bloom is applied (6 levels, soft threshold). Then exposure and a tone curve are applied. The curve is the identity up to 0.8 and rolls off to 1, so ordinary colours look as in the classic renderer and only the highlights are compressed.

Picking (clicking objects) and wire-frame mode always use the classic path.

### Cost

Measured on the development laptop at 960×540 (Debug build):

| Pass | Extra time per frame |
|---|---|
| Modern without reflection probes | +2–7 ms |
| With four reflection probes (Lumina at 46 s) | up to about +16 ms (46.8 ms instead of 30.8 ms) |

Turning off reflections, or lowering *Probes* or *Probe size*, is the main way to speed it up.

## Limitations

- **Reflections and refraction** come from cube maps, not from ray tracing.
  - A surface reflects what its probe sees from the centre of the element, so flat mirrors and large surfaces show parallax errors.
  - Glass refracts once and shows the surroundings, not other glass behind it.
  - The [ray tracer](raytracing.md) (version 2.2) renders exact reflections and refraction for exported frames, snapshots and a preview.
- **Shadows** come from point lights. Their softness is a filter width, not the size of a real light, so the edge does not widen with the distance from the occluder. Half-transparent elements either cast a full shadow (opacity 0.5 or more) or none, and glass casts none. White-transparent texture parts do let the light through.
  - A light inside a glowing element (a sun, a bulb) takes that element's size; a glowing element with unlit elements inside it (a sky, a halo) does not count. An element smaller than the light casts no shadow from it when its umbra, r·d/(R−r), is shorter than eight of its radii. As a point, the light would make a small element next to a sun throw a huge shadow across a planet, while a character in sunshine keeps its shadow. The [ray tracer](raytracing.md) lightens such shadows by how much of the glowing body they hide instead.
  - Shadows outside the focus sphere of a distant light come from its cube map, so small elements far from the camera may lose their shadows there.
- **Ambient occlusion** sees only what is on screen, so occluders outside the picture do not darken anything.
- **More than eight lights:** OpenGL lights only the first eight lights of a scene anyway; *Lights with shadows* below 8 leaves the less important ones without shadows.

## Compatibility

The settings and the element properties are stored in an extension block after `KoniecAnimacie`, the end tag of the original format. The block is described in [ani-format.md](ani-format.md#the-overbryce-21-extension-block).

- **Older programs:** the 1998 program and Overbryce 2.0 stop reading at that tag. They open 2.1 files as before; they do not keep the block when they save.
- **Files without modern data** are written exactly as before.
- **Newer files:** settings and records from a newer version are kept and written back unchanged. Values that cannot be read fall back to the defaults.
- **The classic renderer** gives the same picture as in 2.0 (checked pixel by pixel against the 2.0 build).
