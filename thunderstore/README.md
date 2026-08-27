# PEAK Item Tooltip Mod ⋆˙⟡

<img width="1200" height="400" alt="banner" src="https://github.com/user-attachments/assets/6014513a-6621-4d2e-ada3-c6874864dd7b" />

### ✨ a PEAK mod that displays an item tooltip when holding/hovering an item so you can directly read its type, description and status effects without needing to constantly search it up whenever you see something new
***
### ⚙️ installation
1. this mod requires the PEAK `BepInEx` framework which you can find [here](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/ "thunderstore").
2. after downloading it, extract and drag the `BepInExPack_PEAK` folder into your PEAK directory (_commonly found in `Steam\steamapps\common\PEAK`_) alongside the game's executable file (`.exe`). run the game once after you're done
3. drag `peak-item-tooltip.dll` into the newly created `plugins` folder in your `BepInEx` directory and you're done!  
***
### 👉🏻 features
- read an item's type, description and status effects when you hold or hover an item. simplified chinese translations are available thanks to **@Gintoki000** 🎉
  
  <img width="1200" height="400" alt="item tooltip previews" src="https://github.com/user-attachments/assets/13f15c85-2d22-4575-9b38-78e6b007a72d" />

  ⚠️ **note**: by default, the mod's language is set to `Auto` which follows your game's current language. subsequent changes to the language both in-game and the config file will require you to restart your game even if `HotReload` is on  
***
### 🛠️ configuration
- this mod allows for several customisable configuration options housed in `peak-item-tooltip.cfg`, _which should be in `BepInEx/config` after running the mod for the first time_
    | config option •ᴗ•     | description                                                                                                                               |
  |-----------------------|-------------------------------------------------------------------------------------------------------------------------------------------|
  | `Language`            | `Auto` by default (follows the game's current language); item tooltip display language                                                    |
  | `Enabled`             | master toggle for the item tooltip                                                                                                        |
  | `HotReload`           | `true` by default; allows changes to config and description files to be applied live. changing this option itself requires you to restart |
  | `ShowWhileHolding`    | `true` by default; displays the item tooltip when holding an item. if disabled, it will **only** show for items that you are looking at   |
  | `OffsetX`             | horizontal offset from the screen centre                                                                                                  |
  | `OffsetY`             | vertical offset from the screen centre                                                                                                    |
  | `Scale`               | item tooltip scale multiplier                                                                                                             |
  | `BackgroundOpacity`   | background panel opacity                                                                                                                  |
  | `BorderColour`        | colour of the outline around the item tooltip as a hex string (e.g., #FFFFFF); invalid hex strings will fall back to white                |
  | `BorderOpacity`       | opacity of the outline around the item tooltip                                                                                            |
  | `ShowIcon`            | displays the item's icon                                                                                                                  |
  | `ShowName`            | displays the item's name                                                                                                                  |
  | `ShowType`            | displays the item's type (e.g., Mystical, Consumable)                                                                                     |
  | `ShowDescription`     | displays the item's description                                                                                                           |
  | `ShowModifiers`       | displays the item's status effects                                                                                                        |
  | `IconSize`            | icon width/height in reference pixels (1920x1080)                                                                                         |
  | `NameFontSize`        | item name font size                                                                                                                       |
  | `TypeFontSize`        | item type font size                                                                                                                       |
  | `DescriptionFontSize` | item description font size                                                                                                                |
  | `ModifierFontSize`    | item status effect font size                                                                                                              |
