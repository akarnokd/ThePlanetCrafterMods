# ThePlanetCrafterMods
BepInEx+Harmony mods for the Unity/Steam game The Planet Crafter

Steam: https://store.steampowered.com/app/1284190/The_Planet_Crafter/

Guide on dnSpy-based manual patches: https://steamcommunity.com/sharedfiles/filedetails/?id=2784319459

## Version <a href='https://github.com/akarnokd/ThePlanetCrafterMods/releases'><img src='https://img.shields.io/github/v/release/akarnokd/ThePlanetCrafterMods' alt='Latest GitHub Release Version'/></a>

:arrow_down_small: Download files from the releases: https://github.com/akarnokd/ThePlanetCrafterMods/releases/latest

## Supported Game Version: 0.4.012

Public releases are relatively infrequent (once in a few months). I'll do my best to keep my mods up-to-date in case something drastic changes inside the main game.

:warning: I have not tested my mods with the developer/preview/demo releases. They might work just fine or suddenly break.
I don't promise to fix my mods for these versions as they can get quite out-of-sync with the public release.

## Preparation

In order to use my or anyone other's mods, you need to install BepInEx first. The wiki has a guide for this:

https://planet-crafter.fandom.com/wiki/Modding#Using_Mods

When installing my mods, unzip the mod into the `BepInEx\Plugins` directory, including the folder inside the zip file.

You'll have a directory structure like this:

`BepInEx\Plugins\akarnokd - (UI) Pin Recipe to Screen\UIPinRecipe.dll`

Such organization avoids overwriting each others' files if they happen to be named the same as well as allows removing plugin files together 
by deleting the directory itself.


# Mods

| Cheats | UI/QoL | Other |
|---|---|---|
| [Asteroid Landing Position Override](#cheat-asteroid-landing-position-override)<br/>[Auto Consume Oxygen-Water-Food](#cheat-auto-consume-oxygen-water-food)<br/>[Auto Harvest](#cheat-auto-harvest)<br/>[Highlight Nearby Resources](#cheat-highlight-nearby-resources)<br/>[Inventory Capacity Override](#cheat-inventory-capacity-override)<br/>[Inventory Stacking](#cheat-inventory-stacking)<br/>[Machines Deposit Into Remote Containers](#cheat-machines-deposit-into-remote-containers)<br/>[Minimap](#cheat-minimap)<br/>[Photomode Hide Water](#cheat-photomode-hide-water)<br/>[Teleport to Nearest Minable](#cheat-teleport-to-nearest-minable) | [Craft Equipment Inplace](#ui-craft-equipment-inplace)<br/>[Customize Inventory Sort Order](#ui-customize-inventory-sort-order)<br/>[Don't Close Craft Window](#ui-dont-close-craft-window)<br/>[Hotbar](#ui-hotbar)<br/>[Magyar Fordítás](#ui-hungarian-translation)<br/>[Inventory Move Multiple Items](#ui-inventory-move-multiple-items)<br/>[Save When Quitting](#ui-save-when-quitting)<br/>[Show Consumable Counts](#ui-show-consumable-counts)<br/>[Show Container Content Info](#ui-show-container-content-info)<br/>[Show Grab N Mine Count](#ui-show-grab-n-mine-count)<br/>[Show MultiTool Mode](#ui-show-multitool-mode)<br/>[Show Player Inventory Counts](#ui-show-player-inventory-counts)<br/>[Show Player Tooltip Item Count](#ui-show-player-tooltip-item-count)<br/>[Show Rocket Counts](#ui-show-rocket-counts)<br/>[Pin Recipe to Screen](#ui-pin-recipe-to-screen)<br/>[Teleporter Scroll Targets](#ui-teleporter-scroll-targets) | [Fix International Loading](#fix-international-loading)<br/>[Reduce Save Size](#perf-reduce-save-size)<br/>[Support Mods with Load n Save](#lib-support-mods-with-load-n-save)<br/>[Save Auto Backup](#save-auto-backup)<br/>[Unbrick Save](#fix-unbrick-save) |


## (Cheat) Asteroid Landing Position Override

Fixes the asteroid landing position relative to the player by an offset.
This includes asteroids from rockets and random meteor showers.

Note that currently, this may fail if the landing position is determined by the game as invalid. Be in the clear open!

### Configuration

`akarnokd.theplanetcraftermods.cheatasteroidlandingposition.cfg`

```
[General]

## Relative position east-west (east is positive).
# Setting type: Int32
# Default value: 100
DeltaX = 100

## Relative position north-south (north is positive).
# Setting type: Int32
# Default value: 0
DeltaZ = 0
```

## (Cheat) Auto Consume Oxygen-Water-Food

When the Oxygen, Thirst and Health meters reach a critical level, this mod will automatically
consume an Oxygen bottle, Water bottle or any food item from the player's inventory.

Marked as cheat because it is expected the player does these manually.

### Configuration

`akarnokd.theplanetcraftermods.cheatautoconsume.cfg`

```
[General]

## The percentage for which below food/water/oxygen is consumed.
# Setting type: Int32
# Default value: 9
Threshold = 9
```

## (Cheat) Auto Harvest

Automatically harvest grown algae or food from their machines and deposit them into designated containers.

To deposit **Algae**, name any number of containers as `*Algae1Seed` (the `*` is mandatory).

To deposit food, use the following naming convention:

- **Eggplant** - `*Vegetable0Growable`
- **Squash** - `*Vegetable1Growable`
- **Beans** - `*Vegetable2Growable`
- **Mushroom** - `*Vegetable3Growable`

The naming is case insensitive.

### Configuration

`akarnokd.theplanetcraftermods.cheatautoharvest.cfg`

```
[General]

## Enable auto harvesting for algae.
# Setting type: Boolean
# Default value: true
HarvestAlgae = true

## Enable auto harvesting for food.
# Setting type: Boolean
# Default value: true
HarvestFood = true
```

## (Cheat) Photomode Hide Water

Press <kbd>Shift+F2</kbd> to toggle photomode and hide water as well.

This is marked as cheat because allows picking up objects near the water edge
where they can't be picked up normally.

### Configuration

None.

## (Cheat) Highlight Nearby Resources

Press <kbd>CTRL+F</kbd> or <kbd>X</kbd> to highlight nearby resources.
Press <kbd>Shift+X</kbd> to highlight and cycle forward through the set of resources configured.
Press <kbd>Ctrl+X</kbd> to highlight and cycle backward through the set of resources configured.

### Configuration

`akarnokd.theplanetcraftermods.cheatnearbyresourceshighlight.cfg`

```
[General]

## Specifies how far to look for resources.
# Setting type: Int32
# Default value: 30
Radius = 30

## Specifies how high the resource image to stretch.
# Setting type: Int32
# Default value: 1
StretchY = 1

## List of comma-separated resource ids to look for.
# Setting type: String
# Default value: Cobalt,Silicon,Iron,ice,Magnesium,Titanium,Aluminium,Uranim,Iridium,Alloy,Zeolite,Osmium,Sulfur,PulsarQuartz,PulsarShard
ResourceSet = Cobalt,Silicon,Iron,ice,Magnesium,Titanium,Aluminium,Uranim,Iridium,Alloy,Zeolite,Osmium,Sulfur,PulsarQuartz,PulsarShard

## Key used for cycling resources from the set
# Setting type: String
# Default value: X
CycleResourceKey = X

```

## (Cheat) Inventory Capacity Override

This is a very basic override of game inventories, existing and new alike. It tries to not break certain containers
with capacity 1 or 3. Note that the game is not really setup to handle large inventories that don't fit on the screen.
This mod makes no attempts at remedying this shortcoming.

### Configuration

`akarnokd.theplanetcraftermods.cheatinventorycapacity.cfg`

```
[General]

## The overridden default inventory capacity.
# Setting type: Int32
# Default value: 250
Capacity = 250

## Is this mod enabled?
# Setting type: Boolean
# Default value: true
Enabled = false
```

## (Cheat) Machines Deposit Into Remote Containers

For this mod to work, you have to rename your containers. For example,
to make machines deposit Iron, rename your container(s) to something that includes
`*Iron`. For Uranium, rename them to `*Uranim` (remark: this is a misspelling in the vanilla game which will probably never be fixed as it would break saves). Note the `*` in front of the identifiers.
Identifiers can be any case. 

You can combine multiple resources by mentioning them together: `*Iron *Cobalt`.

Typical identifiers are: 
-`Cobalt`,`Silicon`,`Iron`,`ice`,
`Magnesium`,`Titanium`,`Aluminium`,`Uranim`,
`Iridium`,`Alloy`,`Zeolite`,`Osmium`,
`Sulfur`, 

You can also make the water and methane extractors deposit remotely by naming containers:

- `*WaterBottle1`, `*OxygenCapsule1`, `*MethanCapsule1`

You can have as many containers as you like, but they will be filled in non-deterministically.
If there are no renamed containers or all renamed containers are full, the machines
will deposit the resource into their own container, as would they without this mod.

If the mod **Ore Extractor Tweaks** by *Lathrey* is present, its ore generator functionality is integrated with this mod. Specifically, if `configOnlyExtractDetectedOre` is true *and* a machine mines a common ore *and* there is no target inventory for it, the ore is not deposited anywhere.

Note also that machines are slow to mine resources.


## (Cheat) Teleport to Nearest Minable

- Press <kbd>F8</kbd> to teleport to the nearest minable resource. 
- Press <kbd>Shift+F8</kbd> to teleport to the nearest minable resource and mine it instantly. 
- Press <kbd>CTRL+F8</kbd> to mine the nearest resource without moving the character. 


Note that some
resources are out of bounds and are not normally player-reachable. You may also
fall to your death so be careful on permadeath!

Remark: `Uranim` is a misspelling in the vanilla game which will probably never be fixed as it would break saves.

### Configuration

`akarnokd.theplanetcraftermods.cheatteleportnearestminable.cfg`

```
[General]

## List of comma-separated resource ids to look for.
# Setting type: String
# Default value: Cobalt,Silicon,Iron,ice,Magnesium,Titanium,Aluminium,Uranim,Iridium,Alloy,Zeolite,Osmium,Sulfur,PulsarQuartz,PulsarShard
ResourceSet = Cobalt,Silicon,Iron,ice,Magnesium,Titanium,Aluminium,Uranim,Iridium,Alloy,Zeolite,Osmium,Sulfur,PulsarQuartz,PulsarShard
```

## (Cheat) Minimap

Display a minimap on the lower left side of the screen.

Press <kbd>N</kbd> to show/hide the minimap.
Press <kbd>Shift+N</kbd> or <kbd>Mouse 4</kbd> to zoom in.
Press <kbd>Ctrl+N</kbd> or <kbd>Mouse 5</kbd> to zoom out.
Press <kbd>Alt+N</kbd> to show/hide/autoscan chests.

Notes
- Uses two static maps: barren and lush, where lush is currently set to show after 200 MTi.
- Currently, this was the best map that I could find and also wouldn't be huge.
- Can't do much about the rotating square, Unity has some UI rendering quirks.
- The current map is from https://map.fistshake.net/PlanetCrafter/ by **I crash at random** on the Steam Guides page https://steamcommunity.com/sharedfiles/filedetails/?id=2786757809


### Configuration

`akarnokd.theplanetcraftermods.cheatminimap.cfg`

```
[General]

## The minimap panel size
# Setting type: Int32
# Default value: 400
MapSize = 400

## Panel position from the bottom of the screen
# Setting type: Int32
# Default value: 350
MapBottom = 350

## The zoom level
# Setting type: Int32
# Default value: 4
ZoomLevel = 4

## The key to press to toggle the minimap
# Setting type: String
# Default value: N
ToggleKey = N

## Which mouse button to use for zooming in (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)
# Setting type: Int32
# Default value: 4
ZoomInMouseButton = 4

## Which mouse button to use for zooming out (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)
# Setting type: Int32
# Default value: 5
ZoomOutMouseButton = 5

## If nonzero and the minimap is visible, the minimap periodically scans for chests every N seconds. Toggle with Alt+N
# Setting type: Int32
# Default value: 5
AutoScanForChests = 5

## If negative, the map rotates on screen. If Positive, the map is fixed to that rotation in degrees (0..360).
# Setting type: Int32
# Default value: -1
FixedRotation = -1
```

## (Cheat) Inventory Stacking

Items in inventories now stack. The stack count is displayed on the middle of the item.

Use <kbd>Shift-Left Click</kbd> to move a particular stack of items.

:warning: The game is not meant to be working with stacks of items and I might not have
found all places where this can be bad. Backup your saves!

### Configuration

```
[General]

## The stack size of all item types in the inventory
# Setting type: Int32
# Default value: 10
StackSize = 10

## The font size for the stack amount
# Setting type: Int32
# Default value: 25
FontSize = 25
```

## (Perf) Load Inventories Faster

:warning: **Discontinued, now part of the vanilla game.**

This speeds up loading the game when there are lots of containers or (modded) containers have a lot of items.

### Configuration

None.

## (Fix) International Loading

The game saves the beacon color information in a localized manner that crashes the game on a different windows locale. This mod fixes this by patching the color parsing so it accepts comma and colon as decimal separator.

### Configuration

None.

## (Fix) Unbrick Save

The mod prevents the game from crashing in case the save contains an unplaceable object (often added by 3rd party mods).

Current fixes:
- Remove objects that can't or shouldn't be built as the game has no visual assets for them.
- Prevent the load screen from crashing when a save is truncated (these are beyond repair).

### Configuration

None.

## (Perf) Reduce Save Size

This mods the save process and removes attributes with default values from the `WorldObject`s, reducing
the save size. These attributes are automatically restored when the game loads.

The save remains compatible with the vanilla game so it will still work without this mod (but will be
full size again).

## (Save) Auto Backup

When saving the game, this mod will automatically make a backup copy (optionally compressed) in a specified directory.
You can also control how many and how old backup saves to keep in this directory per world.

Start the game once so you get the default config file `BepInEx\config\akarnokd.theplanetcraftermods.saveautobackup.cfg`.
Quit, then open this file and set `OutputPath` to an **existing directory**. Example

```
OutputPath = c:\Temp\ThePlanetCrafterBackup\
```

Leave `OutputPath` empty to disable the backup process.

Files are saved based on the name of your world plus a timestamp:

- `Survival-9_backup_20220523_115024_255.json.gz`
- `Survival-9_backup_20220523_115024_255.json`

### Configuration

`akarnokd.theplanetcraftermods.saveautobackup.cfg`

```
[General]

## The path where the backups will be placed if not empty. Make sure this path exists!
# Setting type: String
# Default value: 
# c:\Temp\ThePlanetCrafterBackup\
OutputPath = 

## Compress the backups with GZIP?
# Setting type: Boolean
# Default value: true
GZIP = true

## If zero, all previous backups are retained. If positive, only that number of backups per world is kept and the old ones will be deleted
# Setting type: Int32
# Default value: 0
KeepCount = 0

## If zero, all previous backups are retained. If positive, backups older than this number of days will be deleted. Age is determined from the file name's timestamp part
# Setting type: Int32
# Default value: 0
KeepAge = 0

## If true, the backup handling is done asynchronously so the game doesn't hang during the process.
# Setting type: Boolean
# Default value: true
Async = true
```

## (UI) Customize Inventory Sort Order

Specify the order of items when clicking on the sort all button in inventories.

### Configuration

`akarnokd.theplanetcraftermods.uicustominventorysortall.cfg`

```
[General]

## List of comma-separated resource ids to look for in order.
# Setting type: String
# Default value: OxygenCapsule1,WaterBottle1,astrofood
Preference = OxygenCapsule1,WaterBottle1,astrofood
```

## (UI) Don't Close Craft Window

When crafting an item, <kbd>Right Click</kbd> to not close the crafting window.

### Configuration

None.

## (UI) Grower Grab Vegetable Only

:warning: **Discontinued. The vanilla game now grabs the vegetable only.**

When looking at a grown vegetable in a Grower, hold <kbd>Shift</kbd> while clicking
the vegetable itself to not take the seed, so it can immediately grow the next vegetable.

### Configuration

None.

## (UI) Hide Beacons in Photomode

:warning: **Discontinued. Fixed in the vanilla game.**

When using the photomode (<kbd>F2</kbd>), this mod will hide the user placed and colored
beacons.

### Configuration

None.

## (UI) Hotbar

This mod adds 9 slots to the bottom of the screen where you can pin buildable objects from the Constuction screen <kbd>Q</kbd>.

On the construction screen, hold the <kbd>1</kbd>..<kbd>9</kbd> number keys while <kbd>Left clicking</kbd> on a buildable item.

On the normal screen (outside any UI), press <kbd>1</kbd>..<kbd>9</kbd> to build that item if you have enough resources for it.

If the mod [**(UI) Pin Recipe To Screen**](#ui-pin-recipe-to-screen) is also installed,
On the normal screen (outside any UI), press <kbd>Shift+1</kbd>..<kbd>Shift+9</kbd>
to pin/unpin the recipe to the item in the particular non-empty slot.

The color and numbers on the top right of the panels indicate how many of that item you can build.

- If the mod [**Craft From Containers**](https://www.nexusmods.com/planetcrafter/mods/9) by aedenthorn is installed and active (toggle via <kbd>Home</kbd> by default), nearby inventories are also considered when showing how many of the selected buildings one can build.

Multi-build is allowed by holding <kbd>CTRL</kbd> while clicking to build something over and over (vanilla feature as of 0.4.011, not affected by this mod).

### Configuration

`akarnokd.theplanetcraftermods.uihotbar.cfg`

```
[General]

## The size of each inventory slot
# Setting type: Int32
# Default value: 75
SlotSize = 75

## The font size of the slot index
# Setting type: Int32
# Default value: 20
FontSize = 20

## Placement of the panels relative to the bottom of the screen.
# Setting type: Int32
# Default value: 40
SlotBottom = 40
```

## (UI) Inventory Move Multiple Items

When transferring items between the player backpack and any container,
- Press <kbd>Middle Mouse</kbd> to transfer all items of the same type (i.e., all Iron)
- Press <kbd>Shift+Middle Mouse</kbd> to transfer a small amount of items of the same type (default 5)
- Press <kbd>Ctrl+Shift+Middle Mouse</kbd> to transfer a larger amount of items of the same type (default 50)

:information_source: The vanilla game now supports transferring all the same type of items via <kbd>CTRL</kbd>+<kbd>Left Mouse</kbd>.

### Configuration

`akarnokd.theplanetcraftermods.uiinventorymovemultiple.cfg`

```
[General]

## How many items to move when only a few to move.
# Setting type: Int32
# Default value: 5
MoveFewAmount = 5

## How many items to move when many to move.
# Setting type: Int32
# Default value: 50
MoveManyAmount = 50
```

## (UI) Show Consumable Counts

Next to the Health, Food and Water Gauges, display the number of consumables of each type
the player has in its inventory.

### Configuration

`akarnokd.theplanetcraftermods.uishowconsumablecount.cfg`

```
[General]

## The font size
# Setting type: Int32
# Default value: 20
FontSize = 20
```

## (UI) Show Container Content Info

When looking at a container before opening it, the tooltip at the bottom of the screen will show how many
items are in there, the capacity of the container and the very first item type.
(Pro tip: store different types of items in different containers)

Example: `Open Container [ 5 / 30 ] Cobalt`

### Configuration

None.

## (UI) Show Grab N Mine Count

When picking up items or mining ore, this mod will show a small information indicator (left side) for what you picked up
and how many of the same item is now in your inventory.

### Configuration

`akarnokd.theplanetcraftermods.uishowgrabnminecount.cfg`

```
[General]

## Is the visual notification enabled?
# Setting type: Boolean
# Default value: true
Enabled = true
```


## (UI) Show Player Inventory Counts

In the bottom left part of the screen, there are some numbers showing the player's position, status and
framerate. This mod will add the current number of inventory items, the inventory capacity and
how many items can be added to it.

Example: `800,0,100:4:60   <[  5  /  30  (  -25  )]>`

### Configuration

None.

## (UI) Show Player Tooltip Item Count

When in an inventory or build screen, in the tooltip of an item, show the number of items of the same
type in the player's backpack.

Example: `Cobalt x 5`

### Configuration

None.

## (UI) Show Rocket Counts

On the Terraformation information screen (one of the large screens), show the number of rockets used for
each type of terraformation effect: oxygen, heat, pressure, biomass, next to the current growth speed.

Example: ` 2 x -----    6000.00 nPa/s`

On the Launch Platform's crafting screen, the number of rockets are shown above each rocket type.

### Configuration

`akarnokd.theplanetcraftermods.uishowrocketcount.cfg`

```
[General]

## The font size of the counter text on the craft screen
# Setting type: Int32
# Default value: 20
FontSize = 20
```

## (UI) Show MultiTool Mode

Shows the current multitool mode as text and icon on the 2D hud. Useful when running the game on lower resolution and the tool's 3D image is cut off.

### Configuration

`akarnokd.theplanetcraftermods.uishowmultitoolmode.cfg`

```
[General]

## Show the current mode as text?
# Setting type: Boolean
# Default value: true
ShowText = true

## Show the current mode as icon?
# Setting type: Boolean
# Default value: true
ShowIcon = true

## The size of the font used
# Setting type: Int32
# Default value: 15
FontSize = 15

## The icon size
# Setting type: Int32
# Default value: 100
IconSize = 100

## The width of the text background
# Setting type: Int32
# Default value: 200
TextWidth = 200

## How transparent the text/icon background should be.
# Setting type: Int32
# Default value: 80
TransparencyPercent = 80

## Position of the text from the bottom of the screen
# Setting type: Int32
# Default value: 30
Bottom = 30

## Position of the text from the right of the screen
# Setting type: Int32
# Default value: 10
Right = 10
```

## (UI) Pin Recipe to Screen

On the various craft screens, use <kbd>Middle click</kbd> to pin or unpin a craftable recipe to the screen.

To unpin all recipes, press <kbd>C</kbd>.

In the panel, the curly parenthesis indicates how many of that item is in the player's inventory.
The `< 2 >` indicates how many of the recipe can be crafted from the given inventory.

Note that pinned recipes can't be saved currently as it requires save modding.

### Configuration

`akarnokd.theplanetcraftermods.uipinrecipe.cfg`

```cs
[General]

## The size of the font used
# Setting type: Int32
# Default value: 25
FontSize = 25

## The width of the recipe panel
# Setting type: Int32
# Default value: 850
PanelWidth = 850

## Panel position from the top of the screen.
# Setting type: Int32
# Default value: 150
PanelTop = 150

## The key to press to clear all pinned recipes
# Setting type: String
# Default value: C
ClearKey = C
```

## (UI) Craft Equipment Inplace

When crafting upgrades to equimpent currently equipped, the newer equipment
will be replaced inplace. This avoids loosing backpack capacity or equipment capacity
for the duration of a traditional crafting step.

Note that the UI will indicate you are missing the equipment as an ingredient but
the crafting action will succeed if the rest of the materials are in your backpack.

:warning: Please make a backup of your save before attempting to use this mod, just in case.

### Configuration

None.

## (UI) Save When Quitting

Automatically saves the game when clicking the "Exit to main menu" button.

### Configuration

`akarnokd.theplanetcraftermods.saveonquit.cfg`

```
[General]

## Is the mod enabled?
# Setting type: Boolean
# Default value: true
Enabled = true
```

## (UI) Teleporter Scroll Targets

This mod allows scrolling the teleporter targets on screen by adding up and down buttons or via <kbd>Mouse scroll</kbd>. You can configure the number of targets shown at once.

### Config

`akarnokd.theplanetcraftermods.uiteleporterscroll.cfg`

```
[General]

## Maximum number of targets to show at once.
# Setting type: Int32
# Default value: 6
MaxTargets = 6
```

## (UI) Hungarian Translation

Patches in labels and enables switching to Hungarian ("Magyar") in the game's options screen. Note that some labels do not change when switching to Hungarian the first time. This is a bug in the vanilla game's UI and can be resolved by restarting the game.

The translation contains partly my own work, partly the translations (already accepted or still pending) provided by the community on https://www.localizor.com/the-planet-crafter/translate?language=5&key=297111 . Some consistency-adjustments were made.



Magyar nyelvűvé változtatja a játékot. A játék beállítások (Options) képernyőjén lévő nyelvi opciók közül a "Magyar" bejegyzést kell kiválasztani. Sajnos néhány felirat nem változik magyarrá az első nyelvváltás alkalmával. Ez egy hiba az eredeti játékban és a játék teljes újraindításával orvosolható.

A fordítás részben saját munka, részben a https://www.localizor.com/the-planet-crafter/translate?language=5&key=297111 weboldalon a közösség által
még nem vagy már elfogadott fordításokat tartalmazza. Néhány fordítás egy picit át lett alakítva a következetesség és konzisztencia érdekében.


### Configuration

Only diagnostic options. Not relevant for the player.


## (Lib) Support Mods with Load n Save

This mod alters the loading and saving of the game by parsing/appending custom information based on
registered callbacks. These callbacks can be registered by other plugins and thus they can use this
plugin for save-specific persistency.

Here is an example plugin that utilizes this plugin:

https://github.com/akarnokd/ThePlanetCrafterMods/blob/main/ExampleModLoadSaveSupportSoft/Plugin.cs

### Developer notes

#### Dependency setup

To add a (soft) dependency to this plugin in your own plugin, use the `BepInDependency` annotation
with the guid (`akarnokd.theplanetcraftermods.libmodloadsavesupport`) of this plugin:

```cs
[BepInPlugin(guid, "(Example) Soft Dependency on ModLoadSaveSupport", "1.0.0.0")]
[BepInDependency(libModLoadSaveSupportGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    const string libModLoadSaveSupportGuid = "akarnokd.theplanetcraftermods.libmodloadsavesupport";

    const string guid = "akarnokd.theplanetcraftermods.examplemodloadsavesupportsoft";
}
```

This way, BepInEx knows to load your plugin after this plugin.

#### Registering callbacks

This plugin uses callbacks to get what to save or notify about what has been loaded for a plugin.

First, locate this plugin via its guid:

```cs
using BepInEx.Bootstrap;

if (Chainloader.PluginInfos.TryGetValue(libModLoadSaveSupportGuid, 
        out BepInEx.PluginInfo pi))
{
}
```

If found, locate the `RegisterLoadSave` method on its instance:

```cs
// public IDisposable RegisterLoadSave(string guid, Action<string> onLoad, Func<string> onSave)

MethodInfo mi = pi.Instance.GetType().GetMethod("RegisterLoadSave",
                   new Type[] { 
                       typeof(string), 
                       typeof(Action<string>), 
                       typeof(Func<string>) 
                   }
);

```

Then, invoke it with your plugin id and the delegates to a load and a save function:

```cs
this.handle = (IDisposable)mi.Invoke(pi.Instance, new object[] { 
    guid, new Action<string>(OnLoad), new Func<string>(OnSave) 
});

//...

IDisposable handle;

void OnLoad(string content) {

}
string OnSave() {

}
```

The handle is there to remove the registration if needed.

```cs
void OnDestroy()
{
    handle?.Dispose();
    handle = null;
}
```

:warning: Please make sure the string you return does not contain the `@` or `|` characters as these are treated by the vanilla game
and this mod as separators.

#### Loading Lifecycle

This plugin will deliver the custom save data (if any) after the `SessionController.Start` of the game
returns control. This way, every vanilla game data should be initialized.

Note that there is no guaranteed order of loading data for different registered plugins.
