using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.AddressableAssets;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

[assembly: MelonInfo(typeof(MissingCraftingIconFix.Main), "MissingCraftingIconFix", "1.0.0", "leatherneck6017")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace MissingCraftingIconFix
{
    public class Main : MelonMod
    {
        internal static MelonLogger.Instance Logger;

        public override void OnInitializeMelon()
        {
            Logger = LoggerInstance;
            Logger.Msg("MissingCraftingIconFix initialized.");
        }
    }

    internal static class IconResolver
    {
        private static readonly Dictionary<string, Texture2D> iconCache = new();
        private static readonly HashSet<string> loggedBlueprints = new(StringComparer.OrdinalIgnoreCase);

        private static void LogFixOnce(string blueprintName, string sourceDetail)
        {
            if (loggedBlueprints.Add(blueprintName))
            {
                Main.Logger?.Msg(ConsoleColor.Yellow, $"Fixed missing icon for '{blueprintName}' -> {sourceDetail}");
            }
        }

        public static Texture2D ResolveMissingIcon(BlueprintData bpi)
        {
            string bpName = !string.IsNullOrEmpty(bpi.name) ? bpi.name : "UnknownBlueprint";

            // 1. First, let ModComponent / GearItem supply its own icon
            if (bpi.m_CraftedResultGear != null)
            {
                try
                {
                    Texture2D modIcon = Utils.GetInventoryIconTexture(bpi.m_CraftedResultGear);
                    if (modIcon != null && modIcon != Texture2D.whiteTexture && modIcon.width > 16 && modIcon.height > 16)
                    {
                        // Log only when recovering a custom double-underscore ModComponent icon (like Wolfskin Scarf)
                        if (!string.IsNullOrEmpty(modIcon.name) && modIcon.name.Contains("__"))
                        {
                            LogFixOnce(bpName, $"Recovered ModComponent icon '{modIcon.name}'");
                        }
                        return modIcon;
                    }
                }
                catch { }
            }

            // 2. If ModComponent ALSO has no icon (like Wolfskin Scarf), extract 3D mesh texture or build a badge
            string rawName = bpi.m_CraftedResultGear != null
                ? bpi.m_CraftedResultGear.name
                : (bpi.m_CraftedResultDecoration != null ? bpi.m_CraftedResultDecoration.name : bpi.name);

            string cleanName = rawName
                .Replace("GEAR_", "")
                .Replace("BLUEPRINT_", "")
                .Replace("CraftItem_", "")
                .Replace("(Clone)", "")
                .Trim();

            if (iconCache.TryGetValue(cleanName, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            GameObject targetObj = bpi.m_CraftedResultGear != null
                ? bpi.m_CraftedResultGear.gameObject
                : (bpi.m_CraftedResultDecoration != null ? bpi.m_CraftedResultDecoration.gameObject : null);

            if (targetObj != null)
            {
                Texture2D meshTex = ExtractMeshTexture(targetObj);
                if (meshTex != null)
                {
                    iconCache[cleanName] = meshTex;
                    LogFixOnce(bpName, $"3D mesh material texture '{meshTex.name}'");
                    return meshTex;
                }
            }

            Texture2D procedural = BuildProceduralIcon(cleanName);
            iconCache[cleanName] = procedural;
            LogFixOnce(bpName, $"Generated fallback badge for '{cleanName}'");
            return procedural;
        }

        private static Texture2D ExtractMeshTexture(GameObject obj)
        {
            foreach (MeshRenderer mr in obj.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || mr.sharedMaterial == null) continue;
                Texture mainTex = mr.sharedMaterial.mainTexture;
                if (mainTex != null)
                {
                    Texture2D t2d = mainTex.TryCast<Texture2D>();
                    if (t2d != null && t2d != Texture2D.whiteTexture && t2d.width > 16)
                    {
                        return t2d;
                    }
                }
            }
            return null;
        }

        private static Texture2D BuildProceduralIcon(string name)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;

            uint hash = 2166136261;
            foreach (char c in name)
            {
                hash ^= c;
                hash *= 16777619;
            }
            float hue = (hash % 360) / 360f;

            Color bg = Color.HSVToRGB(hue, 0.45f, 0.30f);
            Color border = Color.HSVToRGB(hue, 0.60f, 0.78f);
            Color emblem = new Color(0.95f, 0.95f, 0.95f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < 4 || x >= size - 4 || y < 4 || y >= size - 4;
                    bool isCorner = (x < 16 && y < 16) || (x >= size - 16 && y >= size - 16);
                    tex.SetPixel(x, y, (isBorder || isCorner) ? border : bg);
                }
            }

            int center = size / 2;
            int radius = 26;
            for (int y = center - radius; y <= center + radius; y++)
            {
                for (int x = center - radius; x <= center + radius; x++)
                {
                    int dist = Math.Abs(x - center) + Math.Abs(y - center);
                    if (dist <= radius && dist >= radius - 6)
                    {
                        tex.SetPixel(x, y, emblem);
                    }
                }
            }

            tex.Apply();
            return tex;
        }
    }

    [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetCraftingIcon))]
    [HarmonyPriority(Priority.Last)]
    internal static class BlueprintGetCraftingIconPatch
    {
        internal static void Postfix(BlueprintData __instance, ref Texture2D __result)
        {
            if (__instance == null || __result != null) return;

            __result = IconResolver.ResolveMissingIcon(__instance);
        }
    }

    [HarmonyPatch(
        typeof(TextureCache),
        nameof(TextureCache.GetAddressableTexture),
        new Type[] { typeof(AssetReferenceTexture2D) }
    )]
    internal static class FixNullAddressableTexture
    {
        internal static bool Prefix(AssetReferenceTexture2D assetReference, ref Texture2D __result)
        {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid())
            {
                __result = null;
                return false;
            }

            return true;
        }
    }
}