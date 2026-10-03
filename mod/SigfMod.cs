using System.Collections;
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;

public class SigfMod : MixMod
{
    public override void OnLoad() => G.StartLevel = "LevelTest#59 LabEndless2";

    public override void OnReady()
    {
        Hud.Create();
        G.FaceOpen();
        Mix.Every(0.25f, Gear.DressAll, "dress");
        Mix.Every(4f, () => { if (G.Enemies().Count < 5) G.SpawnEnemy(G.Ahead(11f) + Random.insideUnitSphere * 4f); }, "spawner");
        Mix.After(0.5f, () => { Mix.Say("MODERN WARFARE 2", 4f, Hud.Orange, 0.16f, 84); Mix.Say("TAKEOVER", 4f, Color.white, 0.26f, 54); }, "title");
        Mix.After(1f, () => Mix.Play(Mix.Sound("vo_intro.wav"), null, 1f), "vo");
        Mix.After(1f, () => Hud.I.Intro("\"Team SUPERHOT\"" + System.Environment.NewLine + "0300 HOURS" + System.Environment.NewLine + "Task Force 141, Sgt. Nobody" + System.Environment.NewLine + "The White Room, Somewhere"), "intro");
    }

    static void Wave(int n, float dist)
    {
        var right = new Vector3(G.Cam.transform.right.x, 0f, G.Cam.transform.right.z).normalized;
        for (int i = 0; i < n; i++)
            G.SpawnEnemy(G.Ahead(dist + Random.Range(-1f, 2f)) + right * ((i - (n - 1) / 2f) * 2.8f));
    }

    static void ShootClosest()
    {
        var e = G.Closest(G.Pos);
        if (e == null) return;
        Mix.Play(Mix.Sound("shot.wav"), null, 0.8f);
        G.Shatter(e);
    }

    /// <summary>Shoots the closest enemy until the streak reaches the goal (spawns one when the room is empty).</summary>
    static IEnumerator Until(int goal)
    {
        for (int tries = 0; Streaks.Streak < goal && tries < 12; tries++)
        {
            if (G.Closest(G.Pos) == null) { G.SpawnEnemy(G.Ahead(8f)); yield return Mix.Wait(1f); }
            ShootClosest();
            yield return Mix.Wait(0.7f);
        }
    }

    public override IEnumerator Demo()
    {
        G.FaceOpen();
        Streaks.BaseTime = 0.55f;
        G.ForceTime(0.55f);
        Streaks.Quiet = true; G.KillAll(); yield return Mix.Wait(0.1f); Streaks.Quiet = false; Streaks.Streak = 0;
        yield return Mix.Wait(0.3f);
        Wave(3, 8f);
        Mix.Say("MODERN WARFARE 2", 4f, Hud.Orange, 0.16f, 84);
        Mix.Say("TAKEOVER", 4f, Color.white, 0.26f, 54);
        yield return Mix.Wait(2.6f);
        // 1. the skull soldiers up close, in bullet time
        G.SpawnEnemy(G.Ahead(4.2f));
        G.ForceTime(0.08f);
        yield return Mix.Wait(1.8f);
        // 2. two kills in a row: hitmarkers, XP, gear flies off, the care package is called in
        G.ForceTime(0.4f); Wave(2, 9f);
        yield return Until(2);
        yield return Mix.Wait(6f);
        // 3. predator missile at 4 kills
        G.ForceTime(0.55f); Wave(3, 9f);
        yield return Mix.Wait(1f);
        yield return Until(4);
        yield return Mix.Wait(5.2f);
        // 4. harrier at 6 kills
        Wave(3, 9f);
        yield return Mix.Wait(1f);
        yield return Until(6);
        yield return Mix.Wait(6f);
        // 5. the nuke at 8 kills
        Wave(4, 9f);
        yield return Mix.Wait(1f);
        yield return Until(8);
        yield return Mix.Wait(6.5f);
        // 6. the war goes on: a fresh streak starts
        for (int round = 0; round < 3; round++)
        {
            Wave(3, 8f);
            yield return Mix.Wait(1.6f);
            yield return Until(Streaks.Streak + 3);
            yield return Mix.Wait(2f);
        }
        Streaks.BaseTime = -1f; G.Release();
    }
}
