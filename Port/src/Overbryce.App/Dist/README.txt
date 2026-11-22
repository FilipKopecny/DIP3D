Overbryce Animator 2.4
======================

A key-frame 3D animation editor: a port of Overbryce Animator 1.0 (1998) to .NET 10 and OpenGL 3.3.
This build is self-contained: no .NET installation is needed.

New in 2.4: import from Blender (File > Import > Blender scene, needs Blender 4.2 or newer). Meshes,
curves and text become polygon meshes (stored in a .meshes file next to the .ani); materials,
textures, lights, cameras, the world and the motion are converted, and a report lists what could
only be approximated. A scene exported to Blender by Overbryce comes back as its own elements.

New in 2.3: the editor window has the Windows 11 look, with light and dark themes (View > Theme:
follow Windows, light or dark) and new icons in place of the 1998 bitmap buttons.

New in 2.2: a ray tracer for exported frames, snapshots and a preview (F9), with exact reflections,
refracting glass, soft shadows, depth of field and motion blur. It runs on the processor, on all
cores.

New in 2.1: a modern renderer next to the classic 1998 look, with shadows, reflections, glass,
glow (bloom) and ambient occlusion.

Files stay readable by the 1998 program and by 2.0 to 2.3 (they see a polygon mesh as an empty
group).

Requirements: Windows 10 or 11 (x64) and a graphics card with OpenGL 3.3.


Start
-----

Overbryce.exe      the editor (File > Open starts in the Scenes folder)
Lumina.cmd         opens the Lumina animation; press F5 to play it

Scenes\            the seven 1998 sample animations with their textures
Scenes\Lumina\     Lumina, an animation made for the port, and its textures: 121 seconds in all,
                   a 16-second approach through space, 71 seconds on the moon and its
                   shore, then 34 seconds under the sea


Controls
--------

Click an object in the 3D view      select it (Alt+click: the enclosing group)
Ctrl+1 .. Ctrl+4                    camera, front, side or top view
Ctrl+M                              classic or modern rendering (Animation > Modern rendering
                                    settings: shadows, reflections, glow, ambient occlusion)
F9                                  ray trace the view into its own window (Animation > Ray
                                    tracing settings: samples, bounces, motion blur ...)
Left-drag / mouse wheel             orbit / zoom the front, side and top views
F5                                  play or stop
Drag the timeline                   preview any moment of the animation
PgUp / PgDn                         previous / next key state (PgDn on the last one adds a new state)
Ctrl+E                              export frames (File > Rendering settings: size, format,
                                    renderer)
File > Import > Blender scene       import a .blend file (Esc cancels)
File > Export to Blender            save the animation as a .blend file (Esc cancels)
View > Theme                        light or dark window, or follow Windows
F1                                  all controls


Rendering from the command line
-------------------------------

One picture:

  Overbryce.exe Scenes\Lumina\Lumina.ani --snapshot lumina.png --time 54 --size 1280x720

Every frame of the animation, as PNG files:

  Overbryce.exe Scenes\Lumina\Lumina.ani --frames frames --size 1280x720

Other options: --from and --to (seconds), --view camera|front|side|top, --wire,
--modern or --classic (the renderer; without either, the animation's own setting).

The modern look of Lumina:

  Overbryce.exe Scenes\Lumina\Lumina.ani --snapshot lumina.png --time 54 --size 1280x720 --modern

Ray traced, with 32 samples per pixel (--raytrace also works with --frames; --samples N and
--bounces N override the animation's settings; the time of each picture is printed):

  Overbryce.exe Scenes\Lumina\Lumina.ani --snapshot lumina.png --time 54 --size 1280x720 --raytrace --samples 32

A Blender scene (needs Blender installed; writes LuminaBlender\Lumina.blend):

  Overbryce.exe Scenes\Lumina\Lumina.ani --blender LuminaBlender

With --no-blend it writes only the data and overbryce_blender.py, the script that builds the scene.

A Blender scene back into an animation (writes the report next to it as scene.import.txt):

  Overbryce.exe LuminaBlender\Lumina.blend --import-blender LuminaBack\Lumina.ani

Other options: --static (only the first frame), --axis auto|yup|zup, --tolerance 0.001 (how far
the motion between key states may stray, as a part of the scene's size; 0 keys every frame).


Graphics card
-------------

On laptops with two graphics cards Windows may run Overbryce on the integrated one. To use the
faster card, add Overbryce.exe in Settings > System > Display > Graphics and choose
"High performance".
