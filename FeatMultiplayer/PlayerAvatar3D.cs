using UnityEngine;
using System.IO;
using System;
using SpaceCraft;

public class PlayerAvatar3D {
    const string armName = "A6_Arm";
    const string bodyName = "A6_Body";
    const string bootName = "A6_Boot";
    const string helmetName = "A6_Helmet";
    const string legsName = "A6_Legs";

    internal static float emissiveStrength = 1.2f;

    /// <summary>
    /// Create and return a player game object
    /// </summary>
    /// <param name="playerRootName">The name of the player object created ie.: "Player1"</param>
    /// <param name="playerColor">The emmissive color of this player ie.: Color.White(default), Color.blue, Color.green</param>
    /// <param name="path">The path where the files are stored ie.: "C:/myFolder/"</param>
    public static GameObject CreatePlayer(string playerRootName, Color playerColor, string path) {
        GameObject playerRoot = new GameObject(playerRootName);

        //path = path + "\\";

        //GameObject arm = CreateBodyPart(armName, playerRoot.transform);
        //GameObject body = CreateBodyPart(bodyName, playerRoot.transform);
        //GameObject boot = CreateBodyPart(bootName, playerRoot.transform);
        //GameObject helmet = CreateBodyPart(helmetName, playerRoot.transform);
        //GameObject legs = CreateBodyPart(legsName, playerRoot.transform);

        //Material material = CreateMaterial(playerColor, path);

        //var armor = GameObject.Find("Armor6");
        ////for (int i = 0; i < armor.transform.childCount; i++) {
        ////    armor.transform.GetChild(i).gameObject.SetActive(true);
        ////}

        ////CreateComponents(arm, LoadMesh(armName, path), material);
        ////CreateComponents(body, LoadMesh(bodyName, path), material);
        ////CreateComponents(boot, LoadMesh(bootName, path), material);
        ////CreateComponents(helmet, LoadMesh(helmetName, path), material);
        ////CreateComponents(legs, LoadMesh(legsName, path), material);

        //Transform child;
        //for (int i = 0; i < armor.transform.childCount; i++) {
        //    child = armor.transform.GetChild(i);
        //    switch (child.name) {
        //        case armName:
        //            CreateComponents(arm, child.GetComponent<SkinnedMeshRenderer>().sharedMesh, material);
        //            break;
        //        case bodyName:
        //            child.gameObject.SetActive(true);
        //            CreateComponents(body, child.GetComponent<SkinnedMeshRenderer>().sharedMesh, material);
        //            child.gameObject.SetActive(false);
        //            break;
        //        case bootName:
        //            child.gameObject.SetActive(true);
        //            CreateComponents(boot, child.GetComponent<SkinnedMeshRenderer>().sharedMesh, material);
        //            child.gameObject.SetActive(false);
        //            break;
        //        case helmetName:
        //            child.gameObject.SetActive(true);
        //            CreateComponents(helmet, child.GetComponent<SkinnedMeshRenderer>().sharedMesh, material);
        //            child.gameObject.SetActive(false);
        //            break;
        //        case legsName:
        //            child.gameObject.SetActive(true);
        //            CreateComponents(legs, child.GetComponent<SkinnedMeshRenderer>().sharedMesh, material);
        //            child.gameObject.SetActive(false);
        //            break;
        //        default:
        //            break;
        //    }
        //}

        //Vector3 scale = playerRoot.transform.localScale;
        //playerRoot.transform.localScale = scale * 1.2f;

        //return playerRoot;

        GameObject armor = GameObject.Instantiate(GameObject.Find("Armor6"), playerRoot.transform);
        Transform child;
        for (int i = 0; i < armor.transform.childCount; i++) {
            child = armor.transform.GetChild(i);
            child.gameObject.SetActive(true);
            child.GetComponent<SkinnedMeshRenderer>().enabled = true;
        }
        //Vector3 scale = playerRoot.transform.localScale;
        playerRoot.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        return playerRoot;
    }

    private static GameObject CreateBodyPart(string childName, Transform parent) {
        GameObject childObj = new GameObject(childName);
        childObj.transform.parent = parent;
        //childObj.transform.Translate(0, -0.08f, 0, Space.Self);  // Temp fix for floating avatar
        return childObj;
    }

    private static void CreateComponents(GameObject go, Mesh mesh, Material mat) {
        SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = mat;
    }

    //private static Mesh LoadMesh(string name, string path) {
    //    return GameObject.Find(name).GetComponent<SkinnedMeshRenderer>().sharedMesh;
    //    //return new ObjImporter().ImportFile(path + name + ".obj");
    //}

    private static Material CreateMaterial(Color playerColor, string path) {
        Material mat = new Material(GameObject.Find("A6_Arm").GetComponent<SkinnedMeshRenderer>().sharedMaterial);
        mat.SetColor("_EmissionColor", playerColor * emissiveStrength);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.AnyEmissive;
        mat.EnableKeyword("_EMISSION");

        return mat;
    }
}