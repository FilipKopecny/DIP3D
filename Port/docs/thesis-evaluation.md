# Evaluation of the 1998 diploma thesis

*Simulácia virtuálnej reality pomocou OpenGL – animácia scény* (Simulating virtual reality with OpenGL – scene animation), Tomáš Kopečný, FEI STU Bratislava, December 1998.

This review covers both the written thesis and its software. The written thesis was read in full from the original Word document, which is not in this repository. Its claims were checked against the 1998 source in `DIP/SOURCE` and against the behaviour of this port. The thesis is judged by the standards of a Slovak technical university in 1998, not by today's.

## Verdict

**Overall: A (excellent).**

- **The software: A.** One person delivered the whole system after the original three-person team broke up. It is a complete, working key-frame animation editor. Every one of its sample animations still loads and plays 28 years later.
- **The written thesis: B.** By the standards of its time, it is clearly above an average thesis.

## The assignment

The assignment was a system for computer-aided key-frame animation: the user defines key states of a scene over time, and the system generates the frames in between. The thesis delivers:

- a 3D scene editor;
- time-aware Catmull-Rom interpolation of every attribute;
- hierarchical motion through nested groups;
- a pipeline that outputs frame sequences, which can then be encoded to MPEG.

## Strengths

1. **A correct and original fix for unevenly spaced key states.** This is the best idea in the thesis. When neighbouring key states are not evenly spaced in time, the outer control points are shifted by the ratio of the time intervals before interpolating. Worked through, this makes the speed at a key state the average of the speeds on its two intervals, and both segments meeting there get the same speed. Motion therefore stays smooth in real time, not only along the curve's parameter. The thesis does not prove this, but the method is correct.
2. **The method was tested before it was built in.** A separate test program checked the interpolation's properties first. Its figures cover smooth paths, corners, uneven key spacing and slowing down in sharp turns. Most theses of the time did not go this far.
3. **A strong set of features for 1998:**
   - nested groups that carry motion down to their contents;
   - cameras as ordinary scene elements, with several per scene, cuts between them, and zoom by animating the field of view;
   - permanent rotation and orbit motion;
   - Bezier surfaces that change shape over time;
   - spot lights with attenuation, and fog;
   - transparent textures;
   - drag-and-drop editing in the element tree.
4. **A data model that has aged well.** Elements are matched between key states by name, and scenes are saved in a simple text format. The port reads every original file unchanged.
5. **Its own analysis.** Beyond an overview of OpenGL, the thesis compares linear, cosine and Hermite interpolation, and interpolating the path with interpolating the speed. Its own figures illustrate the comparison.
6. **Honesty about limitations, and realistic further work.** The thesis itself lists several shortcomings: the NoTex button does not clear the texture name, each element keeps its own copy of its texture, the orbit centre is unused, and Cut/Copy/Paste are missing. The further-work suggestions are sorted by difficulty.
7. **Complete supporting material.** It comes with a usable user manual with screenshots. A CD held the commented source, the test program, sample animations, rendered films and a converter tool, which was above average for 1998.

## The written thesis in its context

Some features of the text could look weak today but were normal, or better than normal, for a Slovak thesis in 1998:

- **References:** four was usual. The ACM papers on Catmull-Rom and Kochanek-Bartels splines were hard to get, and building on the OpenGL Programming Guide was standard practice.
- **The OpenGL overview chapter:** an overview of the technology used was the expected form of an analysis chapter.
- **The short implementation chapter that points to the CD:** this was common.
- **Visual verification on documented sample animations:** this was the usual method. A separate program written only to test the interpolation is more than most theses did.
- **Typos and English loanwords:** these were common in technical Slovak of the period, while computing terms were still settling.

## Points for the defence

These are slips in the content. They are minor in a work of this size: the kind of thing raised as questions at a defence, not reasons to lower the grade.

| The thesis says | What is actually true |
|---|---|
| With motion start and end defined, the speed at those key states is zero | Doubling the key state gives **half** the average speed, not zero. For example, x(u) = −½u³ + u² + ½u starts with speed 0.5 |
| `dt2312 = (t1 − t0) / (t2 − t1)` | The code computes the inverse ratio. The code is right and matches the worked example in the text; only the printed formula is inverted |
| Fog density only matters for linear fog | Density is used only by exponential fog |
| A camera grouped with a character's eye shows the view through their eyes | Parent transforms are undone in the wrong order, so a camera inside a group that is both rotated and moved does not look where the group points |
| There are separate "motion start" and "motion end" attributes | Only the start flag is read; one "Final" checkbox does both jobs |
| Smooth paths are the default | "Final" is on by default, so new elements have a corner at every key state until it is unticked |
| Textures are 64×64 | Elsewhere the text and the code use 128×128 |

Suggested questions:

1. Show that the time-ratio adjustment gives the same speed on both sides of a key state. Why doesn't doubling the end key state give zero speed?
2. Why is the camera's view matrix loaded into `GL_PROJECTION`? How does that affect fog and specular lighting?
3. Why is "Final" on by default, and why is it ignored for elements inside groups?
4. How would you interpolate rotations to avoid the problems of interpolating Euler angles one by one?
5. What happens when an element exists in key state N but not in N+1?

## Implementation issues found while porting

Porting the program uncovered a series of bugs. The main ones:

- the view matrix loaded into the projection matrix;
- "Final" ignored inside groups;
- permanent motion restarting at every key state;
- the diffuse colour interpolated twice;
- line draw style never taking effect;
- frames captured from the visible window.

None of them is unusual for a program of this size written by one person. All are fixed in the port, which keeps the original data model, file format and interpolation method. [changes-from-1998.md](changes-from-1998.md) lists each fix and its effect on the sample animations.

## Assessment by criterion

| Criterion | Grade |
|---|---|
| Meeting the assignment | A: all requirements met, several exceeded |
| Analysis of the problem | B: its own analysis of the interpolation methods |
| Design: interpolation | A−: the fix for uneven spacing is correct and original, but not formally justified |
| Design: software | B: clean object model, with some transform and flag errors |
| Implementation | B+: large and working, with a series of correctness bugs |
| Verification | B: a dedicated test program plus documented sample animations |
| Text, references, language | B, by the standards of its time |
| **Overall** | **A (excellent)** |
