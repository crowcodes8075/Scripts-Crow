/*
name: Enhance
description: Auto-enhance your configured classes on startup. If the current class has a custom preset, it uses that preset; otherwise it falls back to SmartEnhance.
tags: enhancement, autoenhance, class
*/
//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreFarms.cs
//cs_include Scripts/CoreAdvanced.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Skua.Core.Interfaces;
using Skua.Core.Options;

public class Enhance
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots Core => CoreBots.Instance;
    private static CoreAdvanced Adv => _adv ??= new CoreAdvanced();
    private static CoreAdvanced _adv;

    public bool DontPreconfigure = true;
    public string OptionsStorage = "Enhance";

    public List<IOption> Options = new()
    {
        CoreBots.Instance.SkipOptions,
    };

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions(disableClassSwap: true);
        EnhanceCurrentClass();
        Core.SetOptions(false);
    }

    private void EnhanceCurrentClass()
    {
        string? currentClass = Bot.Player.CurrentClass?.Name;
        if (string.IsNullOrEmpty(currentClass))
        {
            Core.Logger("Enhance failed: no class is currently equipped.");
            return;
        }

        EnhanceClass(currentClass);
    }

    private void EnhanceClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return;

        className = className.Trim();
        string? inventoryName = FindClassName(className);
        if (string.IsNullOrEmpty(inventoryName))
        {
            Core.Logger($"Enhance failed: class '{className}' not found in inventory.");
            return;
        }

        Core.Logger($"Auto-enhancing class: {inventoryName}");

        var preset = GetEnhancementPreset(inventoryName);
        if (preset != null)
        {
            EnhanceWithPreset(inventoryName, preset.Value);
            return;
        }

        Adv.SmartEnhance(inventoryName, true);
    }

    private void EnhanceWithPreset(string className, EnhancementPreset preset)
    {
        if (!Core.CheckInventory(className))
        {
            Core.Logger($"Enhancement failed: class '{className}' not found in inventory.");
            return;
        }

        var classItem = Bot.Inventory.Items.Find(i =>
            i.Category == ItemCategory.Class
            && NormalizeString(i.Name) == NormalizeString(className)
        );

        if (classItem == null)
        {
            Core.Logger($"Enhancement failed: class '{className}' not found in inventory.");
            return;
        }

        if (classItem.EnhancementLevel <= 0)
        {
            Adv.EnhanceItem(classItem.Name, preset.Type);
        }

        Core.Equip(classItem.Name);
        Bot.Wait.ForTrue(() => NormalizeString(Bot.Player.CurrentClass?.Name) == NormalizeString(classItem.Name), 40);

        Adv.EnhanceEquipped(preset.Type, preset.CapeSpecial, preset.HelmSpecial, preset.WeaponSpecial);
    }

    private string? FindClassName(string className)
    {
        return Bot.Inventory.Items
            .Where(i => i.Category == ItemCategory.Class)
            .FirstOrDefault(i => NormalizeString(i.Name) == NormalizeString(className))
            ?.Name;
    }

    private EnhancementPreset? GetEnhancementPreset(string className)
    {
        switch (NormalizeString(className))
        {
            case "king's echo":
                return new EnhancementPreset(
                    Type: EnhancementType.Healer,
                    CapeSpecial: CapeSpecial.Lament,
                    HelmSpecial: HelmSpecial.Examen,
                    WeaponSpecial: WeaponSpecial.Mana_Vamp
                );

            case "lord of order":
                return new EnhancementPreset(
                    Type: EnhancementType.Healer,
                    CapeSpecial: CapeSpecial.Absolution,
                    HelmSpecial: HelmSpecial.Forge,
                    WeaponSpecial: WeaponSpecial.Valiance
                );

            case "archpaladin":
                return new EnhancementPreset(
                    Type: EnhancementType.Fighter,
                    CapeSpecial: CapeSpecial.Absolution,
                    HelmSpecial: HelmSpecial.Forge,
                    WeaponSpecial: WeaponSpecial.Valiance
                );

            case "stonecrusher":
                return new EnhancementPreset(
                    Type: EnhancementType.Fighter,
                    CapeSpecial: CapeSpecial.Absolution,
                    HelmSpecial: HelmSpecial.Forge,
                    WeaponSpecial: WeaponSpecial.Valiance
                );

            case "arcana invoker":
                return new EnhancementPreset(
                    Type: EnhancementType.Healer,
                    CapeSpecial: CapeSpecial.Lament,
                    HelmSpecial: HelmSpecial.Examen,
                    WeaponSpecial: WeaponSpecial.Health_Vamp
                );

            case "void highlord":
                return new EnhancementPreset(
                    Type: EnhancementType.Fighter,
                    HelmSpecial: HelmSpecial.Anima,
                    WeaponSpecial: WeaponSpecial.Valiance
                );

            case "yami no ronin":
                return new EnhancementPreset(
                    Type: EnhancementType.Lucky,
                    CapeSpecial: CapeSpecial.Vainglory,
                    HelmSpecial: HelmSpecial.Vim,
                    WeaponSpecial: WeaponSpecial.Praxis
                );

            default:
                return null;
        }
    }

    private static string NormalizeString(string? input) => (input ?? string.Empty).Trim().ToLowerInvariant();

    private readonly record struct EnhancementPreset(
        EnhancementType Type,
        CapeSpecial CapeSpecial = CapeSpecial.None,
        HelmSpecial HelmSpecial = HelmSpecial.None,
        WeaponSpecial WeaponSpecial = WeaponSpecial.None
    );
}
