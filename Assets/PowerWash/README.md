# PowerWash MVP – washing dirt off a bone

## Run it
Two versions, each built from the **PowerWash** menu, then press **Play**:
- **Build MVP Scene (texture version)** – per-pixel dirt in a texture (`PowerWashMVP.unity`).
- **Build Patch Scene (smoke and mirrors)** – dirt clumps that pop off (`PowerWashPatches.unity`).

## Controls
LMB spray · 1/2/3 or scroll = nozzle · WASD move · Shift sprint · Ctrl crouch
Q/E spin bone · Z/C tip bone · R reset dirt · Esc free mouse (click to recapture)

## How it works
- **DirtSurface** stores a dirt value (0–1) for every pixel of a texture it creates on the object's material.
- **SprayNozzle** fires a cone of raycasts from the camera each frame. Each hit returns the texture
  coordinate (`RaycastHit.textureCoord`) and calls `DirtSurface.Clean()` there, which lowers dirt in a small circle.
  Once a pixel drops below the threshold it snaps to clean and counts towards the % score.
- The particle system is only visual; the raycasts do the cleaning.
- **AtlasCube** gives the cube's six faces separate areas of the texture (Unity's default cube
  shares one area between all faces, so cleaning one face would clean them all).

## Patch version ("smoke and mirrors")
The bone is just a normal clean object. The dirt is separate **DirtPatch** objects stuck on top: each is a
lumpy mesh + BoxCollider. Spraying finds everything inside a sphere around the crosshair (bigger sphere for
wider nozzles) and damages it; patches wobble, shrink, then pop off with mud particles and a sound.
- **Required Strength** on a patch: which nozzle can shift it (Wide 0.3, Fan 0.6, Pencil 1.0).
- **Durability**: how long it takes. Its look comes from its material (MudLoose / MudCaked / MudCrust).
- To design a level, duplicate patches and drag them where you want — no UVs, textures or MeshCollider rules.
  Works the same on any bone model: just parent the patches to it.
- PatchManager handles the score, particles and sounds. Sounds are generated in code (ProceduralAudio) as
  placeholders; swap in real recordings later.

## Textures and tough dirt (texture version)
DirtSurface has four optional texture slots. All use the model's UVs, so paint them in Blender on the same UV layout:
- **Clean Texture** – what the bone looks like when washed.
- **Dirt Texture** – the dirt (tiles; set **Dirt Tiling**).
- **Toughness Map** – greyscale. Black: any nozzle. Mid-grey (~0.5): fan or pencil. White-ish (~0.9): pencil only.
  Each nozzle has a **Strength** (Wide 0.3, Fan 0.6, Pencil 1.0) and only cleans dirt whose toughness is at or below it.
  Tough dirt is tinted darker (Tough Dirt Color) and washes off a bit slower (Tough Slowdown).
- **Dirt Layout Map** – greyscale. White starts dirty, black starts clean (otherwise random).

Leave the import settings at their defaults; Read/Write isn't needed. Test versions are in `TestTextures/`
(the toughness map is laid out for the cube's 3 x 2 face grid). In Blender, the Toughness map can be made in
Texture Paint mode: new black image, paint grey/white where the grime should be stubborn, save as PNG.

## Swapping the cube for a real bone model
1. Model must have **non-overlapping UVs** (e.g. in Blender: Smart UV Project, or use the lightmap UV set).
2. Put the model in the scene, add a **MeshCollider** (not convex) and **DirtSurface**. Don't add AtlasCube.
3. Optionally add BoneTurntable. Tune texture size, splash radius and power to suit the model's scale.

## Ideas for next steps
Multiple dirt types (some need the pencil nozzle), water/pressure upgrades, sound, a wet-look shader,
several bones per level, timer/score, replacing OnGUI with a proper UI.
