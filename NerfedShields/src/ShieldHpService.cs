using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace NerfedShields
{
    /// <summary>
    /// Scales shield hit points by editing the ItemObject/WeaponComponentData templates
    /// directly. Because every troop, lord, and the player all read their shield's HP
    /// from the same shared ItemObject, this one change applies globally without needing
    /// to touch per-agent or per-mission state.
    /// </summary>
    public static class ShieldHpService
    {
        // Key = ItemObject.StringId + weapon class, Value = original (100%) hit points.
        private static readonly Dictionary<string, int> OriginalHitPoints = new Dictionary<string, int>();
        private static bool _initialized;

        // IMPORTANT: Bannerlord doesn't expose shield hit points as a stable public
        // property across versions - it's read via reflection so this keeps working
        // even if the backing member is private. If ApplyMultiplier ever throws
        // "Could not find shield hit-points member", open TaleWorlds.Core.dll in
        // dnSpy/ILSpy, find WeaponComponentData, look for the field/property tied to
        // the XML "hit_points" attribute on shield weapons, and add its exact name here.
        private static readonly string[] CandidateMemberNames =
        {
            "MaxDataValue",
            "HitPoints"
        };

        private static bool IsSupportedNumericType(Type t)
        {
            return t == typeof(int) || t == typeof(short);
        }

        private static int ToInt(object value)
        {
            return value is short s ? s : (int)value;
        }

        private static object FromInt(Type targetType, int value)
        {
            return targetType == typeof(short) ? (object)(short)value : value;
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            OriginalHitPoints.Clear();

            int shieldsSeen = 0;
            WeaponComponentData firstUnmatchedShield = null;
            string firstUnmatchedItemName = null;

            foreach (var item in GetAllItems())
            {
                if (item?.WeaponComponent?.Weapons == null)
                {
                    continue;
                }

                foreach (var weapon in item.WeaponComponent.Weapons)
                {
                    if (!IsShield(weapon.WeaponClass))
                    {
                        continue;
                    }

                    shieldsSeen++;

                    int? original = TryGetHitPoints(weapon);
                    if (original == null || original <= 0)
                    {
                        if (firstUnmatchedShield == null)
                        {
                            firstUnmatchedShield = weapon;
                            firstUnmatchedItemName = item.StringId;
                        }
                        continue;
                    }

                    string key = GetKey(item, weapon);
                    if (!OriginalHitPoints.ContainsKey(key))
                    {
                        OriginalHitPoints[key] = original.Value;
                    }
                }
            }

            Log($"Scanned items: found {shieldsSeen} shield weapon components, matched HP on {OriginalHitPoints.Count} of them.");

            if (OriginalHitPoints.Count == 0 && firstUnmatchedShield != null)
            {
                DumpIntMembers(firstUnmatchedItemName, firstUnmatchedShield);
            }

            _initialized = true;
        }

        // TEMPORARY DIAGNOSTIC: prints every numeric field/property on a real shield's
        // WeaponComponentData so we can identify the correct hit-points member name.
        // Safe to delete once CandidateMemberNames is confirmed correct.
        private static readonly HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(int), typeof(short), typeof(ushort), typeof(byte),
            typeof(sbyte), typeof(long), typeof(float), typeof(double)
        };

        private static void DumpIntMembers(string itemName, WeaponComponentData weapon)
        {
            var type = weapon.GetType();
            var lines = new List<string>();
            lines.Add($"No HP match on '{itemName}' ({type.Name}). Dumping ALL numeric members:");

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (NumericTypes.Contains(field.FieldType))
                {
                    object value = field.GetValue(weapon);
                    lines.Add($"  field [{field.FieldType.Name}] {Sanitize(field.Name)} = {value}");
                }
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (NumericTypes.Contains(prop.PropertyType) && prop.GetIndexParameters().Length == 0 && prop.CanRead)
                {
                    object value;
                    try { value = prop.GetValue(weapon); }
                    catch { continue; }
                    lines.Add($"  prop  [{prop.PropertyType.Name}] {Sanitize(prop.Name)} = {value}");
                }
            }

            // Also check the parent ItemObject directly - some versions store shield
            // HP on the item itself rather than on the weapon component.
            lines.Add($"-- ItemObject '{itemName}' numeric members --");
            var itemObj = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>()
                .FirstOrDefault(i => i.StringId == itemName);
            if (itemObj != null)
            {
                var itemType = itemObj.GetType();
                foreach (var field in itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (NumericTypes.Contains(field.FieldType))
                    {
                        object value = field.GetValue(itemObj);
                        lines.Add($"  field [{field.FieldType.Name}] {Sanitize(field.Name)} = {value}");
                    }
                }
                foreach (var prop in itemType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (NumericTypes.Contains(prop.PropertyType) && prop.GetIndexParameters().Length == 0 && prop.CanRead)
                    {
                        object value;
                        try { value = prop.GetValue(itemObj); }
                        catch { continue; }
                        lines.Add($"  prop  [{prop.PropertyType.Name}] {Sanitize(prop.Name)} = {value}");
                    }
                }
            }

            foreach (var line in lines)
            {
                Log(line);
            }

            WriteDebugFile(lines);
        }

        // The game's chat/message renderer treats "<...>" as a text-markup tag and
        // silently deletes it - compiler-generated backing fields are literally named
        // "<PropertyName>k__BackingField", so without this they show up as just
        // "k__BackingField" on screen. Swap the brackets for parens so they survive.
        private static string Sanitize(string name)
        {
            return name.Replace('<', '(').Replace('>', ')');
        }

        private static void WriteDebugFile(List<string> lines)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "NerfedShields_debug.txt");
                System.IO.File.WriteAllLines(path, lines);
                Log("Full dump written to Documents\\Mount and Blade II Bannerlord\\NerfedShields_debug.txt");
            }
            catch (Exception ex)
            {
                Log("Could not write debug file: " + ex.Message);
            }
        }

        private static void Log(string message)
        {
            try
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage("[NerfedShields] " + message));
            }
            catch
            {
                // ignore if called before the message system is ready
            }
        }

        public static void ApplyMultiplier(int percent)
        {
            if (!_initialized)
            {
                Initialize();
            }

            percent = Math.Max(1, Math.Min(100, percent));
            float multiplier = percent / 100f;

            foreach (var item in GetAllItems())
            {
                if (item?.WeaponComponent?.Weapons == null)
                {
                    continue;
                }

                foreach (var weapon in item.WeaponComponent.Weapons)
                {
                    if (!IsShield(weapon.WeaponClass))
                    {
                        continue;
                    }

                    string key = GetKey(item, weapon);
                    if (!OriginalHitPoints.TryGetValue(key, out int original))
                    {
                        continue;
                    }

                    int scaled = Math.Max(1, (int)Math.Round(original * multiplier));
                    TrySetHitPoints(weapon, scaled);
                }
            }
        }

        public static void RestoreAll()
        {
            if (_initialized)
            {
                ApplyMultiplier(100);
            }
        }

        private static IEnumerable<ItemObject> GetAllItems()
        {
            var list = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
            if (list == null)
            {
                yield break;
            }

            foreach (var item in list)
            {
                yield return item;
            }
        }

        private static string GetKey(ItemObject item, WeaponComponentData weapon)
        {
            return item.StringId + "::" + weapon.WeaponClass;
        }

        private static bool IsShield(WeaponClass weaponClass)
        {
            return weaponClass == WeaponClass.SmallShield || weaponClass == WeaponClass.LargeShield;
        }

        private static int? TryGetHitPoints(WeaponComponentData weapon)
        {
            var type = weapon.GetType();

            foreach (var name in CandidateMemberNames)
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanRead && IsSupportedNumericType(prop.PropertyType))
                {
                    return ToInt(prop.GetValue(weapon));
                }

                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && IsSupportedNumericType(field.FieldType))
                {
                    return ToInt(field.GetValue(weapon));
                }
            }

            return null;
        }

        private static bool TrySetHitPoints(WeaponComponentData weapon, int value)
        {
            var type = weapon.GetType();

            foreach (var name in CandidateMemberNames)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && IsSupportedNumericType(field.FieldType))
                {
                    field.SetValue(weapon, FromInt(field.FieldType, value));
                    return true;
                }

                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && IsSupportedNumericType(prop.PropertyType))
                {
                    var setter = prop.GetSetMethod(true);
                    if (setter != null)
                    {
                        setter.Invoke(weapon, new object[] { FromInt(prop.PropertyType, value) });
                        return true;
                    }

                    var backingField = type.GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (backingField != null)
                    {
                        backingField.SetValue(weapon, FromInt(backingField.FieldType, value));
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
