# Nerfed Shields

A Bannerlord mod that adds one MCM slider — **Shield HP %** (1–100) — which scales
the hit points of every shield in the game (player, companions, troops, and AI
lords) to that percentage of its original value.

## How it works

Shield hit points aren't per-agent state — they live on the shared `ItemObject`
that every character equipping that shield reads from. `ShieldHpService`:

1. On game start, walks every `ItemObject` with a shield `WeaponComponentData`
   (`WeaponClass.SmallShield` / `WeaponClass.LargeShield`) and caches its
   original hit points.
2. Whenever the slider changes (or on game start), multiplies each cached
   original by `percent / 100` and writes it back.

Because it's one shared template per item, this automatically covers the
player, every companion, every troop, and every AI lord — there's no need to
patch combat/damage code at all.

## ⚠️ Before you build: verify one field name

Bannerlord doesn't expose a stable public `ShieldHitPoints` property — the
value is read via reflection in `ShieldHpService.CandidateMemberNames`,
currently `["MaxDataValue", "HitPoints"]` based on the XML `hit_points`
attribute on shield weapon components. **Confirm this against the game
version you're building for**:

1. Open `TaleWorlds.Core.dll` (from your game's
   `bin\Win64_Shipping_Client` folder) in [dnSpy](https://github.com/dnSpy/dnSpy)
   or [ILSpy](https://github.com/icsharpcode/ILSpy).
2. Find `WeaponComponentData` and look for the int field/property that the
   XML deserializer maps to the `hit_points` attribute (search for
   `"hit_points"` as a string reference, or check the `Weapon` XML docs at
   https://docs.bannerlordmodding.com for the current mapping).
3. If it's not `MaxDataValue` or `HitPoints`, add the correct name to the
   `CandidateMemberNames` array.

If you get the name wrong, `Initialize()` just won't find any shields to
scale (no crash) — but the slider will silently do nothing, so it's worth
testing in-game once (put the slider at something obvious like 25% and check
shield HP in the encyclopedia/inventory tooltip).

## Project layout

```
NerfedShields/
├── NerfedShields.csproj      # build settings + NuGet references
├── ModuleFiles/
│   └── SubModule.xml          # Bannerlord module descriptor
└── src/
    ├── SubModule.cs            # mod entry point / lifecycle hooks
    ├── NerfedShieldsSettings.cs # MCM slider definition
    └── ShieldHpService.cs       # the actual scaling logic
```

## Building

1. Install:
   - [Visual Studio 2022](https://visualstudio.microsoft.com/) (or `dotnet` SDK + VS Code)
   - the game itself, and these Steam Workshop / Nexus mods enabled at least once so
     you have their DLLs to test against: **Harmony**, **UIExtenderEx**, **ButterLib**,
     **Mod Configuration Menu v5**.
2. Open `NerfedShields.csproj`.
3. Edit the `<GameFolder>` property near the top to point at your actual
   Bannerlord install (e.g. `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`).
4. Build (`dotnet build` or Ctrl+Shift+B in Visual Studio). This will:
   - Restore `Bannerlord.ReferenceAssemblies.Core` (metadata-only stand-ins for
     the game DLLs, so you don't need to hand-add DLL references) and
     `Bannerlord.MCM`.
   - Compile `NerfedShields.dll` straight into
     `<GameFolder>\Modules\NerfedShields\bin\Win64_Shipping_Client\`.
   - Copy `SubModule.xml` into `<GameFolder>\Modules\NerfedShields\`.
5. Launch the Bannerlord launcher, and in the **Mods** tab enable, in this order:
   Harmony → UIExtenderEx → ButterLib → Mod Configuration Menu → Nerfed Shields
   (plus your usual Native/SandBoxCore/etc. — the launcher handles load order
   for you once dependencies are declared, but Harmony-family mods should sit
   near the top).
6. Start a campaign or custom battle, open **Mod Options** from the escape
   menu (that's where MCM lives), find **Nerfed Shields**, and move the
   **Shield HP %** slider.

## Notes / things you may want to extend

- The slider is a **Global** MCM setting, so it applies across every save —
  that matches "one global control," but let me know if you'd rather it be
  per-campaign or per-save instead (that's a one-line change: swap
  `AttributeGlobalSettings<T>` for `AttributePerSaveSettings<T>` /
  `AttributePerCampaignSettings<T>`).
- Currently this only scales **max** hit points on the item template. If you
  also want shields that are already damaged mid-battle to rescale live
  (e.g. changing the slider mid-fight instantly changes a shield that's at
  40/180 HP to keep the same *percentage* remaining rather than being clamped),
  that needs a small Harmony patch on `MissionWeapon`/`Agent` shield state
  instead of/in addition to this — happy to add that if you want it.
- `SubModule.xml`'s `DependedModule` version numbers (`v1.0.0` placeholders)
  should be bumped to match whatever game version you're actually shipping
  against before release.
