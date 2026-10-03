// SUPERHOT's own giant words ("1", "NEVER", "NEXT"...) would bury the Modern Warfare 2 banners: they are muted
// unless this mod asks for them.
using HarmonyLib;
using UnityEngine;

public static class Words { public static bool Allow; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuick), new[] { typeof(string), typeof(float) })]
static class MuteWords1 { static bool Prefix() => Words.Allow; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuick), new[] { typeof(string[]), typeof(bool), typeof(float) })]
static class MuteWords2 { static bool Prefix() => Words.Allow; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuickSilent))]
static class MuteWords3 { static bool Prefix() => Words.Allow; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuickSilentSmaller))]
static class MuteWords4 { static bool Prefix() => Words.Allow; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuickSmaller))]
static class MuteWords5 { static bool Prefix() => Words.Allow; }

[HarmonyPatch(typeof(TextManager), nameof(TextManager.DisplayQuickPulsating))]
static class MuteWords6 { static bool Prefix() => Words.Allow; }
