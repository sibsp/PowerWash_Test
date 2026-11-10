using UnityEngine;

/// <summary>
/// Replaces Unity's default cube mesh with one where each face has its OWN area of the texture
/// (a 3 x 2 grid). The default cube maps every face to the same texture area, so washing one
/// face would clean all six. A real bone model just needs non-overlapping UVs instead of this.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(MeshFilter))]
public class AtlasCube : MonoBehaviour
{
    [Tooltip("Gap around each face in the texture, as a fraction of a grid cell")]
    [Range(0f, 0.1f)] public float padding = 0.02f;

    const int Cols = 3, Rows = 2;

    void Awake()
    {
        Mesh mesh = BuildMesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        // Raycasts only report texture coordinates when they hit a (non-convex) MeshCollider.
        var box = GetComponent<BoxCollider>();
        if (box != null) Destroy(box);
        var mc = GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
        mc.convex = false;
        mc.sharedMesh = mesh;
    }

    /// <summary>Pixel rectangles of each face in a texture of the given size.</summary>
    public RectInt[] GetFaceRects(int width, int height)
    {
        var rects = new RectInt[6];
        for (int i = 0; i < 6; i++)
        {
            int col = i % Cols, row = i / Cols;
            float u0 = (col + padding) / Cols, u1 = (col + 1 - padding) / Cols;
            float v0 = (row + padding) / Rows, v1 = (row + 1 - padding) / Rows;
            int x0 = Mathf.FloorToInt(u0 * width), x1 = Mathf.CeilToInt(u1 * width);
            int y0 = Mathf.FloorToInt(v0 * height), y1 = Mathf.CeilToInt(v1 * height);
            rects[i] = new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
        return rects;
    }

    Mesh BuildMesh()
    {
        // normal, right (a), up (b) for each face, chosen so the triangles face outwards.
        Vector3[,] faces =
        {
            { Vector3.right,   Vector3.forward, Vector3.up },
            { Vector3.left,    Vector3.back,    Vector3.up },
            { Vector3.up,      Vector3.right,   Vector3.forward },
            { Vector3.down,    Vector3.right,   Vector3.back },
            { Vector3.forward, Vector3.left,    Vector3.up },
            { Vector3.back,    Vector3.right,   Vector3.up },
        };

        var verts = new Vector3[24];
        var norms = new Vector3[24];
        var uvs = new Vector2[24];
        var tris = new int[36];

        for (int f = 0; f < 6; f++)
        {
            Vector3 n = faces[f, 0], a = faces[f, 1], b = faces[f, 2];
            Vector3 c = n * 0.5f;
            int v = f * 4;
            verts[v + 0] = c - a * 0.5f - b * 0.5f;
            verts[v + 1] = c - a * 0.5f + b * 0.5f;
            verts[v + 2] = c + a * 0.5f + b * 0.5f;
            verts[v + 3] = c + a * 0.5f - b * 0.5f;
            for (int k = 0; k < 4; k++) norms[v + k] = n;

            int col = f % Cols, row = f / Cols;
            float u0 = (col + padding) / Cols, u1 = (col + 1 - padding) / Cols;
            float v0 = (row + padding) / Rows, v1 = (row + 1 - padding) / Rows;
            uvs[v + 0] = new Vector2(u0, v0);
            uvs[v + 1] = new Vector2(u0, v1);
            uvs[v + 2] = new Vector2(u1, v1);
            uvs[v + 3] = new Vector2(u1, v0);

            int t = f * 6;
            tris[t + 0] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }

        var mesh = new Mesh { name = "AtlasCube" };
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
