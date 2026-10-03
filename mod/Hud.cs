// The Modern Warfare 2 heads-up display drawn over SUPERHOT: hitmarkers, XP feed, rank bar, killstreak rack,
// medal banners, the mission-intro typewriter and the full-screen flashes (nuke).
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Hud : MonoBehaviour
{
    public static Hud I;
    public static readonly Color Orange = new Color(1f, 0.62f, 0.12f);
    public static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
    static readonly string[] Ranks = { "Pvt.", "Pfc.", "Cpl.", "Sgt.", "SSgt.", "Lt.", "Capt.", "Maj.", "Col.", "Gen.", "PRESTIGE" };

    class Feed { public string text; public Color color; public float born; }
    readonly List<Feed> feed = new List<Feed>();
    Texture2D white;
    GUIStyle label, big;
    float hitAt = -9f; bool hitKill;
    public int Xp, Kills;
    float flash; Color flashColor; float flashFade = 1f;
    string introText = ""; float introAt = -1f; string introFull = "";
    float rankAt = -9f;

    public static void Create()
    {
        if (I != null) return;
        var go = new GameObject("MwHud");
        DontDestroyOnLoad(go);
        I = go.AddComponent<Hud>();
    }

    public int Rank => Mathf.Min(Ranks.Length - 1, Xp / 1500);

    public void Hit(bool kill) { hitAt = Time.unscaledTime; hitKill = kill; }

    public void Add(string text, Color color)
    {
        feed.Add(new Feed { text = text, color = color, born = Time.unscaledTime });
        if (feed.Count > 6) feed.RemoveAt(0);
    }

    public void GiveXp(int xp, string text, Color color)
    {
        int before = Rank;
        Xp += xp;
        Add("+" + xp + "  " + text, color);
        if (Rank > before)
        {
            rankAt = Time.unscaledTime;
            Mix.Play(Mix.Sound("rank.wav"), null, 0.8f);
            Mix.Say("RANK UP: " + Ranks[Rank], 3f, Gold, 0.36f, 54);
        }
    }

    float nukeAt = -99f;
    public void NukeFire() { nukeAt = Time.unscaledTime; }
    Color tint; string tintText = ""; float tintUntil;
    public void SetTint(Color c, string text, float seconds) { tint = c; tintText = text; tintUntil = Time.unscaledTime + seconds; }

    public void Flash(Color c, float fade = 1f) { flash = 1f; flashColor = c; flashFade = fade; }

    public void Intro(string text) { introFull = text; introAt = Time.unscaledTime; }

    void Update()
    {
        if (flash > 0f) flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime / flashFade);
        if (introAt > 0f)
        {
            int n = Mathf.Min(introFull.Length, (int)((Time.unscaledTime - introAt) * 38f));
            introText = introFull.Substring(0, n);
            if (Time.unscaledTime - introAt > introFull.Length / 38f + 5f) { introAt = -1f; introText = ""; }
        }
    }

    void Box(Rect r, Color c)
    {
        var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = p;
    }

    void Text(Rect r, string s, Color c, GUIStyle st)
    {
        var p = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.85f * c.a);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
        GUI.color = c; GUI.Label(r, s, st); GUI.color = p;
    }

    void OnGUI()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            big = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        }
        float k = Screen.height / 1080f, W = Screen.width, H = Screen.height, now = Time.unscaledTime;
        float cx = W / 2f, cy = H / 2f;

        // hitmarker: four white ticks around the crosshair (red and bigger on a kill)
        float ha = 1f - (now - hitAt) / 0.35f;
        if (ha > 0f)
        {
            var c = hitKill ? new Color(1f, 0.15f, 0.1f, ha) : new Color(1f, 1f, 1f, ha);
            float inner = (hitKill ? 14f : 10f) * k * (1f + (1f - ha) * 0.4f), len = (hitKill ? 20f : 13f) * k, th = 4f * k;
            var prev = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                GUI.matrix = prev;
                GUIUtility.RotateAroundPivot(45f + 90f * i, new Vector2(cx, cy));
                Box(new Rect(cx + inner, cy - th / 2f, len, th), c);
            }
            GUI.matrix = prev;
        }

        // XP feed under the crosshair
        label.fontSize = Mathf.RoundToInt(38 * k);
        for (int i = feed.Count - 1; i >= 0; i--)
        {
            float a = Mathf.Clamp01(3.2f - (now - feed[i].born));
            if (a <= 0f) { feed.RemoveAt(i); continue; }
        }
        for (int i = 0; i < feed.Count; i++)
        {
            float a = Mathf.Clamp01(3.2f - (now - feed[i].born));
            var c = feed[i].color; c.a = a;
            float rise = Mathf.Min(1f, (now - feed[i].born) * 6f);
            Text(new Rect(cx + 70 * k, cy + (60 + i * 44) * k + (1f - rise) * 10, 700 * k, 40 * k), feed[i].text, c, label);
        }

        // rank + XP bar, top left
        int rk = Rank; float into = (Xp % 1500) / 1500f;
        label.fontSize = Mathf.RoundToInt(34 * k);
        Text(new Rect(40 * k, 30 * k, 600 * k, 44 * k), Ranks[rk] + "  SUPERHOT", Gold, label);
        Box(new Rect(42 * k, 78 * k, 260 * k, 10 * k), new Color(0, 0, 0, 0.55f));
        Box(new Rect(42 * k, 78 * k, 260 * k * (rk >= Ranks.Length - 1 ? 1f : into), 10 * k), Orange);
        label.fontSize = Mathf.RoundToInt(22 * k);
        Text(new Rect(42 * k, 92 * k, 600 * k, 30 * k), Xp + " XP     KILLS " + Kills, new Color(1, 1, 1, 0.9f), label);

        // killstreak rack, bottom right
        Streaks.DrawRack(this, k, W, H);

        if (Time.unscaledTime < tintUntil)
        {
            Box(new Rect(0, 0, W, H), tint);
            if (tintText.Length > 0)
            {
                big.fontSize = Mathf.RoundToInt(40 * k);
                Text(new Rect(0, 60 * k, W, 60 * k), tintText, new Color(0.6f, 1f, 0.7f, 1f), big);
                for (float y = 0; y < H; y += 6 * k) Box(new Rect(0, y, W, 1.5f * k), new Color(0, 0, 0, 0.18f));
            }
        }
        float nt = now - nukeAt;
        if (nt >= 0f && nt < 4f)
        {
            var fire = Mix.Texture("fire.png"); var prev = GUI.color;
            float a = Mathf.Clamp01((4f - nt) / 2.2f);
            for (int i = 0; i < 6; i++)
            {
                float sz = H * (0.55f + nt * 0.32f) * (1f - i * 0.07f);
                float dx = (i - 2.5f) * W * 0.17f, dy = Mathf.Sin(i * 2.1f) * H * 0.12f - nt * H * 0.03f;
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(cx + dx - sz / 2f, H * 0.58f + dy - sz / 2f, sz, sz), fire, ScaleMode.StretchToFill, true);
            }
            GUI.color = prev;
        }
        // flash overlay (nuke)
        if (flash > 0f) { var c = flashColor; c.a = Mathf.Clamp01(flash); Box(new Rect(0, 0, W, H), c); }

        // mission intro typewriter, bottom left
        if (introText.Length > 0)
        {
            label.fontSize = Mathf.RoundToInt(30 * k);
            Text(new Rect(60 * k, H - 330 * k, 1100 * k, 300 * k), introText, new Color(0.92f, 0.95f, 0.88f, 1f), new GUIStyle(label) { alignment = TextAnchor.LowerLeft, fontSize = label.fontSize, wordWrap = true });
        }
    }

    public void DrawSlot(Rect r, string title, string sub, float progress, bool ready, Color c)
    {
        Box(r, new Color(0, 0, 0, 0.6f));
        Box(new Rect(r.x, r.y, 6, r.height), ready ? c : new Color(c.r, c.g, c.b, 0.4f));
        Box(new Rect(r.x + 6, r.yMax - 6, (r.width - 6) * progress, 6), c);
        label.fontSize = Mathf.RoundToInt(r.height * 0.36f);
        Text(new Rect(r.x + 14, r.y + 2, r.width - 16, r.height * 0.55f), title, ready ? c : new Color(1, 1, 1, 0.8f), label);
        label.fontSize = Mathf.RoundToInt(r.height * 0.27f);
        Text(new Rect(r.x + 14, r.y + r.height * 0.5f, r.width - 16, r.height * 0.4f), sub, new Color(1, 1, 1, 0.65f), label);
    }
}
