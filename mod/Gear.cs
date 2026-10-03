// Dresses every crystal man as a Task Force soldier: skull balaclava, helmet with goggles, plate carrier and pouches.
// The gear is real 3D (primitives with the generated skull texture) glued to the enemy's bones; on death it flies off.
using System.Collections.Generic;
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;

public static class Gear
{
    public static readonly Color Olive = new Color(0.22f, 0.26f, 0.15f);
    public static readonly Color Tan = new Color(0.55f, 0.45f, 0.28f);
    public static readonly Color Black = new Color(0.04f, 0.04f, 0.045f);
    const string Tag = "SigfGear";
    static Mesh maskMesh;
    static Shader unlit;
    static Quaternion front = Quaternion.identity;

    public static Vector3 FrontOf(PejAiController e)
    {
        var h = Bone(e.transform, "Head"); var eyes = Bone(e.transform, "VirtualEyes");
        var d = h != null && eyes != null ? eyes.position - h.position : e.transform.forward; d.y = 0f;
        return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
    }

    public static void DressAll()
    {
        foreach (var e in Object.FindObjectsOfType<PejAiController>())
            if (e != null && !e.IsDead && e.transform.Find(Tag) == null) Dress(e);
    }

    static Transform Bone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // A piece of gear: world-sized, placed relative to the enemy root's axes, glued to a bone.
    static GameObject Piece(PrimitiveType type, Transform bone, Transform root, Vector3 offset, Vector3 size, Color color, Texture2D tex = null, Quaternion? rotOffset = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = "Gear";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = bone.position + front * offset;
        go.transform.rotation = front * (rotOffset ?? Quaternion.identity);
        var parent = root.Find(Tag);
        go.transform.SetParent(bone, true);
        var ls = bone.lossyScale;
        go.transform.localScale = new Vector3(size.x / ls.x, size.y / ls.y, size.z / ls.z);
        Mix.Paint(go, color, 0f, tex);
        Unlit(go, color, tex);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        return go;
    }

    /// <summary>Flat unlit look (SUPERHOT's lighting turns the Standard shader black). alpha=true: translucent.</summary>
    public static void Flat(GameObject go, Color color, Texture2D tex = null, bool alpha = false)
    {
        var sh = Shader.Find(alpha ? "Sprites/Default" : tex != null ? "Unlit/Texture" : "Unlit/Color");
        if (sh == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var m = new Material(sh); m.color = color; if (tex != null) m.mainTexture = tex; r.material = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static void Unlit(GameObject go, Color color, Texture2D tex)
    {
        if (unlit == null) { unlit = Shader.Find(tex != null ? "Unlit/Texture" : "Unlit/Color"); if (unlit == null) { Mix.Warn("no unlit shader"); return; } }
        var sh = Shader.Find(tex != null ? "Unlit/Texture" : "Unlit/Color");
        if (sh == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var m = new Material(sh); m.color = color; if (tex != null) m.mainTexture = tex; r.material = m;
        }
    }

    static Mesh MaskMesh()
    {
        if (maskMesh != null) return maskMesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        maskMesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
        Object.Destroy(tmp);
        var v = maskMesh.vertices; var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
            uv[i] = v[i].z > -0.05f ? new Vector2(v[i].x + 0.5f, v[i].y * 0.5f + 0.5f) : new Vector2(0.01f, 0.01f);
        maskMesh.uv = uv;
        return maskMesh;
    }

    static void Dress(PejAiController e)
    {
        var root = e.transform;
        var head = Bone(root, "Head"); var chest = Bone(root, "Chest");
        if (head == null || chest == null) return;
        var eyes = Bone(root, "VirtualEyes");
        var fw = eyes != null ? eyes.position - head.position : root.forward; fw.y = 0f;
        front = fw.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fw.normalized) : root.rotation;
        var marker = new GameObject(Tag); marker.transform.SetParent(root, false);
        var skull = Mix.Texture("skull.png");

        // the skull balaclava: an ellipsoid whose front carries the skull print
        var mask = Piece(PrimitiveType.Sphere, head, root, new Vector3(0f, 0.11f, 0.04f), new Vector3(0.34f, 0.41f, 0.37f), Color.white, skull);
        mask.GetComponent<MeshFilter>().sharedMesh = MaskMesh();
        mask.name = "GearMask";
        // helmet shell, brim and goggles
        Piece(PrimitiveType.Sphere, head, root, new Vector3(0f, 0.24f, 0.0f), new Vector3(0.39f, 0.24f, 0.42f), Olive).name = "GearHelmet";
        Piece(PrimitiveType.Cube, head, root, new Vector3(0f, 0.2f, 0.2f), new Vector3(0.36f, 0.03f, 0.09f), Olive).name = "GearBrim";
        var gog = Piece(PrimitiveType.Cube, head, root, new Vector3(0f, 0.2f, 0.2f), new Vector3(0.33f, 0.055f, 0.035f), new Color(0.9f, 0.55f, 0.1f));
        gog.name = "GearGoggles";
        // plate carrier and pouches
        Piece(PrimitiveType.Cube, chest, root, new Vector3(0f, 0.0f, 0.04f), new Vector3(0.36f, 0.36f, 0.27f), Olive).name = "GearVest";
        for (int i = -1; i <= 1; i += 2)
            Piece(PrimitiveType.Cube, chest, root, new Vector3(0.09f * i, -0.08f, 0.18f), new Vector3(0.1f, 0.11f, 0.05f), Tan).name = "GearPouch";
        Piece(PrimitiveType.Cube, chest, root, new Vector3(0f, 0.18f, 0.03f), new Vector3(0.13f, 0.05f, 0.25f), Black).name = "GearStrap";
    }

    /// <summary>Called when an enemy dies: helmet, mask and vest fly off as real physics props.</summary>
    public static void Eject(PejAiController e)
    {
        var bones = new List<Transform>();
        foreach (var t in e.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Gear") && t.name != Tag) bones.Add(t);
        foreach (var t in bones)
        {
            t.SetParent(null, true);
            var c = t.gameObject.AddComponent<BoxCollider>();
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.velocity = (Random.onUnitSphere + Vector3.up * 1.2f) * Random.Range(2.5f, 5.5f);
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            Object.Destroy(t.gameObject, 4f);
        }
    }
}

[HarmonyPatch(typeof(PejAiController), nameof(PejAiController.Kill))]
static class GearOnKill
{
    static void Prefix(PejAiController __instance)
    {
        if (__instance == null || __instance.IsDead) return;
        try { Gear.Eject(__instance); } catch (System.Exception ex) { Mix.Error("GearOnKill", ex); }
    }
}
