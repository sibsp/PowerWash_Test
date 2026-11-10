using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: PowerWash > ...
///  - Build MVP Scene: texture version (per-pixel dirt).
///  - Build Patch Scene: "smoke and mirrors" version (dirt clumps that pop off).
/// </summary>
public static class PowerWashSceneBuilder
{
    const string Root = "Assets/PowerWash";
    const string MatFolder = Root + "/Materials";
    const string MeshFolder = Root + "/Meshes";
    const string TexFolder = Root + "/TestTextures";
    const string ScenePath = "Assets/Scenes/PowerWashMVP.unity";
    const string PatchScenePath = "Assets/Scenes/PowerWashPatches.unity";

    // ---------------------------------------------------------------- Texture version

    [MenuItem("PowerWash/Build MVP Scene (texture version)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildEnvironment();

        var bone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bone.name = "Bone (cube placeholder)";
        bone.transform.position = new Vector3(0f, 1.6f, 0f);
        bone.transform.localScale = Vector3.one * 0.6f;
        Object.DestroyImmediate(bone.GetComponent<BoxCollider>());
        bone.AddComponent<MeshCollider>();
        bone.AddComponent<AtlasCube>();
        var dirtSurface = bone.AddComponent<DirtSurface>();
        // Test textures (swap for the student's own Blender textures)
        dirtSurface.cleanTexture = AssetDatabase.LoadAssetAtPath<Texture>(TexFolder + "/BoneClean.png");
        dirtSurface.dirtTexture = AssetDatabase.LoadAssetAtPath<Texture>(TexFolder + "/Mud.png");
        dirtSurface.toughnessMap = AssetDatabase.LoadAssetAtPath<Texture>(TexFolder + "/Toughness.png");
        bone.AddComponent<BoneTurntable>();
        SetMat(bone, Mat("Bone", Color.white));

        BuildPlayer();
        SaveScene(scene, ScenePath, bone);
    }

    // ---------------------------------------------------------------- Patch version

    struct Tier
    {
        public string name; public float required, durability, minSize, maxSize, heightScale;
        public Color splash; public Material mat;
    }

    [MenuItem("PowerWash/Build Patch Scene (smoke and mirrors)")]
    public static void BuildPatchScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildEnvironment();

        // The bone is just a normal, clean object. A plain BoxCollider is fine here.
        var bone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bone.name = "Bone (cube placeholder)";
        bone.transform.position = new Vector3(0f, 1.6f, 0f);
        bone.transform.localScale = Vector3.one * 0.6f;
        bone.AddComponent<BoneTurntable>();
        SetMat(bone, TexturedMat("BoneTextured", TexFolder + "/BoneClean.png", Color.white, 0.3f));

        var group = new GameObject("Dirt Patches").transform;
        group.SetParent(bone.transform, false);

        Tier[] tiers =
        {
            new Tier { name = "loose", required = 0.3f, durability = 1.2f, minSize = 0.12f, maxSize = 0.20f, heightScale = 1.0f,
                       splash = new Color(0.45f, 0.33f, 0.20f), mat = TexturedMat("MudLoose", TexFolder + "/Mud.png", Color.white, 0.15f) },
            new Tier { name = "caked", required = 0.6f, durability = 2.0f, minSize = 0.10f, maxSize = 0.16f, heightScale = 1.3f,
                       splash = new Color(0.30f, 0.22f, 0.14f), mat = TexturedMat("MudCaked", TexFolder + "/Mud.png", new Color(0.6f, 0.58f, 0.55f), 0.1f) },
            new Tier { name = "crust", required = 1.0f, durability = 2.5f, minSize = 0.08f, maxSize = 0.13f, heightScale = 1.7f,
                       splash = new Color(0.16f, 0.14f, 0.12f), mat = TexturedMat("MudCrust", TexFolder + "/Mud.png", new Color(0.32f, 0.31f, 0.30f), 0.05f) },
        };

        Mesh[] blobs = GetBlobMeshes(6);
        Random.InitState(1234);
        Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        int index = 0;
        foreach (Vector3 face in faces)
        {
            for (int k = 0; k < 5; k++)
            {
                float roll = Random.value;
                Tier tier = roll < 0.6f ? tiers[0] : roll < 0.85f ? tiers[1] : tiers[2];
                CreatePatch(bone.transform, group, face, blobs[Random.Range(0, blobs.Length)], tier, index++);
            }
        }

        new GameObject("PatchManager").AddComponent<PatchManager>();

        BuildPlayer();
        SaveScene(scene, PatchScenePath, bone);
    }

    static void CreatePatch(Transform bone, Transform group, Vector3 localNormal, Mesh mesh, Tier tier, int index)
    {
        float diameter = Random.Range(tier.minSize, tier.maxSize);

        // Random spot on this face of the cube, kept away from the edges so it doesn't overhang.
        float radiusLocal = diameter * 0.5f / bone.lossyScale.x;
        float margin = Mathf.Max(0f, 0.5f - radiusLocal - 0.02f);
        Vector3 t1 = Vector3.Cross(localNormal, Mathf.Abs(localNormal.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 t2 = Vector3.Cross(localNormal, t1);
        Vector3 local = localNormal * 0.5f + t1 * Random.Range(-margin, margin) + t2 * Random.Range(-margin, margin);

        Vector3 worldNormal = bone.TransformDirection(localNormal).normalized;
        Vector3 worldPos = bone.TransformPoint(local) + worldNormal * 0.002f;

        var go = new GameObject($"DirtPatch_{index:00} ({tier.name})");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = tier.mat;
        go.transform.SetPositionAndRotation(worldPos,
            Quaternion.FromToRotation(Vector3.up, worldNormal) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up));
        go.transform.localScale = new Vector3(diameter, diameter * tier.heightScale, diameter);
        go.transform.SetParent(group, true);
        go.AddComponent<BoxCollider>();   // sizes itself to the mesh

        var patch = go.AddComponent<DirtPatch>();
        patch.requiredStrength = tier.required;
        patch.durability = tier.durability;
        patch.colour = tier.splash;
    }

    /// <summary>A few lumpy "cowpat" meshes, saved as assets so they show in the editor and can be reused.</summary>
    static Mesh[] GetBlobMeshes(int count)
    {
        EnsureFolder(MeshFolder);
        var meshes = new Mesh[count];
        for (int i = 0; i < count; i++)
        {
            string path = $"{MeshFolder}/DirtBlob_{i}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = MakeBlob(i + 1);
                AssetDatabase.CreateAsset(mesh, path);
            }
            meshes[i] = mesh;
        }
        AssetDatabase.SaveAssets();
        return meshes;
    }

    /// <summary>Flat lumpy disc, 1 unit across, lying on the XZ plane with its bump pointing up (+Y).</summary>
    static Mesh MakeBlob(int seed)
    {
        var rnd = new System.Random(seed);
        const int segments = 24, rings = 4;
        const float height = 0.15f;
        float p0 = (float)(rnd.NextDouble() * Mathf.PI * 2), p1 = (float)(rnd.NextDouble() * Mathf.PI * 2), p2 = (float)(rnd.NextDouble() * Mathf.PI * 2);

        var verts = new List<Vector3> { new Vector3(0f, height, 0f) };
        for (int r = 1; r <= rings; r++)
        {
            float rr = (float)r / rings;
            for (int s = 0; s < segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float edge = 1f + 0.18f * Mathf.Sin(3 * a + p0) + 0.10f * Mathf.Sin(5 * a + p1) + 0.06f * Mathf.Sin(7 * a + p2);
                float rad = rr * edge * 0.5f;
                float y = r == rings ? 0f : height * (1f - rr * rr) * (0.85f + 0.3f * (float)rnd.NextDouble());
                verts.Add(new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad));
            }
        }

        var tris = new List<int>();
        for (int s = 0; s < segments; s++)                       // centre fan
            tris.AddRange(new[] { 0, 1 + (s + 1) % segments, 1 + s });
        for (int r = 0; r < rings - 1; r++)                      // rings
        {
            int inner = 1 + r * segments, outer = 1 + (r + 1) * segments;
            for (int s = 0; s < segments; s++)
            {
                int s1 = (s + 1) % segments;
                tris.AddRange(new[] { inner + s, inner + s1, outer + s });
                tris.AddRange(new[] { inner + s1, outer + s1, outer + s });
            }
        }

        var uvs = new List<Vector2>();
        foreach (var v in verts) uvs.Add(new Vector2(v.x + 0.5f, v.z + 0.5f));

        var mesh = new Mesh { name = $"DirtBlob_{seed}" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- Shared

    static void BuildEnvironment()
    {
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        SetMat(floor, Mat("Floor", new Color(0.35f, 0.37f, 0.40f)));

        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Pedestal";
        pedestal.transform.position = new Vector3(0f, 0.5f, 0f);
        pedestal.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
        SetMat(pedestal, Mat("Pedestal", new Color(0.55f, 0.55f, 0.58f)));
    }

    static void BuildPlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -2.2f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        var controller = player.AddComponent<PlayerController>();

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        controller.cameraPivot = camGo.transform;

        // Pressure-washer lance held in view
        var lance = new GameObject("Lance");
        lance.transform.SetParent(camGo.transform, false);
        lance.transform.localPosition = new Vector3(0.22f, -0.22f, 0.35f);

        Prim(PrimitiveType.Cylinder, "Pipe", lance.transform, new Vector3(0f, 0f, 0.25f),
             new Vector3(90f, 0f, 0f), new Vector3(0.025f, 0.25f, 0.025f), Mat("Metal", new Color(0.7f, 0.7f, 0.75f)));
        Prim(PrimitiveType.Cube, "Handle", lance.transform, new Vector3(0f, -0.07f, 0.02f),
             new Vector3(15f, 0f, 0f), new Vector3(0.05f, 0.14f, 0.06f), Mat("Handle", new Color(0.95f, 0.75f, 0.1f)));
        Prim(PrimitiveType.Cylinder, "Nozzle Head", lance.transform, new Vector3(0f, 0f, 0.52f),
             new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.03f, 0.04f), Mat("Nozzle", new Color(0.85f, 0.15f, 0.1f)));

        var tip = new GameObject("Tip");
        tip.transform.SetParent(lance.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.56f);

        var spray = camGo.AddComponent<SprayNozzle>();
        spray.aim = camGo.transform;
        spray.tip = tip.transform;

        player.AddComponent<PowerWashHUD>();
    }

    static void SaveScene(UnityEngine.SceneManagement.Scene scene, string path, GameObject select)
    {
        EditorSceneManager.SaveScene(scene, path);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        Selection.activeGameObject = select;
        Debug.Log("PowerWash scene built: " + path + ". Press Play.");
    }

    static void Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 rot, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // don't let the lance block the spray
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = rot;
        go.transform.localScale = scale;
        SetMat(go, mat);
    }

    static void SetMat(GameObject go, Material mat) => go.GetComponent<Renderer>().sharedMaterial = mat;

    static Material Mat(string name, Color colour)
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
        else mat.color = colour;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material TexturedMat(string name, string texturePath, Color tint, float smoothness)
    {
        var mat = Mat(name, tint);
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
        if (tex != null) mat.mainTexture = tex;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
