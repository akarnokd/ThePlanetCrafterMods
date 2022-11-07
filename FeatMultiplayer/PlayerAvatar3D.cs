using UnityEngine;
using System.IO;

public class PlayerAvatar3D {
    static string armName = "A6_Arm";
    static string bodyName = "A6_Body";
    static string bootName = "A6_Boot";
    static string helmetName = "A6_Helmet";
    static string legsName = "A6_Legs";

    static string albedoName = "Armor6_White_AlbedoTransparency.png";
    static string metallicName = "Armor6_PaintMetall_MetallicSmoothness.png";
    static string normalName = "Armor6_Normal.png";
    static string occlusionName = "Armor6_Occlusion.png";
    static string emissionName = "Armor6_Orange_Emission.png";

    static float smoothnessValue = 0.75f;
    static float normalValue = 1;
    static float occlusionStrength = 1;
    static float emissiveStrength = 1.5f;

    //private void Start() {
    //    CreatePlayer("Player", Color.white, "C:/Unity Projects/PCrafter/Assets/");
    //}

    /// <summary>
    /// Create and return a player game object
    /// </summary>
    /// <param name="playerRootName">The name of the player object created ie.: "Player1"</param>
    /// <param name="playerColor">The emmissive color of this player ie.: Color.White(default), Color.blue, Color.green</param>
    /// <param name="path">The path where the files are stored ie.: "C:/myFolder/"</param>
    public static GameObject CreatePlayer(string playerRootName, Color playerColor, string path) {
        GameObject playerRoot = new GameObject(playerRootName);

        GameObject arm = CreateBodyPart(armName, playerRoot.transform);
        GameObject body = CreateBodyPart(bodyName, playerRoot.transform);
        GameObject boot = CreateBodyPart(bootName, playerRoot.transform);
        GameObject helmet = CreateBodyPart(helmetName, playerRoot.transform);
        GameObject legs = CreateBodyPart(legsName, playerRoot.transform);

        Material material = CreateMaterial(playerColor, path);

        CreateComponents(arm, LoadMesh(armName, path), material);
        CreateComponents(body, LoadMesh(bodyName, path), material);
        CreateComponents(boot, LoadMesh(bootName, path), material);
        CreateComponents(helmet, LoadMesh(helmetName, path), material);
        CreateComponents(legs, LoadMesh(legsName, path), material);

        return playerRoot;
    }

    private static GameObject CreateBodyPart(string childName, Transform parent) {
        GameObject childObj = new GameObject(childName);
        childObj.transform.parent = parent;
        return childObj;
    }

    private static void CreateComponents(GameObject go, Mesh mesh, Material mat) {
        SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = mat;
    }

    private static Mesh LoadMesh(string name, string path) {
        return new ObjImporter().ImportFile(path + name + ".obj");
    }

    private static Material CreateMaterial(Color playerColor, string path) {
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetTexture("_MainTex", LoadPNG(path + albedoName));
        
        mat.SetTexture("_MetallicGlossMap", LoadPNG(path + metallicName));
        mat.SetFloat("_GlossMapScale", smoothnessValue);
        mat.EnableKeyword("_METALLICGLOSSMAP");

        mat.SetTexture("_BumpMap", LoadPNG(path + normalName));
        mat.SetFloat("_BumpScale", normalValue);
        mat.EnableKeyword("_NORMALMAP");

        mat.SetTexture("_OcclusionMap", LoadPNG(path + occlusionName));
        mat.SetFloat("_OcclusionStrength", occlusionStrength);

        mat.SetTexture("_EmissionMap", LoadPNG(path + emissionName));
        mat.SetColor("_EmissionColor", playerColor * emissiveStrength);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.AnyEmissive;
        mat.EnableKeyword("_EMISSION");

        return mat;
    }

    private static Texture2D LoadPNG(string filePath) {
        Texture2D tex = null;
        byte[] fileData;
        
        if (File.Exists(filePath))     {
            fileData = File.ReadAllBytes(filePath);
            tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);
        }
        return tex;
    }
}