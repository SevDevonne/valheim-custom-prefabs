# Valerius Custom Prefabs

Adds custom building prefabs to Valheim and the Hammer build menu.

Originally developed for use in another project and released here in case the prefabs are useful to others.

## Installation (If you don't have a mod manager)

1. Install [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Install [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
3. Extract the dll into your Valheim `BepInEx/Core` folder in your relevant mod profile.

The DLL should be located at:

```text
BepInEx/plugins/ValeriusCustomPrefabs.dll
```

## Available Pieces

### Dungeon Scripting Pieces

| Name | Token | Based on |
|---|---|---|
| Dungeon Marker | `ValeriusDungeonMarker` | Ward (`guard_stone`) |

* Set the script_id custom value for use in constraining **Expand World Prefab** scripts to a set location opposed to affected the entire in-game world.


### Invisible Prefabs

| Name | Token | Based on |
|---|---|---|
| Invisible Boundary Wall | `ValeriusInvisibleWall` | 2×2 Wood Wall (`woodwall`) |
| Invisible Boundary Floor | `ValeriusInvisibleFloor` | 2×2 Wood Floor (`wood_floor`) |
| Invisible Boundary Wall 4×4 | `ValeriusInvisibleWall4x4` | 2×2 Wood Wall (`woodwall`), scaled |
| Invisible Boundary Floor 4×4 | `ValeriusInvisibleFloor4x4` | 2×2 Wood Floor (`wood_floor`), scaled |

* Use the same size,collision and durability as their vanilla counterparts.
* Can be placed together to create physical boundaries.
* Are **spawned at half health**.
* Are visible while below full health.
* Become invisible when at full health.
* Can be damaged and destroyed normally unless set invulnerable with **Infinity Hammer**.

Because the pieces are invisible when at full health, the  **PlanBuild** mod or **Infinity Hammer** mod is recommended for accurate placement. The **ESP** mod can also be used to locate placed pieces.


## Known Issues

* The invisible comes with the issue of being invisible.
* The dungeon marker causes TeleportWorld errors to spam console but does not impact gameplay. 

## Github

- This project is available on github at 'https://github.com/SevDevonne/valheim-custom-prefabs'


## Changelog

## 0.1.4
* Added Valerius Custom Prefabs to Github

## 0.1.3
* Added Invisible Boundary Floor 4x4.
* Added Invisible Boundary Wall 4x4.
* Added Dungeon Marker

## 0.1.2
* Invisible prefabs are now spawned at half health.
* Invisible prefabs become invisible when repaired to full health.
* Invisible prefabs are visible when below full health.

### 0.1.1

* Added Invisible Boundary Floor.

### 0.1.0

* Added Invisible Boundary Wall.


## AI Declaration 

- Artificial intelligence was used in the creation of this mod. All suggestions were reviewed by a questionably competent human. 

## Acknowledgement 

- Thanks to Raaka for the idea of having the invisible prefabs be visible when they are at less than full health.