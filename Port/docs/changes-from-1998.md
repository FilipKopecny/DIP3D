# Changes from the 1998 program

The port keeps the original data model, file format and interpolation method. The changes below are bug fixes, or places where the port does what the thesis describes rather than what the 1998 code did. Each one is marked `FIX:` in the source code.

## Fixes that change how existing animations play

| # | Original behaviour | Port | Effect on the sample files |
|---|---|---|---|
| 1 | **"Final" only halved the velocity** at a key state. The key state was duplicated as the outer spline point, although the thesis says the velocity becomes zero | The velocity is zero: the element comes to rest at that key state and starts again from rest | All samples. Elements flagged in a key state now pause there instead of passing through at half speed with a kink |
| 2 | "Final" was **ignored for elements inside groups** | Honoured at every level | `ha1`, `NewAni`, `Scena`, `Meshe`, `Gule`, `Mesto`: flagged group children now stop at their key states |
| 3 | The **start and end of the animation** also used duplication, with half velocity | Everything starts and ends at rest | Smoother start and end in all samples |
| 4 | When an element was missing in the state after next, the original used its **value from the first state** as the 4th control point, bending the motion back towards where it came from | Treated like the end of motion | Only animations where elements disappear |
| 5 | **Permanent rotation and orbit restarted at every key state** and jumped unless an interval was a whole number of turns | Accumulated over the whole animation: continuous | `Disky.ani` (`Disk5`: 45° jump every 5 s), `Gule.ani` |
| 6 | Permanent motion was driven by the interpolated scene time, which is not linear in the first and last interval | Real frame time | Slightly different spin speed at the start and end |
| 7 | A **camera inside a rotated and moved group** was positioned with the parent transforms undone in the wrong order | The view is the exact inverse of the camera's world transform; cameras placed directly in a state are unchanged | `NewAni.ani`: from t = 19 s the camera sits up to 0.76 units from where it used to be; the viewing direction is the same |
| 8 | The camera's view matrix was loaded into `GL_PROJECTION`, so **fog** was measured from the world origin and **specular highlights** were computed wrongly | True eye-space lighting and fog | Fog and highlights in every scene with a moving camera |
| 9 | Diffuse colour was **interpolated twice** per frame (copy-paste slip) | Interpolated once | Colour animations |
| 10 | Disk radii used a **different end-point formula** from all other elements | Common formula | Disks in unevenly spaced key states |
| 11 | Orbit used the constant 0.01744 for π/180 | Exact value | Negligible |
| 12 | Lights inside groups were not positioned in the group's frame | Hierarchical frame | Lights in groups |

## Fixes that do not change existing files

- **Line draw style:** box, rectangle and triangle compared `GL_LINE` with `GLU_LINE` and never drew as lines. Now they do.
- **"Final" default:** it is now off for **new** elements. The original switched it on for every new element, so new motion always kinked at every key state. Files keep their stored value.
- **Mesh emission:** the mesh uses its own emission colour.
- **Light and camera icons:**
  - The light icon points along the spot direction; the original rotated it by the direction vector as if it were angles.
  - The camera icon opens along the viewing direction and uses the camera's rotation order.
- **No active camera:** camera view falls back to the front view and shows a status bar message instead of an undefined view.
- **Frame timing:** frames are spaced evenly over the whole animation instead of being truncated per interval.
- **Frame export:** frames are rendered off-screen at the chosen resolution in 24-bit colour. The original copied them from the window, so overlapping windows could corrupt the frames, and 16-bit desktops needed the separate *Converter* program.

## New in the port

- **Click to select in the 3D view.** The original selected only in the Object List.
  - Click an object to select it; transparent texels can be clicked through.
  - Alt+click selects the enclosing group; repeat to go further up.
  - Click the background to select the key state.
  - In the wire-frame view lights and cameras can be clicked too.
- **Property grid** instead of one modal dialog per element type.
- **Timeline** with live preview and real-time playback of the interpolated animation.
- **Editor cameras** can be orbited with the mouse and zoomed with the wheel.
- **Rendering:** OpenGL 3.3 core with multisampling. Export as BMP or PNG at up to 1920×1080.
- **Modern rendering (2.1):** an optional second renderer with HDR, bloom, shadows, ambient occlusion, reflections, glass and glow. The classic renderer still gives the 1998 look and stays the default. See [modern-rendering.md](modern-rendering.md).
- **File format extension (2.1):** the modern settings and four new element properties are written after `KoniecAnimacie`, where the 1998 program stops reading, so the files still open there. Files without them are written exactly as before. See [ani-format.md](ani-format.md#the-overbryce-21-extension-block).
- **Ray tracing (2.2):** an offline CPU ray tracer for exported frames, snapshots and a preview window (F9), with exact reflections, refraction, soft shadows, depth of field and motion blur. Its settings and five new properties (roughness, absorption, light radius, aperture and focus) go into the same extension block. With its effects off, it renders the classic look. See [raytracing.md](raytracing.md).
- **Command line:** `--snapshot` renders a single picture and `--frames` renders the whole animation, both without opening the editor. `--modern` and `--classic` choose the renderer, and `--raytrace` (with `--samples` and `--bounces`) traces the picture instead.
- **A new sample:** [Lumina](../samples/Lumina/README.md), generated by `tools/Lumina` through the model library.

## Kept on purpose

- **Group transforms:** a group rotates before it translates, unlike all other elements.
- **Light angles:** for lights, alpha, beta and gamma are the spot direction vector.
- **Element matching:** by name among the direct children of the same group.
- **Angle interpolation:** angles are interpolated as plain numbers, so 350° → 10° turns the long way round. This is what makes spins by more than 360° possible.
- **Orbit centre:** orbits are always about the parent's origin. The stored orbit centre was never used.
- **Limits:** at most 8 lights and 8 cameras per key state.
- **Textures:** stretched to 128×128, and pure white is transparent when alpha blending is on.
- **File format:** unchanged. Files written by the port can be opened by the 1998 program.
