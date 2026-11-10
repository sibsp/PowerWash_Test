using UnityEngine;

/// <summary>
/// Makes an object dirty. Dirt is stored per texture pixel (0 = clean, 1 = filthy) and drawn into
/// a runtime texture on the object's material. SprayNozzle calls Clean() wherever water hits.
/// Needs a non-convex MeshCollider (for hit texture coordinates) and a mesh with non-overlapping UVs.
///
/// Optional textures (all use the mesh's UVs, so paint them in Blender on the same UV layout):
///  - Clean Texture: what the bone looks like once washed.
///  - Dirt Texture: what the dirt looks like (can tile).
///  - Toughness Map: greyscale. Black = any nozzle cleans it, white = only the strongest nozzle.
///  - Dirt Layout Map: greyscale. White = starts dirty, black = starts clean.
/// Textures don't need Read/Write enabled — they're copied via the GPU at start-up.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class DirtSurface : MonoBehaviour
{
    [Header("Dirt texture resolution")]
    public int textureWidth = 768;
    public int textureHeight = 512;

    [Header("Textures (optional)")]
    public Texture cleanTexture;
    public Texture dirtTexture;
    [Tooltip("How many times the dirt texture repeats across the UVs (needs the texture's Wrap Mode set to Repeat)")]
    public float dirtTiling = 3f;
    public Texture toughnessMap;
    public Texture dirtLayoutMap;

    [Header("Colours (used when no texture is set)")]
    public Color cleanColor = new Color(0.86f, 0.80f, 0.68f);   // bone
    public Color dirtColor = new Color(0.30f, 0.22f, 0.13f);    // mud
    [Tooltip("Tint applied to tough dirt so players can see it needs a stronger nozzle")]
    public Color toughDirtColor = new Color(0.12f, 0.11f, 0.10f);
    [Range(0f, 1f)] public float toughTintStrength = 0.7f;
    public float noiseScale = 8f;
    [Range(0f, 1f)] public float minDirt = 0.6f;
    [Range(0f, 0.3f)] public float colourVariation = 0.06f;
    [Tooltip("On: dirt fades out gradually. Off: dirt stays until it pops clean (hard edges)")]
    public bool softEdges = true;

    [Header("Cleaning")]
    [Tooltip("A pixel snaps to clean once its dirt drops below this")]
    [Range(0.01f, 0.3f)] public float cleanThreshold = 0.08f;
    [Tooltip("Fraction of the surface that must be clean to count as finished")]
    [Range(0.5f, 1f)] public float completeAt = 0.98f;
    [Tooltip("Tough dirt also washes off more slowly (0 = same speed, 1 = much slower)")]
    [Range(0f, 1f)] public float toughSlowdown = 0.5f;

    public float PercentClean => totalPixels == 0 ? 0f : (float)cleanPixels / totalPixels;
    public bool IsComplete => PercentClean >= completeAt;

    Texture2D tex;
    float[] dirt, toughness;
    Color32[] pixels, cleanPix, dirtPix;
    Color32[] cleanSrc, dirtSrc, toughSrc, layoutSrc;
    RectInt[] regions;
    int totalPixels, cleanPixels;
    bool needsApply;

    void Awake()
    {
        int w = textureWidth, h = textureHeight;
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name + "_Dirt",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Material mat = GetComponent<Renderer>().material; // instance, so other objects aren't affected
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);

        var atlas = GetComponent<AtlasCube>();
        regions = atlas != null
            ? atlas.GetFaceRects(w, h)
            : new[] { new RectInt(0, 0, w, h) };

        int n = w * h;
        dirt = new float[n];
        toughness = new float[n];
        pixels = new Color32[n];
        cleanPix = new Color32[n];
        dirtPix = new Color32[n];

        // Copy any supplied textures to our resolution once.
        cleanSrc = ReadTexture(cleanTexture, w, h, 1f);
        dirtSrc = ReadTexture(dirtTexture, w, h, dirtTiling);
        toughSrc = ReadTexture(toughnessMap, w, h, 1f);
        layoutSrc = ReadTexture(dirtLayoutMap, w, h, 1f);

        ResetDirt();
    }

    /// <summary>Covers the surface in fresh dirt.</summary>
    public void ResetDirt()
    {
        int w = textureWidth, h = textureHeight;
        float ox = Random.value * 1000f, oy = Random.value * 1000f;

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = cleanSrc != null ? cleanSrc[i] : (Color32)cleanColor;
            dirt[i] = 0f;
        }

        totalPixels = 0;
        cleanPixels = 0;
        foreach (RectInt r in regions)
        {
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                int i = y * w + x;
                float nx = (float)x / h * noiseScale, ny = (float)y / h * noiseScale;

                // How tough is the dirt here?
                toughness[i] = toughSrc != null ? toughSrc[i].r / 255f : 0f;

                // How dirty does it start?
                float n = Mathf.PerlinNoise(ox + nx, oy + ny) * 0.65f
                        + Mathf.PerlinNoise(ox + nx * 3.1f, oy + ny * 3.1f) * 0.35f;
                float d = Mathf.Lerp(minDirt, 1f, n);
                if (layoutSrc != null) d *= layoutSrc[i].r / 255f;
                if (toughness[i] > 0.01f) d = Mathf.Max(d, minDirt); // tough patches are always dirty
                dirt[i] = d < cleanThreshold ? 0f : d;

                // Clean look: texture, or bone colour with gentle tone variation + fine grain
                if (cleanSrc != null) cleanPix[i] = cleanSrc[i];
                else
                {
                    float v1 = (Mathf.PerlinNoise(ox * 0.5f + nx * 0.4f, oy * 0.5f + ny * 0.4f) - 0.5f) * colourVariation * 2f
                             + (Mathf.PerlinNoise(ox * 0.7f + nx * 12f, oy * 0.7f + ny * 12f) - 0.5f) * colourVariation * 0.6f;
                    cleanPix[i] = Shade(cleanColor, v1);
                }

                // Dirt look: texture or mud colour, darkened towards the tough colour where it's tough
                Color dc;
                if (dirtSrc != null) dc = dirtSrc[i];
                else
                {
                    float v2 = (Mathf.PerlinNoise(oy * 0.5f + nx * 9f, ox * 0.5f + ny * 9f) - 0.5f) * colourVariation * 4f;
                    dc = Shade(dirtColor, v2);
                }
                dc = Color.Lerp(dc, toughDirtColor, toughness[i] * toughTintStrength);
                dc.a = 1f;
                dirtPix[i] = dc;

                pixels[i] = Blend(i);
                totalPixels++;
                if (dirt[i] == 0f) cleanPixels++;
            }
        }
        needsApply = true;
    }

    /// <summary>
    /// Removes dirt in a circle around a texture coordinate.
    /// radiusPx: size in texture pixels. amount: dirt removed at the centre.
    /// strength: nozzle strength 0-1; pixels tougher than this are not affected.
    /// Returns true if the hit point itself was too tough for this nozzle.
    /// </summary>
    public bool Clean(Vector2 uv, float radiusPx, float amount, float strength = 1f)
    {
        if (amount <= 0f) return false;
        int w = textureWidth, h = textureHeight;
        int px = Mathf.FloorToInt(uv.x * w), py = Mathf.FloorToInt(uv.y * h);

        // Only clean inside the face that was hit, so water doesn't leak onto other faces.
        RectInt region = default;
        bool found = false;
        var p = new Vector2Int(px, py);
        foreach (RectInt r in regions)
            if (r.Contains(p)) { region = r; found = true; break; }
        if (!found) return false;

        int centre = py * w + px;
        bool blocked = dirt[centre] > 0f && toughness[centre] > strength + 0.001f;

        int rad = Mathf.CeilToInt(radiusPx);
        int x0 = Mathf.Max(region.xMin, px - rad), x1 = Mathf.Min(region.xMax - 1, px + rad);
        int y0 = Mathf.Max(region.yMin, py - rad), y1 = Mathf.Min(region.yMax - 1, py + rad);
        float r2 = radiusPx * radiusPx;

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int dx = x - px, dy = y - py;
            float d2 = dx * dx + dy * dy;
            if (d2 > r2) continue;

            int i = y * w + x;
            float old = dirt[i];
            if (old <= 0f) continue;
            if (toughness[i] > strength + 0.001f) continue;   // nozzle too weak for this dirt

            float falloff = 1f - Mathf.Sqrt(d2) / radiusPx;
            float rate = 1f - toughness[i] * toughSlowdown;
            float now = Mathf.Max(0f, old - amount * falloff * rate);
            if (now < cleanThreshold)
            {
                now = 0f;           // snap to fully clean
                cleanPixels++;
            }
            dirt[i] = now;
            pixels[i] = Blend(i);
        }
        needsApply = true;
        return blocked;
    }

    void LateUpdate()
    {
        if (!needsApply) return;
        tex.SetPixels32(pixels);
        tex.Apply(false);
        needsApply = false;
    }

    void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }

    Color32 Blend(int i)
    {
        float d = dirt[i];
        float t;
        if (softEdges)
            t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, minDirt, d));   // fades as it's washed
        else
            t = d < cleanThreshold ? 0f : Mathf.Lerp(0.45f, 1f, d);          // stays dirty until it pops clean
        return Color32.Lerp(cleanPix[i], dirtPix[i], t);
    }

    static Color32 Shade(Color c, float v)
    {
        Color s = c * (1f + v);
        s.a = 1f;
        return s;
    }

    /// <summary>
    /// Copies any texture to a pixel array of the given size using the GPU,
    /// so the source doesn't need Read/Write enabled. Returns null if no texture.
    /// </summary>
    static Color32[] ReadTexture(Texture source, int w, int h, float tiling)
    {
        if (source == null) return null;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt, new Vector2(tiling, tiling), Vector2.zero);

        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        copy.Apply(false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] result = copy.GetPixels32();
        Destroy(copy);
        return result;
    }
}
