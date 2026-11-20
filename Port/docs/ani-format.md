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

- **Reading** is tolerant in the same way as `ifstream >>`. Anything after `KoniecAnimacie` is ignored, and `inf` / `nan` are accepted.
- **Writing** uses the same token order, so the original program can read the files. The port writes:
  - numbers in `%g` format (6 significant digits)
  - textures as `128 128 <file name>`; spaces in a file name become `_`
  - draw style `100012` or `100011`
- **Saving** copies texture files picked from other folders next to the `.ani` file, because the format stores only the file name.
- **Fixes:** the port's bug fixes ([changes-from-1998.md](changes-from-1998.md)) did not change the format. A few behaviour changes alter how some existing files play back, as described there.
