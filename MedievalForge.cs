using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Medieval Texture Forge – A procedural texture generator for Unity (Editor).
/// Generates Albedo/Height/Normal textures for: Wood Planks, Cobblestone, Woven Cloth, Parchment.
/// - Tiling support
/// - Deterministic randomness via Seed
/// - PNG export + optional Material creation
/// Note: This is an editor-only tool. Place in an Editor/ folder.
/// </summary>
public class MedievalTextureForge : EditorWindow
{
    // --- UI State ---
    private enum TextureType { WoodPlanks, Cobblestone, WovenCloth, Parchment }
    private TextureType textureType = TextureType.WoodPlanks;

    private int size = 512;                      // texture resolution (square)
    private int seed = 12345;                    // deterministic seed
    private bool tiling = true;                  // wrap noise for seamlessness
    private string outputFolder = "Assets/GeneratedTextures";
    private string baseName = "MedievalTex";

    // Palette / Colors
    private Color colA = new Color(0.33f, 0.26f, 0.18f); // dark wood/base
    private Color colB = new Color(0.55f, 0.45f, 0.32f); // mid
    private Color colC = new Color(0.78f, 0.68f, 0.52f); // light
    private float colorVariation = 0.15f;

    // Shared noise controls
    private float grungeAmount = 0.35f;          // overlay dirt/noise
    private float roughness = 0.6f;              // height amplitude
    private float normalIntensity = 1.0f;        // normal strength

    // Wood
    private int plankCount = 6;
    private float plankGap = 0.008f;
    private float woodRingFreq = 5.0f;
    private float woodWarp = 0.08f;
    private float woodKnotChance = 0.35f;

    // Cobble
    private int cobbleCells = 12;                // Voronoi grid resolution
    private float mortarWidth = 0.035f;          // 0..~0.1
    private float stoneRoundness = 0.75f;        // 0..1 (soft edges)
    private float stoneHeightVar = 0.35f;

    // Cloth
    private int weaveDensity = 24;               // threads per texture width
    private float weaveContrast = 0.8f;
    private float threadThickness = 0.6f;        // 0..1

    // Parchment
    private float parchmentFibers = 6.0f;
    private float parchmentCloud = 2.5f;
    private float edgeDarken = 0.4f;

    // Previews
    private Texture2D previewAlbedo;
    private Texture2D previewHeight;
    private Texture2D previewNormal;

    [MenuItem("Window/Medieval Texture Forge")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<MedievalTextureForge>("Medieval Texture Forge");
        wnd.minSize = new Vector2(420, 560);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Medieval Texture Forge", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        textureType = (TextureType)EditorGUILayout.EnumPopup("Texture Type", textureType);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            size = NextPow2(EditorGUILayout.IntSlider("Size", size, 128, 2048));
            seed = EditorGUILayout.IntField("Seed", seed);
            tiling = EditorGUILayout.Toggle("Seamless Tiling", tiling);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            baseName = EditorGUILayout.TextField("Base Name", baseName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            colA = EditorGUILayout.ColorField("Color A (Dark/Base)", colA);
            colB = EditorGUILayout.ColorField("Color B (Mid)", colB);
            colC = EditorGUILayout.ColorField("Color C (Light)", colC);
            colorVariation = EditorGUILayout.Slider("Color Variation", colorVariation, 0f, 0.6f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel);
            grungeAmount = EditorGUILayout.Slider("Grunge Amount", grungeAmount, 0f, 1f);
            roughness = EditorGUILayout.Slider("Height (Roughness)", roughness, 0f, 1.5f);
            normalIntensity = EditorGUILayout.Slider("Normal Intensity", normalIntensity, 0f, 3f);
        }

        // Per-type parameters
        switch (textureType)
        {
            case TextureType.WoodPlanks: DrawWoodParams(); break;
            case TextureType.Cobblestone: DrawCobbleParams(); break;
            case TextureType.WovenCloth: DrawClothParams(); break;
            case TextureType.Parchment: DrawParchmentParams(); break;
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Preview")) Generate();
            if (GUILayout.Button("Save PNGs")) SavePNGs();
            if (GUILayout.Button("Save + Create Material")) SavePNGs(createMaterial: true);
        }

        EditorGUILayout.Space();
        DrawPreviews();
    }

    private void DrawWoodParams()
    {
        EditorGUILayout.LabelField("Wood Planks", EditorStyles.boldLabel);
        plankCount = EditorGUILayout.IntSlider("Plank Count", plankCount, 2, 20);
        plankGap = EditorGUILayout.Slider("Plank Gap", plankGap, 0f, 0.03f);
        woodRingFreq = EditorGUILayout.Slider("Ring Frequency", woodRingFreq, 0.5f, 12f);
        woodWarp = EditorGUILayout.Slider("Grain Warp", woodWarp, 0f, 0.3f);
        woodKnotChance = EditorGUILayout.Slider("Knot Chance", woodKnotChance, 0f, 1f);
    }

    private void DrawCobbleParams()
    {
        EditorGUILayout.LabelField("Cobblestone", EditorStyles.boldLabel);
        cobbleCells = EditorGUILayout.IntSlider("Cells", cobbleCells, 4, 40);
        mortarWidth = EditorGUILayout.Slider("Mortar Width", mortarWidth, 0.005f, 0.08f);
        stoneRoundness = EditorGUILayout.Slider("Stone Roundness", stoneRoundness, 0f, 1f);
        stoneHeightVar = EditorGUILayout.Slider("Height Variation", stoneHeightVar, 0f, 1f);
    }

    private void DrawClothParams()
    {
        EditorGUILayout.LabelField("Woven Cloth", EditorStyles.boldLabel);
        weaveDensity = EditorGUILayout.IntSlider("Weave Density", weaveDensity, 6, 64);
        weaveContrast = EditorGUILayout.Slider("Weave Contrast", weaveContrast, 0.2f, 1.5f);
        threadThickness = EditorGUILayout.Slider("Thread Thickness", threadThickness, 0.2f, 1.0f);
    }

    private void DrawParchmentParams()
    {
        EditorGUILayout.LabelField("Parchment", EditorStyles.boldLabel);
        parchmentFibers = EditorGUILayout.Slider("Fiber Frequency", parchmentFibers, 0.5f, 12f);
        parchmentCloud = EditorGUILayout.Slider("Cloudiness", parchmentCloud, 0.5f, 6f);
        edgeDarken = EditorGUILayout.Slider("Edge Darkening", edgeDarken, 0f, 1f);
    }

    // --- Generation Pipeline ---
    private void Generate()
    {
        // Prepare RNG
        var rng = new System.Random(seed);

        var albedo = NewTex(size);
        var height = NewTex(size);

        switch (textureType)
        {
            case TextureType.WoodPlanks:
                GenerateWood(albedo, height, rng);
                break;
            case TextureType.Cobblestone:
                GenerateCobble(albedo, height, rng);
                break;
            case TextureType.WovenCloth:
                GenerateCloth(albedo, height, rng);
                break;
            case TextureType.Parchment:
                GenerateParchment(albedo, height, rng);
                break;
        }

        // Grunge overlay & color variation
        OverlayGrunge(albedo, height, rng);

        // Normal from height
        var normal = HeightToNormal(height, normalIntensity, tiling);

        // Assign previews
        previewAlbedo = albedo;
        previewHeight = height;
        previewNormal = normal;

        Repaint();
    }

    private void SavePNGs(bool createMaterial = false)
    {
        if (previewAlbedo == null) Generate();

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string prefix = $"{outputFolder}/{baseName}_{textureType}_{size}_{stamp}";

        SaveTextureAsPNG(previewAlbedo, $"{prefix}_Albedo.png");
        SaveTextureAsPNG(previewHeight, $"{prefix}_Height.png");
        SaveTextureAsPNG(previewNormal, $"{prefix}_Normal.png");

        AssetDatabase.Refresh();

        if (createMaterial)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{prefix}_Albedo.png"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{prefix}_Normal.png"));
            mat.EnableKeyword("_NORMALMAP");
            AssetDatabase.CreateAsset(mat, $"{prefix}.mat");
            AssetDatabase.SaveAssets();
        }
    }

    private void DrawPreviews()
    {
        if (previewAlbedo == null) return;

        EditorGUILayout.LabelField("Previews", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTextureBox("Albedo", previewAlbedo);
            DrawTextureBox("Height", previewHeight);
            DrawTextureBox("Normal", previewNormal);
        }
    }

    private void DrawTextureBox(string label, Texture2D tex)
    {
        using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(128)))
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            GUILayout.Label(tex, GUILayout.Width(128), GUILayout.Height(128));
        }
    }

    // --- Generators ---

    // WOOD: plank layout + ring/grain via warped radial noise + subtle knots
    private void GenerateWood(Texture2D albedo, Texture2D height, System.Random rng)
    {
        int w = albedo.width, h = albedo.height;
        float invW = 1f / w, invH = 1f / h;

        // Random plank offsets
        float[] plankShifts = new float[plankCount];
        for (int p = 0; p < plankCount; p++)
            plankShifts[p] = (float)rng.NextDouble() * 10f;

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) * invH;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) * invW;

                // Plank index (horizontal planks)
                int pi = Mathf.FloorToInt(u * plankCount);
                float uLocal = u * plankCount - pi;

                // Plank gaps
                float gap = Mathf.SmoothStep(0f, plankGap, Mathf.Abs(uLocal - 0.5f));
                gap = Mathf.Clamp01((gap - plankGap * 0.5f) * 8f);
                float gapMask = 1f - gap;

                // Warped rings: distance to a moving center per plank
                float cx = 0.5f + 0.2f * Mathf.Sin((v + plankShifts[pi]) * 3.1f);
                float cy = 0.5f + 0.2f * Mathf.Cos((v + plankShifts[pi]) * 2.7f);
                float dx = (uLocal - cx + 0.5f), dy = (v - cy);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Warp with Perlin
                float warp = woodWarp * TileableNoise(u * 4f, v * 4f, tiling);
                float rings = Mathf.Sin((dist + warp) * woodRingFreq * Mathf.PI * 2f) * 0.5f + 0.5f;

                // Knot mask (random blobs)
                float knot = (float)rng.NextDouble() < woodKnotChance
                    ? Mathf.Pow(TileableNoise(u * 12f + plankShifts[pi], v * 12f, tiling), 6f)
                    : 0f;

                // Height & color
                float hgt = Mathf.Lerp(0.35f, 0.65f, rings) + knot * 0.25f;
                hgt *= gapMask; // recess the gap a bit

                Color baseCol = TriBlend(colA, colB, colC, rings);
                baseCol = JitterColor(baseCol, colorVariation, rng);
                baseCol *= Mathf.Lerp(0.9f, 1.05f, hgt);

                // Darken gaps
                baseCol = Color.Lerp(baseCol, new Color(0.05f, 0.05f, 0.05f), (1f - gapMask) * 0.9f);

                albedo.SetPixel(x, y, baseCol);
                height.SetPixel(x, y, new Color(hgt, hgt, hgt, 1f));
            }
        }
        albedo.Apply(false);
        height.Apply(false);
    }

    // COBBLE: Voronoi distance to cell centers (simple jittered grid) + mortar
    private void GenerateCobble(Texture2D albedo, Texture2D height, System.Random rng)
    {
        int w = albedo.width, h = albedo.height;
        int cells = Mathf.Max(2, cobbleCells);

        // Jittered grid centers
        Vector2[,] centers = new Vector2[cells, cells];
        for (int j = 0; j < cells; j++)
        for (int i = 0; i < cells; i++)
        {
            float rx = (float)rng.NextDouble() * 0.6f + 0.2f;
            float ry = (float)rng.NextDouble() * 0.6f + 0.2f;
            centers[i, j] = new Vector2((i + rx) / cells, (j + ry) / cells);
        }

        // For tiling, mirror access
        Vector2 GetCenterWrapped(int i, int j)
        {
            i = (i + cells) % cells;
            j = (j + cells) % cells;
            return centers[i, j];
        }

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;

                // Find nearest and second-nearest center (for cell borders)
                float d1 = 10f, d2 = 10f;
                Vector2 best = Vector2.zero;

                // Examine 3x3 neighborhood to support tiling
                int ci = Mathf.FloorToInt(u * cells);
                int cj = Mathf.FloorToInt(v * cells);

                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    var c = GetCenterWrapped(ci + i, cj + j);

                    // Account for toroidal wrap if tiling
                    Vector2 p = new Vector2(u, v);
                    Vector2 delta = p - c;
                    if (tiling)
                    {
                        if (delta.x > 0.5f) delta.x -= 1f;
                        if (delta.x < -0.5f) delta.x += 1f;
                        if (delta.y > 0.5f) delta.y -= 1f;
                        if (delta.y < -0.5f) delta.y += 1f;
                    }
                    float d = delta.sqrMagnitude;

                    if (d < d1)
                    {
                        d2 = d1; d1 = d; best = c;
                    }
                    else if (d < d2)
                    {
                        d2 = d;
                    }
                }

                float border = Mathf.Clamp01((Mathf.Sqrt(d2) - Mathf.Sqrt(d1)) * (cells * 1.2f));
                float mortar = Mathf.SmoothStep(0f, mortarWidth, border);
                float stoneMask = Mathf.SmoothStep(mortarWidth, 0f, mortar);

                // Stone height with variation
                float cellNoise = TileableNoise(best.x * 12f, best.y * 12f, tiling);
                float hgt = Mathf.Lerp(0.35f, 0.65f, cellNoise);
                hgt += (TileableNoise(u * 8f, v * 8f, tiling) - 0.5f) * stoneHeightVar;
                hgt = Mathf.Clamp01(hgt) * stoneMask;

                // Roundness softens edges
                hgt = Mathf.Lerp(hgt, Mathf.Pow(hgt, stoneRoundness * 2f + 0.1f), 0.5f);

                Color baseCol = TriBlend(colA, colB, colC, hgt);
                baseCol = JitterColor(baseCol, colorVariation, rng);

                // Mortar color
                Color mortarCol = Color.Lerp(new Color(0.18f, 0.18f, 0.18f), new Color(0.27f, 0.27f, 0.27f), 0.5f);
                Color final = Color.Lerp(mortarCol, baseCol, stoneMask);

                albedo.SetPixel(x, y, final);
                height.SetPixel(x, y, new Color(hgt, hgt, hgt, 1f));
            }
        }
        albedo.Apply(false);
        height.Apply(false);
    }

    // CLOTH: simple orthogonal weave with sinusoidal thread bulge
    private void GenerateCloth(Texture2D albedo, Texture2D height, System.Random rng)
    {
        int w = albedo.width, h = albedo.height;

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;

                float s = u * weaveDensity;
                float t = v * weaveDensity;

                float warp = Frac(s); // vertical threads
                float weft = Frac(t); // horizontal threads

                float warpProfile = ThreadProfile(warp, threadThickness);
                float weftProfile = ThreadProfile(weft, threadThickness);

                // Over/under pattern (checkerboard)
                bool over = ((Mathf.FloorToInt(s) + Mathf.FloorToInt(t)) & 1) == 0;
                float weave = over
                    ? Mathf.Lerp(weftProfile, warpProfile, weaveContrast)
                    : Mathf.Lerp(warpProfile, weftProfile, weaveContrast);

                float hgt = Mathf.Clamp01(0.35f + weave * 0.65f);

                Color baseCol = TriBlend(colA, colB, colC, weave);
                baseCol = JitterColor(baseCol, colorVariation * 0.5f, rng);

                albedo.SetPixel(x, y, baseCol);
                height.SetPixel(x, y, new Color(hgt, hgt, hgt, 1f));
            }
        }
        albedo.Apply(false);
        height.Apply(false);
    }

    // PARCHMENT: fBm noise + fibers + edge darkening
    private void GenerateParchment(Texture2D albedo, Texture2D height, System.Random rng)
    {
        int w = albedo.width, h = albedo.height;

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;

                float cloud = FBM(u, v, parchmentCloud, 4, tiling);
                float fibers = Mathf.Pow(TileableNoise(u * parchmentFibers, v * parchmentFibers * 1.8f, tiling), 4f);

                // Edge darkening (radial)
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.SmoothStep(0.2f, 0.7f, r);
                float edgeMask = Mathf.Lerp(1f, 1f - edgeDarken, edge);

                float hgt = Mathf.Clamp01(0.45f + cloud * 0.35f + fibers * 0.2f);
                Color baseCol = TriBlend(colA, colB, colC, hgt);
                baseCol = Color.Lerp(baseCol, new Color(0.85f, 0.78f, 0.6f), 0.6f); // paper tint
                baseCol *= edgeMask;

                albedo.SetPixel(x, y, baseCol);
                height.SetPixel(x, y, new Color(hgt, hgt, hgt, 1f));
            }
        }
        albedo.Apply(false);
        height.Apply(false);
    }

    // --- Post passes ---

    private void OverlayGrunge(Texture2D albedo, Texture2D height, System.Random rng)
    {
        if (grungeAmount <= 0.001f) return;

        int w = albedo.width, h = albedo.height;
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;

                float g1 = FBM(u, v, 2.0f, 3, tiling);
                float g2 = FBM(u + 13.12f, v + 7.9f, 7.0f, 2, tiling);
                float g = Mathf.Clamp01((g1 * 0.7f + g2 * 0.3f));

                Color c = albedo.GetPixel(x, y);
                float hgt = height.GetPixel(x, y).r;

                // Darken crevices slightly + random dirt
                float dirt = Mathf.Lerp(1f, 0.8f, g * grungeAmount);
                float crevice = Mathf.Lerp(1f, 0.85f, (1f - hgt) * grungeAmount * 0.5f);
                c *= dirt * crevice;

                albedo.SetPixel(x, y, c);
            }
        }
        albedo.Apply(false);
    }

    private Texture2D HeightToNormal(Texture2D height, float intensity, bool wrap)
    {
        int w = height.width, h = height.height;
        var normal = NewTex(w, h);

        // Sobel-like deriv
        for (int y = 0; y < h; y++)
        {
            int yp = (y + 1) % h, ym = (y - 1 + h) % h;
            for (int x = 0; x < w; x++)
            {
                int xp = (x + 1) % w, xm = (x - 1 + w) % w;

                float c = height.GetPixel(x, y).r;
                float r = height.GetPixel(wrap ? xp : Mathf.Min(x + 1, w - 1), y).r;
                float l = height.GetPixel(wrap ? xm : Mathf.Max(x - 1, 0), y).r;
                float u = height.GetPixel(x, wrap ? yp : Mathf.Min(y + 1, h - 1)).r;
                float d = height.GetPixel(x, wrap ? ym : Mathf.Max(y - 1, 0)).r;

                float dx = (r - l);
                float dy = (u - d);

                Vector3 n = new Vector3(-dx * intensity, -dy * intensity, 1f).normalized;
                Color nc = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                normal.SetPixel(x, y, nc);
            }
        }

        normal.Apply(false);
        return normal;
    }

    // --- Helpers ---

    private static Texture2D NewTex(int s, int? optionalHeight = null)
    {
        int h = optionalHeight ?? s;
        var t = new Texture2D(s, h, TextureFormat.RGBA32, false, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    private static void SaveTextureAsPNG(Texture2D tex, string path)
    {
        var bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    private static int NextPow2(int v)
    {
        int p = 128;
        while (p < v) p <<= 1;
        return p;
    }

    // Tri-color blend by t (0..1)
    private static Color TriBlend(Color a, Color b, Color c, float t)
    {
        if (t < 0.5f) return Color.Lerp(a, b, t * 2f);
        return Color.Lerp(b, c, (t - 0.5f) * 2f);
    }

    private static Color JitterColor(Color c, float amount, System.Random rng)
    {
        if (amount <= 0f) return c;
        float r = (float)(rng.NextDouble() * 2 - 1) * amount;
        float g = (float)(rng.NextDouble() * 2 - 1) * amount;
        float b = (float)(rng.NextDouble() * 2 - 1) * amount;
        return new Color(
            Mathf.Clamp01(c.r + r),
            Mathf.Clamp01(c.g + g),
            Mathf.Clamp01(c.b + b),
            1f
        );
    }

    // Seamless Perlin via wrap (tileable by sampling on torus)
    private static float TileableNoise(float x, float y, bool tile)
    {
        if (!tile) return Mathf.PerlinNoise(x, y);

        // Sample 4 corners around a torus and bilerp (classic trick)
        float n00 = Mathf.PerlinNoise(x, y);
        float n10 = Mathf.PerlinNoise(x + 1f, y);
        float n01 = Mathf.PerlinNoise(x, y + 1f);
        float n11 = Mathf.PerlinNoise(x + 1f, y + 1f);

        float fx = Frac(x);
        float fy = Frac(y);

        float n0 = Mathf.Lerp(n00, n10, fx);
        float n1 = Mathf.Lerp(n01, n11, fx);
        return Mathf.Lerp(n0, n1, fy);
    }

    // Fractional part (0..1)
    private static float Frac(float v) => v - Mathf.Floor(v);

    // Simple FBM (fractal brownian motion) using tileable noise
    private static float FBM(float x, float y, float scale, int octaves, bool tile)
    {
        float amp = 0.5f;
        float freq = scale;
        float sum = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += TileableNoise(x * freq, y * freq, tile) * amp;
            freq *= 2f;
            amp *= 0.5f;
        }
        return Mathf.Clamp01(sum);
    }

    private static float ThreadProfile(float t, float thickness)
    {
        // A soft box profile with a rounded top (sine)
        float center = Mathf.Abs(t - 0.5f) * 2f;
        float box = Mathf.Clamp01(1f - center / Mathf.Clamp(thickness, 0.0001f, 1f));
        float round = Mathf.Sin(Mathf.Clamp01(box) * Mathf.PI * 0.5f);
        return Mathf.Clamp01((box * 0.6f + round * 0.4f));
    }
}
