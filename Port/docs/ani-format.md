# The `.ani` file format

Overbryce stores a whole animation in a single `.ani` text file. The format comes from the 1998 program (`CAnimacia::SaveYou` / `LoadYou` and the `SaveYou` / `ReadYou` methods of each element class). The 1998 thesis did not document it, so this page is the reference.

The port reads every file written by the original program and writes files that the original program can read.

## Lexical rules

- **Encoding:** Windows-1250 (Central European). The sample files use it for the animation name, for example `Animácia`.
- **Tokens:** a flat stream separated by whitespace. The original writes everything on one line with a single space after each token. Line breaks are not significant.
- **Numbers:** C++ `ostream` default formatting, which is `%g` with 6 significant digits and `.` as the decimal point, for example `0.0941176` or `1e-05`.
- **Booleans:** `0` or `1`.
- **Names:** one token, so no whitespace is allowed. They are at most 20 characters; the original stored them in `char[21]`.
- **Element matching:** elements are matched between key states by name, ignoring case (`stricmp`).
- **Tags:** keywords are Slovak: *Animacia* = animation, *Scena* = scene / key state, *Grupa* = group, *Koniec_…* = end of ….

## Grammar

```text
file        := "Animacia" animName fps  scene+  "KoniecAnimacie"

scene       := "Scena" common  aniTime fogMode fogStart fogEnd fogDensity fogR fogG fogB fogA
               element*  "Koniec_sceny"

element     := "Svetlo"        common  lightBody      // light
             | "Kamera"        common  cameraBody     // camera
             | "Gula"          common  sphereBody     // sphere
             | "Disk"          common  diskBody       // disk, ring or sector
             | "Zrezany_kuzel" common  coneBody       // truncated cone
             | "Kvader"        common  boxBody        // box
             | "Obdlznik"      common  rectBody       // rectangle
             | "Trojuholnik"   common  triangleBody   // triangle
             | "Torus"         common  torusBody      // torus
             | "Mesh"          common  meshBody       // Bezier surface
             | "Grupa"         common  element*  "Koniec_grupy"
```

`fps` is a float. A scene starts with the same `common` block as every element, because `CScena` derives from `CPrvok`.

### `common`: shared by all elements (`CPrvok::SaveYouPrvok`)

| # | Field | Type | Meaning |
|---|---|---|---|
| 1 | name | token | element name, max. 20 characters |
| 2–4 | x y z | float | position in the parent's frame |
| 5–7 | alfa beta gama | float | rotation about X, Y, Z in degrees. **For lights: the spot direction vector** |
| 8–11 | specular R G B A | float | material, or the light's specular colour |
| 12–15 | diffuse R G B A | float | material, or the light's diffuse colour. Its alpha is the surface opacity |
| 16–19 | ambient R G B A | float | material, or the light's ambient colour |
| 20–23 | emission R G B A | float | material emission (not used by lights) |
| 24 | shininess | float | 0..1; OpenGL exponent = shininess × 128 |
| 25 | zaciatokPohybu | 0/1 | "Final": stop at this key state (see [animation-model.md](animation-model.md)) |
| 26 | koniecPohybu | 0/1 | stored but never used, by the original or the port |
| 27–29 | rotation angle X Y Z | float | permanent rotation: degrees per period |
| 30 | rotation period | float | seconds |
| 31–33 | orbit angle X Y Z | float | permanent orbit about the parent's origin: degrees per period |
| 34–36 | orbit centre X Y Z | float | stored but never used |
| 37 | orbit period | float | seconds |
| 38 | rotation permanent | 0/1 | permanent rotation enabled |
| 39 | orbit permanent | 0/1 | permanent orbit enabled |
| 40–41 | texture width height | int | `128 128` when textured; `0 0` means no texture |
| 42 | texture name | token | file name looked up next to the `.ani` file, or `NoTex` |
| 43 | draw style | int | `100012` = `GLU_FILL` (filled), `100011` = `GLU_LINE` (wire) |

A texture is used only when width × height ≠ 0 and the name is not `NoTex`. The original NoTex button set the size to `0 0` but kept the old name. Such files, for example `Meshe.ani`, therefore contain a texture name that must be ignored.

### Type-specific bodies

| Type | Body |
|---|---|
| `Svetlo` | `enabled spotExponent spotCutoff constantAtt linearAtt quadraticAtt`. A cutoff over 90 is treated as 180, an omnidirectional point light |
| `Kamera` | `fieldOfView active` (field of view in degrees, vertical) |
| `Gula` | `radius slices stacks` |
| `Zrezany_kuzel` | `baseRadius topRadius height slices stacks`, along +Z from z = 0 |
| `Disk` | `innerRadius outerRadius sweepAngle slices loops`. A sweep of 360 is a full disk |
| `Kvader` | `sizeX sizeY sizeZ`, centred on the position |
| `Obdlznik` | `sizeX sizeY`, in the local XY plane, centred |
| `Trojuholnik` | `x1 y1 x2 y2`. The vertices are (0,0), (x1,y1) and (x2,y2) in the local XY plane |
| `Torus` | `tubeRadius ringRadius tubeSides ringSegments`, around the local Z axis |
| `Mesh` | `countX countY` then `countX × countY × 3` floats (control points, row by row: `[y][x][xyz]`), then `gridU gridV` |

### Scene (key state) body

| Field | Meaning |
|---|---|
| aniTime | time of the key state in seconds. States are stored in time order |
| fogMode | `0` none, `1` linear, `2` exponential, `3` exponential² |
| fogStart fogEnd | linear fog range: distance from the camera |
| fogDensity | density for the exponential fog modes |
| fogR G B A | fog colour |

## Example

This is the beginning of `DIP/SCENES/Gule.ani`, wrapped for readability:

```text
Animacia Animácia 25
Scena Scena 0 0 0 0 0 0  1 1 1 1  0.8 0.8 0.8 1  0.6 0.6 0.6 1  0.1 0.1 0.2 0.6  0.8 1 1
      0 0 0 0  0 0 0 0 0 0 0  0 0  0 0 NoTex 100012
      50  0 0 1 1  0.2 0.2 0.2 0.2
  Svetlo NoveSvetlo 0 0 2 0 0 0 ... NoTex 100012  1 1 180 1 0 0
  Kamera NovaKamera 0 0 23 0 0 0 ... NoTex 100012  35 1
  Gula Slnko 0 0 0 0 0 0 ... NoTex 100012  2.4 62 3
  Grupa NovaGrupa 9 0 0 0 0 0 ... 
     ... Koniec_grupy
Koniec_sceny
...
KoniecAnimacie
```

## How the port reads and writes the format

- **Reading** is tolerant in the same way as `ifstream >>`. Anything after `KoniecAnimacie` is ignored, except the extension block below, and `inf` / `nan` are accepted.
- **Writing** uses the same token order, so the original program can read the files. The port writes:
  - numbers in `%g` format (6 significant digits)
  - textures as `128 128 <file name>`; spaces in a file name become `_`
  - draw style `100012` or `100011`
- **Saving** copies texture files picked from other folders next to the `.ani` file, because the format stores only the file name.
- **Fixes:** the port's bug fixes ([changes-from-1998.md](changes-from-1998.md)) did not change the format. A few behaviour changes alter how some existing files play back, as described there.

## The Overbryce 2.1 extension block

Overbryce 2.1 adds values that the 1998 format has no room for: the [modern renderer](modern-rendering.md) settings, and per-element reflectivity, refractive index, glow and "casts shadows". It stores them **after** `KoniecAnimacie`, in a block of its own.

```text
...
KoniecAnimacie
Overbryce2 1
Modern enabled=1 bloomStrength=0.6 shadowMapSize=2048 ;
Element 0 2 glow=2.5 shadows=0 ;
Element 0 3.1 reflectivity=0.35 ;
Element 4 5.0.2 ior=1.45 ;
EndOverbryce2
```

### Grammar

```text
block   := "Overbryce2" version  record*  "EndOverbryce2"
record  := kind  token*  ";"
Modern  := "Modern"  key=value*
Element := "Element"  state  path  key=value*
```

- `version` is the writer's version, currently `1`. Later versions keep this record syntax, so a reader does not need to check the version.
- `state` is the 0-based index of the key state.
- `path` gives the child indices from the key state down to the element, joined by `.`. For example, `3.1` is the second child of the fourth element.
- Only values that differ from the defaults are written. A file with no modern data has no block at all, so it is written byte for byte as before.

### Keys

| Record | Key | Type | Default | Meaning |
|---|---|---|---|---|
| `Modern` | `enabled` | 0/1 | 0 | open the animation in modern mode |
| | `exposure` | float | 1 | brightness before tone mapping |
| | `bloom`, `bloomStrength`, `bloomThreshold` | 0/1, float, float | 1, 0.5, 0.9 | glow of bright parts |
| | `shadows`, `shadowSoftness`, `shadowLights`, `shadowMapSize` | 0/1, float, int, int | 1, 1, 4, 1024 | shadow maps |
| | `ao`, `aoRadius`, `aoStrength` | 0/1, float, float | 1, 0.5, 1 | ambient occlusion |
| | `reflections`, `probes`, `probeSize` | 0/1, int, int | 1, 4, 256 | reflection probes |
| `Element` | `reflectivity` | float 0..1 | 0 | mirror-like reflection |
| | `ior` | float ≥ 1 | 1 | refractive index; above 1 the element is glass |
| | `glow` | float ≥ 0 | 1 | multiplier of the emission colour |
| | `shadows` | 0/1 | 1 | the element casts shadows; on a light, the light casts shadows |

### Compatibility

- **Backward compatibility.** The 1998 program and Overbryce 2.0 stop reading at `KoniecAnimacie`, so they open a 2.1 file and simply do not see the block. Everything before the block is the original format; a test checks this.
- **Forward compatibility.** Overbryce 2.1 opens files from later versions:
  - unknown records, and unknown keys in `Modern` and `Element` records, are kept and written back unchanged;
  - values it cannot parse are ignored, so the default stays;
  - out-of-range values are clamped;
  - a damaged or truncated block keeps what was read before the damage.
- **Stale records.** If an older version edits a 2.1 file, it drops the block when it saves. If the block survives but the element it names no longer exists, the record is dropped.
