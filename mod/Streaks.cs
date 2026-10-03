// Killstreak rewards, the Modern Warfare 2 way: every kill feeds the streak and the rewards call themselves in.
//   2 kills: Care Package (the airdrop that crushes enemies, a classic)   4: Predator Missile
//   6: Harrier Strike (jet bombing run)                                     8: Tactical Nuke (the whole level goes white)
// All movement runs in SUPERHOT time, so strikes crawl while the player stands still and rush when they move.
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;

public static class Streaks
{
    public static int Streak;
    public static bool Striking;     // true while a reward kills: those kills give XP but do not feed the streak
    public static bool Nuking;
    public static bool Quiet;         // true while the demo clears the room: those deaths are not kills
    static float lastKill = -99f; static int combo;
    static bool firstBlood;
    static readonly string[] Names = { "CARE PACKAGE", "PREDATOR MISSILE", "HARRIER STRIKE", "TACTICAL NUKE" };
    static readonly int[] At = { 2, 4, 6, 8 };
    static readonly Color[] Cols = { new Color(0.55f, 0.85f, 0.35f), new Color(0.3f, 0.9f, 0.9f), new Color(0.95f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.2f) };
    public static int NukeAt { get => At[3]; set { At[3] = value; } }

    /// <summary>Game time, but never fully frozen: strikes crawl in bullet time and rush when the player moves.</summary>
    static float Dt() => Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime * 0.12f);

    /// <summary>The time scale the mod returns to after a slow-motion beat (-1 = the game's own, set by the demo otherwise).</summary>
    public static float BaseTime = -1f;
    static int slowToken;
    public static void SlowMo(float scale, float seconds)
    {
        int tok = ++slowToken;
        G.ForceTime(scale);
        Mix.After(seconds, () => { if (tok == slowToken) { if (BaseTime < 0f) G.Release(); else G.ForceTime(BaseTime); } }, "slowmo");
    }

    static Vector3 CamRight => new Vector3(G.Cam.transform.right.x, 0f, G.Cam.transform.right.z).normalized;

    // ------------------------------------------------------------------ kills
    public static void OnKill(Vector3 pos)
    {
        var hud = Hud.I; if (hud == null || Quiet) return;
        Mix.Log("KILLDBG wall=" + System.DateTime.Now.ToString("HH:mm:ss.f") + " t=" + Time.unscaledTime.ToString("F1") + " striking=" + Striking + " streak=" + Streak);
        hud.Hit(true); hud.Kills++;
        Mix.Play(Mix.Sound("hit.wav"), null, 0.9f);
        Mix.Play(Mix.Sound("kill.wav"), null, 0.55f);
        Debris(pos + Vector3.up * 1.2f, new Color(1f, 0.3f, 0.2f), 14, 5f, 0.1f, 2f);
        float now = Time.unscaledTime;
        if (!firstBlood) { firstBlood = true; hud.GiveXp(100, "First Blood", Hud.Gold); Mix.Play(Mix.Sound("vo_effect.wav"), null, 1f); }
        if (Striking) { hud.GiveXp(100, "Streak kill", Hud.Orange); return; }
        hud.GiveXp(100, "Enemy killed", Color.white);
        combo = now - lastKill < 4f ? combo + 1 : 1; lastKill = now;
        if (combo == 2) hud.GiveXp(50, "Double Kill", Hud.Gold);
        else if (combo == 3) hud.GiveXp(100, "Triple Kill", Hud.Gold);
        else if (combo >= 4) hud.GiveXp(150, "Multi Kill", Hud.Gold);
        Streak++;
        for (int i = 0; i < At.Length; i++) if (Streak == At[i]) Award(i);
    }

    static void Award(int tier)
    {
        Mix.Play(Mix.Sound("streak.wav"), null, 0.9f);
        Mix.Say(Names[tier], 3f, Cols[tier], 0.16f, 78);
        Mix.Say(Streak + " KILL STREAK", 3f, Color.white, 0.24f, 38);
        Hud.I.Add(Names[tier] + " ready", Cols[tier]);
        string[] vo = { "vo_care.wav", "vo_predator.wav", "vo_harrier.wav", "vo_nuke.wav" };
        Mix.Play(Mix.Sound("radio.wav"), null, 0.6f);
        Mix.After(0.5f, () => Mix.Play(Mix.Sound(vo[tier]), null, 1f), "voice");
        Mix.After(1.4f, () => Run(tier), "award");
    }

    public static void Run(int tier)
    {
        switch (tier)
        {
            case 0: Mix.Run(CarePackage(), "CarePackage"); break;
            case 1: Mix.Run(Predator(), "Predator"); break;
            case 2: Mix.Run(Harrier(), "Harrier"); break;
            default: Mix.Run(Nuke(), "Nuke"); break;
        }
    }

    public static void DrawRack(Hud hud, float k, float W, float H)
    {
        for (int i = 0; i < At.Length; i++)
        {
            bool ready = Streak >= At[i];
            float prog = Mathf.Min(1f, Streak / (float)At[i]);
            var r = new Rect(W - 400 * k, H - (60 + (4 - i) * 92) * k, 370 * k, 80 * k);
            hud.DrawSlot(r, Names[i], Mathf.Min(Streak, At[i]) + " / " + At[i] + " kills", prog, ready, Cols[i]);
        }
    }

    // ------------------------------------------------------------------ helpers
    static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
        return p;
    }

    /// <summary>The enemy nearest the middle of the screen (so strikes land where viewers are looking).</summary>
    static PejAiController Target(float maxDist = 30f)
    {
        PejAiController best = null; float bestAngle = 999f;
        foreach (var e in G.Enemies())
        {
            var d = e.transform.position + Vector3.up - G.Cam.transform.position;
            if (d.magnitude > maxDist || d.magnitude < 3f) continue;
            float a = Vector3.Angle(G.Cam.transform.forward, d);
            if (a < bestAngle) { bestAngle = a; best = e; }
        }
        return best;
    }

    static GameObject Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 ls, Color c, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localScale = ls; g.transform.localRotation = rot ?? Quaternion.identity;
        Gear.Flat(g, c);
        return g;
    }

    /// <summary>Flat-colored cubes flung from pos (real physics, gone after a few seconds).</summary>
    public static void Debris(Vector3 pos, Color color, int count = 20, float speed = 8f, float size = 0.2f, float life = 2f)
    {
        for (int i = 0; i < count; i++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.layer = 2;
            c.transform.position = pos; c.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.3f);
            Gear.Flat(c, color);
            var rb = c.AddComponent<Rigidbody>();
            rb.velocity = (Random.onUnitSphere + Vector3.up * 0.6f) * speed * Random.Range(0.4f, 1f);
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            Object.Destroy(c, life * Random.Range(0.7f, 1.3f));
        }
    }

    class Billboard : MonoBehaviour
    {
        float roll = Random.Range(0f, 360f);
        void LateUpdate() { var c = G.Cam; if (c != null) transform.rotation = c.transform.rotation * Quaternion.Euler(0f, 0f, roll); }
    }

    static GameObject Sprite(string tex, Vector3 pos, float size, Color tint)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.position = pos; g.transform.localScale = Vector3.one * size;
        Gear.Flat(g, tint, Mix.Texture(tex), true);
        g.AddComponent<Billboard>();
        return g;
    }

    static void Smoke(Vector3 pos, Color c, float size, float life)
    {
        var g = Sprite("smoke.png", pos, size, c);
        Mix.Run(SmokeLife(g, c, size, life), "smoke");
    }

    static IEnumerator SmokeLife(GameObject g, Color c, float size, float life)
    {
        float t = 0f; var v = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.8f, 1.6f), Random.Range(-0.4f, 0.4f));
        var m = g.GetComponent<Renderer>().material;
        while (t < life && g != null)
        {
            float dt = Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime * 0.25f);
            t += dt;
            g.transform.position += v * dt;
            g.transform.localScale = Vector3.one * size * (1f + t / life * 0.9f);
            var cc = c; cc.a = c.a * Mathf.Clamp01(1.6f * (1f - t / life)); m.color = cc;
            yield return null;
        }
        if (g != null) Object.Destroy(g);
    }

    /// <summary>A fireball, flash, smoke, debris and a boom; shatters every enemy inside the radius.</summary>
    public static void Explode(Vector3 pos, float radius, bool kill = true, bool huge = false)
    {
        Mix.Play(Mix.Sound(huge ? "nukeboom.wav" : "boom.wav"), pos, 1f);
        Mix.Run(Fireball(Sprite("fire.png", pos + Vector3.up * 0.8f, 1f, Color.white), radius, 1f), "fireball");
        for (int i = 0; i < 3; i++)
            Mix.Run(Fireball(Sprite("fire.png", pos + Vector3.up * 0.6f + Random.insideUnitSphere * radius * 0.45f, 1f, Color.white), radius * 0.6f, 0.7f), "fireball");
        var l = Mix.Glow(pos + Vector3.up, new Color(1f, 0.6f, 0.2f), radius * 3f, 8f);
        Object.Destroy(l.gameObject, 0.7f);
        Debris(pos + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.1f), 26, radius * 1.3f, 0.2f, 2.5f);
        Debris(pos + Vector3.up * 0.3f, new Color(0.15f, 0.15f, 0.15f), 14, radius, 0.28f, 3f);
        for (int i = 0; i < 4; i++) Smoke(pos + Random.insideUnitSphere * radius * 0.4f + Vector3.up * 1.2f, new Color(1f, 1f, 1f, 0.8f), radius * 0.9f, Random.Range(1.8f, 2.8f));
        G.Shake(huge ? 2.5f : 1.2f);
        if (!kill) return;
        Striking = true;
        try { foreach (var e in G.Enemies()) if (Vector3.Distance(e.transform.position + Vector3.up, pos) < radius) G.Shatter(e); }
        finally { Striking = false; }
    }

    static IEnumerator Fireball(GameObject g, float radius, float life)
    {
        float t = 0f; var m = g.GetComponent<Renderer>().material;
        while (t < 0.75f * life && g != null)
        {
            t += Time.unscaledDeltaTime;
            float k = t / (0.75f * life);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            g.transform.localScale = Vector3.one * Mathf.Lerp(radius * 0.5f, radius * 2.6f, ease);
            m.color = new Color(1f, 1f, 1f, Mathf.Clamp01(2.2f * (1f - k)));
            yield return null;
        }
        if (g != null) Object.Destroy(g);
    }

    // ------------------------------------------------------------------ 2: care package
    static IEnumerator CarePackage()
    {
        var tg = Target();
        Vector3 land = tg != null ? tg.transform.position : Ground(G.Ahead(7f));
        Vector3 pos = land + Vector3.up * 11f + CamRight * 2.5f;
        var root = new GameObject("CarePackage");
        root.transform.position = pos;
        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Crate"; crate.transform.SetParent(root.transform, false);
        crate.transform.localScale = Vector3.one * 1.5f;
        Gear.Flat(crate, Color.white, Mix.Texture("crate.png"));
        var canopy = Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 5.2f, 0), new Vector3(6f, 2.6f, 6f), new Color(1f, 0.5f, 0.05f));
        Part(canopy.transform, PrimitiveType.Sphere, new Vector3(0, 0.12f, 0), new Vector3(0.55f, 0.85f, 1.02f), new Color(0.95f, 0.95f, 0.9f));
        var cords = new List<LineRenderer>();
        for (int i = 0; i < 4; i++)
        {
            var lg = new GameObject("cord"); lg.transform.SetParent(root.transform, false);
            var lr = lg.AddComponent<LineRenderer>(); lr.positionCount = 2; lr.widthMultiplier = 0.04f; lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("Sprites/Default")); lr.startColor = lr.endColor = new Color(0.1f, 0.1f, 0.1f);
            float a = i * 90f * Mathf.Deg2Rad;
            lr.SetPosition(0, new Vector3(Mathf.Cos(a) * 0.7f, 0.78f, Mathf.Sin(a) * 0.7f));
            lr.SetPosition(1, new Vector3(Mathf.Cos(a) * 2.7f, 4.6f, Mathf.Sin(a) * 2.7f));
            cords.Add(lr);
        }
        Mix.Play(Mix.Sound("jet.wav"), pos, 0.6f);
        float floor = land.y + 0.75f, smokeT = 0f;
        while (root.transform.position.y > floor)
        {
            float dt = Dt();
            var p = root.transform.position;
            var aim = tg != null && !tg.IsDead ? tg.transform.position : land;
            var flat = new Vector3(aim.x - p.x, 0f, aim.z - p.z);
            p += Vector3.ClampMagnitude(flat, 6f * dt);
            p.y -= 8f * dt;
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2f) * 4f);
            smokeT += Time.unscaledDeltaTime;
            if (smokeT > 0.12f) { smokeT = 0f; Smoke(p + Vector3.up * 0.7f, new Color(1f, 0.2f, 0.12f, 0.8f), 1.0f, 2.2f); }
            yield return null;
        }
        // touchdown: crush whoever stands under it
        var lp = root.transform.position; lp.y = floor; root.transform.position = lp; root.transform.rotation = Quaternion.identity;
        Mix.Play(Mix.Sound("crate.wav"), lp, 1f);
        Debris(lp, new Color(0.8f, 0.78f, 0.7f), 18, 4f, 0.18f, 2f);
        G.Shake(0.8f);
        bool crushed = false;
        Striking = true;
        try { foreach (var e in G.Enemies()) { var d = e.transform.position - lp; d.y = 0f; if (d.magnitude < 2.4f) { G.Shatter(e); crushed = true; } } }
        finally { Striking = false; }
        if (crushed)
        {
            Hud.I.GiveXp(250, "Emergency Airdrop", Hud.Gold);
            SlowMo(0.07f, 1.4f);
            Mix.Say("EMERGENCY AIRDROP!", 2.5f, Hud.Gold, 0.3f, 60);
        }
        // the chute collapses, the crate stays as solid cover, then pops open
        Object.Destroy(canopy);
        foreach (var c in cords) Object.Destroy(c.gameObject);
        var box = crate.AddComponent<BoxCollider>();
        float wait = 0f;
        while (wait < 6f) { wait += Time.unscaledDeltaTime; if (wait > 0.2f && Random.value < 0.08f) Smoke(lp + Vector3.up * 0.8f, new Color(1f, 0.2f, 0.12f, 0.7f), 0.9f, 2f); yield return null; }
        Debris(lp + Vector3.up * 0.6f, new Color(0.3f, 0.35f, 0.2f), 20, 6f, 0.2f, 2f);
        Debris(lp + Vector3.up * 0.6f, Hud.Gold, 16, 7f, 0.1f, 2f);
        Hud.I.GiveXp(200, "Care Package opened", Cols[0]);
        Object.Destroy(root);
    }

    // ------------------------------------------------------------------ 4: predator missile
    static IEnumerator Predator()
    {
        var tg = Target(40f);
        Vector3 tpos = tg != null ? tg.transform.position + Vector3.up : G.Ahead(9f, 0.5f);
        Vector3 pos = Vector3.Lerp(tpos, G.Cam.transform.position, 0.45f) + CamRight * -3.5f + Vector3.up * 8f;
        var root = new GameObject("Predator");
        root.transform.position = pos; root.transform.localScale = Vector3.one * 1.7f;
        var steel = new Color(0.62f, 0.64f, 0.66f);
        Part(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.38f, 1.4f, 0.38f), steel, Quaternion.Euler(90, 0, 0));
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 0, 1.35f), new Vector3(0.38f, 0.38f, 0.8f), new Color(0.85f, 0.2f, 0.15f));
        for (int i = 0; i < 4; i++)
            Part(root.transform, PrimitiveType.Cube, Quaternion.Euler(0, 0, i * 90) * new Vector3(0.3f, 0, -1.0f), new Vector3(0.02f, 0.5f, 0.55f), new Color(0.3f, 0.3f, 0.32f), Quaternion.Euler(0, 0, i * 90));
        var tr = root.AddComponent<TrailRenderer>();
        tr.material = new Material(Shader.Find("Sprites/Default"));
        tr.time = 0.9f; tr.widthMultiplier = 0.55f; tr.startColor = new Color(1f, 0.7f, 0.2f, 0.9f); tr.endColor = new Color(0.8f, 0.8f, 0.8f, 0f);
        var l = Mix.Glow(pos, new Color(1f, 0.6f, 0.2f), 7f, 4f, root.transform);
        Mix.Play(Mix.Sound("missile.wav"), pos, 0.9f);
        Hud.I.SetTint(new Color(0.1f, 0.35f, 0.15f, 0.22f), "PREDATOR MISSILE  //  ARMED", 4f);
        float life = 0f;
        while (life < 6f)
        {
            life += Dt();
            if (tg != null && !tg.IsDead) tpos = tg.transform.position + Vector3.up;
            var d = tpos - root.transform.position;
            if (d.magnitude < 1.2f) break;
            root.transform.rotation = Quaternion.LookRotation(d.normalized);
            root.transform.position += d.normalized * 34f * Dt();
            yield return null;
        }
        Hud.I.SetTint(Color.clear, "", 0f);
        var ep = root.transform.position;
        Object.Destroy(root, 1f); root.transform.GetChild(0).gameObject.SetActive(false);
        foreach (Transform c in root.transform) c.gameObject.SetActive(false);
        Destroy(l);
        Explode(ep, 8f);
        SlowMo(0.07f, 1.2f);
        Hud.I.Flash(new Color(1f, 0.8f, 0.5f), 0.4f);
        Hud.I.GiveXp(100, "Predator Missile", Cols[1]);
    }

    static void Destroy(Light l) { if (l != null) Object.Destroy(l.gameObject); }

    // ------------------------------------------------------------------ 6: harrier strike
    class Bomb : MonoBehaviour
    {
        public float born;
        void OnCollisionEnter(Collision c) { Boom(); }
        void Update() { if (Time.time - born > 3f || transform.position.y < -20f) Boom(); }
        void Boom() { if (this == null) return; var p = transform.position; Destroy(this); Object.Destroy(gameObject); Explode(p, 6.5f); }
    }

    static IEnumerator Harrier()
    {
        var tg = Target(40f);
        Vector3 c = tg != null ? tg.transform.position : Ground(G.Ahead(9f));
        Vector3 dir = CamRight;
        Vector3 pos = c - dir * 40f + Vector3.up * 5.2f;
        var root = new GameObject("Harrier");
        root.transform.position = pos; root.transform.rotation = Quaternion.LookRotation(dir); root.transform.localScale = Vector3.one * 1.3f;
        var grey = new Color(0.5f, 0.53f, 0.57f);
        Part(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(1.0f, 0.9f, 9f), grey);
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 0.1f, 4.9f), new Vector3(0.9f, 0.8f, 2.4f), grey);
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 0.55f, 1.9f), new Vector3(0.7f, 0.5f, 2.2f), new Color(0.1f, 0.18f, 0.3f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.05f, -0.5f), new Vector3(8.6f, 0.14f, 2.6f), new Color(0.42f, 0.45f, 0.49f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.9f, -3.9f), new Vector3(0.12f, 1.8f, 1.5f), grey);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.1f, -4.2f), new Vector3(3.4f, 0.1f, 1.2f), grey);
        var fire = Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 0, -4.7f), new Vector3(0.8f, 0.8f, 1.8f), new Color(1f, 0.6f, 0.15f));
        Mix.Glow(pos, new Color(1f, 0.6f, 0.2f), 9f, 3f, root.transform);
        Mix.Play(Mix.Sound("jet.wav"), c + Vector3.up * 5f, 1f);
        float bombT = 0f, speed = 30f; int dropped = 0;
        for (float life = 0f; life < 4.5f; )
        {
            float dt = Dt(); life += dt;
            root.transform.position += dir * speed * dt;
            float along = Vector3.Dot(root.transform.position - c, dir);
            bombT += dt;
            if (along > -12f && along < 12f && bombT > 0.14f && dropped < 8)
            {
                bombT = 0f; dropped++;
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.transform.position = root.transform.position + Vector3.down * 0.8f;
                b.transform.localScale = new Vector3(0.4f, 0.4f, 0.9f); b.transform.rotation = root.transform.rotation;
                Gear.Flat(b, new Color(0.25f, 0.27f, 0.2f));
                var rb = b.AddComponent<Rigidbody>(); rb.velocity = dir * speed * 0.6f;
                b.AddComponent<Bomb>().born = Time.time;
            }
            if (along > 45f) break;
            yield return null;
        }
        Object.Destroy(root);
        Hud.I.GiveXp(100, "Harrier Strike", Cols[2]);
    }

    // ------------------------------------------------------------------ 8: tactical nuke
    static IEnumerator Nuke()
    {
        Nuking = true;
        var hud = Hud.I;
        Mix.Play(Mix.Sound("siren.wav"), null, 0.8f);
        Words.Allow = true; G.Words("TACTICAL;NUKE"); Words.Allow = false;
        for (int i = 3; i >= 1; i--)
        {
            Mix.Say("NUKE INBOUND  " + i, 1.1f, new Color(1f, 0.25f, 0.2f), 0.4f, 84);
            hud.SetTint(new Color(1f, 0.1f, 0.05f, i % 2 == 0 ? 0.2f : 0.05f), "", 1f);
            yield return Mix.Wait(0.7f);
        }
        hud.SetTint(Color.clear, "", 0f);
        Mix.Play(Mix.Sound("nukeboom.wav"), null, 1f);
        hud.Flash(Color.white, 2.6f);
        G.Shake(3f);
        hud.NukeFire();
        yield return Mix.Wait(0.25f);
        // shockwave ring from the player
        var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(ring.GetComponent<Collider>());
        ring.transform.position = G.Pos; Gear.Flat(ring, new Color(1f, 0.9f, 0.7f, 0.5f), null, true);
        float t = 0f;
        Striking = true;
        var rmat = ring.GetComponent<Renderer>().material;
        while (t < 1.6f)
        {
            t += Time.unscaledDeltaTime;
            float r = t * 40f;
            ring.transform.localScale = Vector3.one * r * 2f;
            var cc = rmat.color; cc.a = 0.5f * (1f - t / 1.6f); rmat.color = cc;
            foreach (var e in G.Enemies())
                if (Vector3.Distance(e.transform.position, G.Pos) < r)
                {
                    Debris(e.transform.position + Vector3.up, Color.white, 10, 9f, 0.15f, 2f);
                    G.Shatter(e);
                }
            yield return null;
        }
        Striking = false;
        Object.Destroy(ring);
        Mix.Say("ENEMIES NUKED", 3.5f, Color.white, 0.25f, 88);
        hud.GiveXp(1000, "Tactical Nuke", Cols[3]);
        Streak = 0; combo = 0;
        Nuking = false;
    }
}

[HarmonyPatch(typeof(PejAiController), nameof(PejAiController.Kill))]
static class StreakOnKill
{
    static void Prefix(PejAiController __instance)
    {
        if (__instance == null || __instance.IsDead) return;
        try { Streaks.OnKill(__instance.transform.position); }
        catch (System.Exception ex) { Mix.Error("StreakOnKill", ex); }
    }
}
