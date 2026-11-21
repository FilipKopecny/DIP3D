# How the port was made

Overbryce Animator 1.0 was written in 1998 with Borland C++ Builder 3 and OpenGL 1.1. Version 2.0 brings it to current Windows. The port was made in 2026 with the help of Claude, an AI coding assistant from Anthropic, working from the original source, the thesis and the sample animations. The 1998 files in `DIP/` were not changed.

## What was done

1. **Analysis.** The 1998 C++ source (`DIP/SOURCE`), the sample animations (`DIP/SCENES`) and the thesis text were studied. This covered the scene model, the `.ani` format, the Catmull-Rom interpolation and its fix for unevenly spaced key states, and the rendering.
2. **The model library** (`src/Overbryce.Core`). It holds the original element types, groups, key states and animation, with no user interface. It reads and writes the `.ani` format unchanged and interpolates the same way the thesis describes. The [animation model](animation-model.md) and the [file format](ani-format.md) are documented separately.
3. **The editor** (`src/Overbryce.App`): WinForms and OpenGL 3.3.
   - **Rendering:** a shader reproduces the OpenGL 1.1 fixed-function pipeline, with up to 8 lights, fog, transparent textures and the torus's sphere mapping. The shapes are built in code the way GLU built them.
   - **Editing:** the original tool-bar bitmaps, the element tree and a property grid.
   - **Playback and output:** a timeline with live preview and playback, and off-screen frame export.
4. **Fixes.** The port fixes the bugs found in the 1998 program: the camera matrix in the projection matrix, "Final" ignored inside groups, permanent motion restarting at every key state, and others. Each one is described, with its effect on the samples, in [changes-from-1998.md](changes-from-1998.md), and marked `FIX:` in the code.
5. **New features:**
   - click selection in the 3D view (Alt+click selects the group);
   - orbit and zoom in the editor views;
   - command-line rendering of one picture (`--snapshot`) or of every frame (`--frames`);
   - [export to Blender](blender-export.md) (`--blender`), with the motion carried over exactly as Bezier curves;
   - in version 2.1, a [modern renderer](modern-rendering.md) next to the classic one: HDR with bloom, soft shadows, ambient occlusion, reflections, refracting glass and glow. Its settings and four new element properties are stored in an [extension block](ani-format.md#the-overbryce-21-extension-block) after the end of the original format, so the 1998 program and version 2.0 still read the files. An offline ray tracer is [planned](raytracing-plan.md).
6. **Tests** (`tests/Overbryce.Core.Tests`): 59 xUnit tests.
   - Every original sample and Lumina load, and survive a save and reload unchanged.
   - The interpolation, stopping, permanent motion and camera maths behave as documented.
   - The 2.1 extension block round-trips, keeps what a newer version wrote, ignores damaged data, and leaves the original part of the file readable by older versions.
7. **Evaluation of the thesis**, judged by the standards of its time: [thesis-evaluation.md](thesis-evaluation.md).
8. **A new sample animation, [Lumina](../samples/Lumina/README.md).** It runs 121 seconds: a 16-second opening in a planetary system, a story on a small moon covered in jungle, a journey to its southern ocean and a dive into an underwater canyon. It is written entirely in Overbryce's own format by a generator, [`tools/Lumina`](../tools/Lumina), through the model library. It shows nested orbits, hierarchical characters up to eleven levels deep, Bezier wings, permanent rotation, camera cuts, first-person shots, fog and a spot light carried by a character. Its textures were painted in code or taken from the 1998 samples. The [contact sheet](images/lumina-contact-sheet.png) shows sixteen stills.
9. **A distributable build:** a self-contained Windows x64 folder with the samples and Lumina, described under "Distributable build" in [the port's README](../README.md).

## Not in the repository

These are not in the repository:

- **Not published:** the thesis text and its figures (`TEXT/`), and the original design documents and an earlier year project (`DIP/DOCS/`).
- **Can be rebuilt from the repository:** the rendered Lumina video and the zipped distributable, both in `Port/out/`.
