# Animation model

This page describes how Overbryce computes the frames between key states. The model comes from the 1998 diploma thesis (chapter "Návrh interpolačnej metódy") and `CAnimacia::Render`. Where the port behaves differently, the section says so; [changes-from-1998.md](changes-from-1998.md) lists all the differences.

## Key states and element matching

An animation is a time-ordered list of **key states** (*scény*). Each key state is a complete scene: a tree of elements and groups, plus a time stamp and fog settings.

A frame at time *t* in the interval between key states *K* and *K+1* is computed in four steps:

1. Copy key state *K*.
2. For every element of the copy, find its **counterparts** in key states *K−1*, *K+1* and *K+2*. A counterpart is an element with the same name (case-insensitive) and the same type in the same group. Groups are matched the same way, level by level.
3. Interpolate every numeric attribute through the four values: position, angles, colours, shape sizes, mesh control points, light and camera parameters.
4. Add the permanent rotation and orbit.

How the frame handles missing counterparts and changes in the set of elements:

- **Element missing in *K+1*:** it does not move during that interval.
- **Element missing in *K−1* or *K+2*:** on that side the element is treated as starting or ending its motion (see the next section).
- **Element new in *K+1*:** it appears when the frame time reaches *K+1*, because frames are built from a copy of *K*.

## Interpolation: non-uniform Catmull-Rom

For one attribute, let *p₁ … p₄* be its values in key states *K−1 … K+2* at times *t₁ … t₄*. Let *u ∈ [0, 1]* be the position within the interval *[t₂, t₃]*. The uniform Catmull-Rom basis is

```text
c1 = −½u³ + u² − ½u      c2 = 1½u³ − 2½u² + 1
c3 = −1½u³ + 2u² + ½u    c4 = ½u³ − ½u²
x(u) = h1·c1 + p2·c2 + p3·c3 + h4·c4
```

The thesis' contribution is how *h1* and *h4* are chosen when key states are **not evenly spaced in time**:

```text
h1 = p2 − (p2 − p1) · (t3 − t2)/(t2 − t1)
h4 = p3 + (p4 − p3) · (t3 − t2)/(t4 − t3)
```

(The thesis text prints the first ratio inverted; the code, and this formula, are correct.)

### Why it works

The Catmull-Rom velocity at *u = 0* is *(p3 − h1)/2*. Divided by the interval length *Δ₂₃* to convert to real time, this gives

```text
v(t2) = ½ · [ (p3 − p2)/Δ23 + (p2 − p1)/Δ12 ]
```

This is the average of the speeds over the two intervals that meet at *t₂*. The interval before computes exactly the same value at its end. So the motion is smooth (C¹) **in real time**, not only along the curve, however unevenly the key states are spaced.

## Stopping at a key state ("Final")

Each element has a flag *zaciatokPohybu*: the "Final" check box of the original, called **Stop at this state** in the port. When it is set in key state *K*, the element's velocity at *K* is zero. The element decelerates into *K* and accelerates out of it, and its path may turn sharply there.

The port implements this by replacing the outer control point:

- at the start of an interval: `h1 = p3`
- at the end of an interval: `h4 = p2`

Both give zero velocity. With both ends stopped, the curve becomes the smoothstep `p2 + (p3 − p2)(3u² − 2u³)`.

The same rule applies:

- at the first and the last key state of the animation, so everything starts and ends at rest;
- where an element has no counterpart in the neighbouring state.

> **Port vs. original:** the original implemented the flag by duplicating the key state (*p₁ = p₂*). That only halves the velocity, although the thesis states that the velocity is zero. The original also ignored the flag for elements inside groups and set it for every new element. The port follows what the thesis describes. New elements have the flag off.

## Permanent rotation and orbit

An element can rotate or orbit continuously, independently of its key states:

- **Rotation:** adds *angle × elapsed / period* to alpha, beta and gamma.
- **Orbit:** rotates the element's position about the origin of its parent (scene or group) by the same kind of angle. It also turns the element, so the element keeps facing the centre.

The motion of each interval uses the settings from the key state where the interval starts.

> **Port vs. original:** the original measured the elapsed time from the start of the current interval. The rotation therefore restarted at every key state and jumped, unless each interval happened to be a whole number of turns. In `Disky.ani`, `Disk5` turns 180° every 0.8 s over 5 s intervals and jumped by 45° at every key state. The port adds the rotation accumulated in earlier intervals, so permanent motion is continuous over the whole animation. It also uses the real frame time instead of the interpolated scene time.

## Hierarchy and transforms

Each element type composes its local transform in its own order. *T* is the translation by the position; *Rx, Ry, Rz* are the rotations by alpha, beta, gamma. The products are written in OpenGL call order, so the leftmost operation is the outermost.

| Element | Local transform | Note |
|---|---|---|
| shapes, lights, scenes | T · Rx · Ry · Rz | translate, then rotate about the element's own axes |
| groups | Rx · Ry · Rz · T | rotate first: a group's position is given in its rotated frame (kept from the original) |
| cameras | T · Ry · Rx · Rz | yaw, pitch, roll; the camera looks along its local −Z |

An element's world transform is its local transform followed by the transforms of all its parent groups up to the key state. Motion is therefore hierarchical: moving a group moves everything in it, and a camera can be placed in a group, for example in a character's head. The view is the exact inverse of the active camera's world transform.

> **Port vs. original:** `CKamera::RenderDirect` undid the parent groups in the wrong order. A camera inside a rotated *and* moved group was therefore placed slightly wrong. For cameras placed directly in a key state nothing changes.

### Lights

For lights, alpha, beta and gamma are the **spot direction vector**, not angles. This is kept from the original. The light's position and direction are given in its parent's frame.

- A spot cutoff up to 90° gives a spotlight; anything above 90 gives an omnidirectional point light.
- At most 8 lights per key state are used (OpenGL's GL_LIGHT0..7).

### Cameras

- One camera per key state is **active**, and the state is rendered through it.
- The field of view is interpolated, which produces zoom.
- Changing the active camera between key states produces a cut.

## Frame times

Frames are computed at *t = start + n / fps* for *n = 0, 1, …* up to the time of the last key state. The original counted the frames separately for each interval and truncated each count, which made long animations drift. The same `Evaluate(t)` drives the timeline preview, playback and the frame export.
