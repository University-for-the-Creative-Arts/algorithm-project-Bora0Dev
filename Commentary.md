# Commentary: Medieval Texture Forge

## Overview

**Medieval Texture Forge** is a procedural texture generation system
built as a Unity Editor extension. It allows artists and developers to
synthesize tileable, stylized materials such as **wood planks**,
**cobblestone**, **woven cloth**, and **parchment** entirely within the
Unity Editor---no external texture assets required. The system
procedurally generates **Albedo**, **Height**, and **Normal** maps,
enabling physically based rendering (PBR) compatibility. It also
supports **deterministic seeds**, **tiled outputs**, and **automated
material creation**.

This tool exemplifies the power of **algorithmic art generation**, where
controlled randomness and mathematical functions define material
appearance rather than pixel painting or photographic capture.

## How It Works

The system uses **noise-based synthesis**, **analytical patterns**, and
**randomized parameter variation** to construct realistic and tileable
textures. The workflow proceeds as follows:

1.  **User Interface (Editor Window)** -- Built with Unity's
    `EditorGUILayout` API, providing sliders, color pickers, and toggles
    for texture parameters (size, seed, material type, tiling, color
    palette, and surface characteristics). Each texture type exposes
    specialized controls (e.g., plank count, mortar width, weave
    density).

2.  **Deterministic Randomness** -- A seeded `System.Random` ensures
    that the same input parameters yield identical textures, critical
    for reproducible procedural generation.

3.  **Texture Generation Pipeline**

    -   **Initialization** -- Two primary textures are created: Albedo
        (color) and Height (scalar heightmap).\
    -   **Per-Type Generator** -- Depending on the selected mode, the
        system executes a specialized algorithm (`GenerateWood`,
        `GenerateCobble`, `GenerateCloth`, `GenerateParchment`).
    -   **Post-Processing** -- Grunge and color variation overlays
        introduce organic irregularities. The Height map is converted
        into a Normal map via a Sobel-like gradient kernel.
    -   **Export** -- Final textures are saved as PNGs and optionally
        used to create a Unity `Material` asset configured with URP's
        Lit shader.

## Algorithms and Techniques

### Tileable Noise

A critical feature is **seamless tiling**, achieved by sampling Perlin
noise in a toroidal manner. The function `TileableNoise(x, y, tile)`
bilinearly blends four corner samples to produce wrap-around continuity.
This allows generated textures to repeat infinitely without visible
seams.

### Material-Specific Models

-   **Wood Planks** -- Uses warped radial noise to simulate growth rings
    and sinusoidal plank variations. Randomized knots and plank offsets
    add realism.
-   **Cobblestone** -- Employs a 2D Voronoi diagram approximation via
    nearest-neighbor distance to jittered grid centers. Mortar width,
    stone height variation, and roundness are adjustable.
-   **Woven Cloth** -- Models interlaced threads using sinusoidal
    profiles along orthogonal warp/weft directions, combined in a
    checkerboard pattern to represent over/under threading.
-   **Parchment** -- Combines fractal Brownian motion (FBM) noise for
    paper grain with fine fiber texture and radial edge darkening for an
    aged look.

### Procedural Nature

Every texture pixel results from **mathematical computation**, not fixed
assets. This allows infinite variation by simply changing seeds or
parameters. Techniques like **FBM**, **color jitter**, and
**height-dependent shading** create naturalistic diversity without
repetition.

### APIs and Tools

-   **Unity Editor GUI (EditorGUILayout)** -- For parameter input.
-   **Texture2D API** -- For pixel-level access and export.
-   **Mathf.PerlinNoise** -- Unity's built-in 2D Perlin noise function.
-   **AssetDatabase** -- To integrate generated assets into Unity's
    project structure.
-   **System.IO** -- For file writing (PNG export).

## Conclusion

Medieval Texture Forge exemplifies **procedural generation** in
practical game development: mathematically defined, infinitely variable,
and deterministic textures created from user-controllable inputs. By
leveraging Unity's Editor extensibility and built-in noise algorithms,
it provides an efficient, fully in-engine alternative to manual texture
creation workflows.
