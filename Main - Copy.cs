using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.AddressableAssets;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

[assembly: MelonInfo(typeof(MissingCraftingIconFix.Main), "MissingCraftingIconFix", "1.4.0", "Custom")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace MissingCraftingIconFix
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("MissingCraftingIconFix initialized.");
        }
    }

    internal static class IconResolver
    {
        private static readonly Dictionary<string, Texture2D> iconCache = new();
        private static Dictionary<string, Texture2D> allLoadedTextures;

        public static Texture2D ResolveIconForBlueprint(BlueprintData bpi)
        {
            // 1. If the blueprint crafts an existing GearItem (e.g., ArrowMod recipes), ask the game for that item's real icon
            if (bpi.m_CraftedResultGear != null)
            {
                try
                {
                    Texture2D existingGearIcon = Utils.GetInventoryIconTexture(bpi.m_CraftedResultGear);
                    if (existingGearIcon != null && existingGearIcon != Texture2D.whiteTexture)
                    {
                        return existingGearIcon;
                    }
                }
                catch { }
            }

            // Determine the cleanest item name from Gear, Decoration, or Blueprint name
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

            // Build lookup table of all loaded textures in memory (including ModComponent bundles)
            if (allLoadedTextures == null)
            {
                allLoadedTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
                foreach (Texture2D tex in Resources.FindObjectsOfTypeAll<Texture2D>())
                {
                    if (tex != null && !string.IsNullOrEmpty(tex.name))
                    {
                        allLoadedTextures[tex.name] = tex;
                    }
                }
            }

            // 2. Check ModComponent double-underscore & single-underscore naming conventions
            string[] candidates =
            {
                "ico_GearItem__" + cleanName,
                "ico_CraftItem__" + cleanName,
                "ico_GearItem_" + cleanName,
                "ico_CraftItem_" + cleanName,
                "ico_" + cleanName,
                "GEAR_" + cleanName,
                cleanName
            };

            foreach (string key in candidates)
            {
                if (allLoadedTextures.TryGetValue(key, out Texture2D found) && found != null)
                {
                    iconCache[cleanName] = found;
                    return found;
                }
            }

            // 3. Extract the 2D diffuse texture directly from the item's 3D MeshRenderer material
            GameObject targetObj = bpi.m_CraftedResultGear != null
                ? bpi.m_CraftedResultGear.gameObject
                : (bpi.m_CraftedResultDecoration != null ? bpi.m_CraftedResultDecoration.gameObject : null);

            if (targetObj != null)
            {
                Texture2D meshTex = ExtractMeshTexture(targetObj);
                if (meshTex != null)
                {
                    iconCache[cleanName] = meshTex;
                    return meshTex;
                }
            }

            // 4. Final fallback: unique color-coded badge if the item has no 2D texture anywhere
            Texture2D procedural = BuildProceduralIcon(cleanName);
            iconCache[cleanName] = procedural;
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
                    if (t2d != null && t2d != Texture2D.whiteTexture)
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
    internal static class BlueprintGetCraftingIconPatch
    {
        internal static bool Prefix(BlueprintData __instance, ref Texture2D __result)
        {
            if (__instance == null) return true;

            if (__instance.m_CraftingIcon != null && __instance.m_CraftingIcon.RuntimeKeyIsValid())
            {
                return true;
            }

            __result = IconResolver.ResolveIconForBlueprint(__instance);
            return false;
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
                __result = Texture2D.whiteTexture;
                return false;
            }

            return true;
        }
    }
}