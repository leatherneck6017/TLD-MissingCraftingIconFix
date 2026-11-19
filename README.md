# Missing Crafting Icon Fix

<img width="1920" height="1080" alt="MissingCraftingIconFix_Cover" src="https://github.com/user-attachments/assets/830ea021-af8a-41f7-8204-288133c67e77" />

A lightweight MelonLoader utility mod for **The Long Dark** that restores missing blueprint icons in the crafting menu and eliminates `NullReferenceException` console spam caused by older or unupdated mods.

## Overview

Recent updates to *The Long Dark* changed how the crafting UI loads icons via Unity Addressables (`TextureCache.GetAddressableTexture`). Many older C# mods and `.modcomponent` packs still use legacy icon setups, causing blank icon squares in the crafting list and heavy log spam while scrolling through blueprints.

**Missing Crafting Icon Fix** resolves these issues globally across your entire load order without touching your save files or requiring edits to individual mods.

## Features

* **Eliminates Console Spam:** Intercepts null or invalid `AssetReferenceTexture2D` calls in `TextureCache.GetAddressableTexture` before they throw `NullReferenceException` errors.
* **Recovers ModComponent Icons:** Automatically locates and restores `ico_GearItem__` and `ico_CraftItem__` textures bundled inside `.modcomponent` archives.
* **Preserves Stock Icons:** Leaves all vanilla icons and stock white UI silhouette sprites untouched.
* **Smart Fallbacks:** Extracts the 2D diffuse texture directly from an item's 3D `MeshRenderer` material when no dedicated icon exists—or generates a unique, color-coded badge if no texture is available anywhere.
* **Clean Diagnostics:** Logs a single yellow confirmation line in the MelonLoader console when a custom or fallback icon is recovered, without spamming on repeat views.

## Requirements

* [MelonLoader](https://melonloader.co/download.html) (install whatever version is currently compatible with TLD)

## Installation

1. Download the latest `MissingCraftingIconFix.dll` from the [Releases](../../releases) page.
2. Place it into your `TheLongDark\Mods\` folder.
3. Launch the game.

## Building from Source

1. Clone this repository.
2. Open `MissingCraftingIconFix.csproj` and update `<TLDPath>` to match your local *The Long Dark* installation directory:

3. Build the project in **Visual Studio** targeting **.NET 6.0**. The post-build event will automatically copy `MissingCraftingIconFix.dll` into your `Mods` folder.

## AI Disclaimer

AI tools were consulted during the development of this mod as part of my learning process for exploring *The Long Dark*'s IL2CPP APIs, Harmony patching, and troubleshooting. All code was tested and verified in-game.

## License

This project is open source and available under the [MIT License](LICENSE).
