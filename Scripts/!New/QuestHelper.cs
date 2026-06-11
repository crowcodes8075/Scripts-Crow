/*
name: QuestHelper
description: Utility helpers for story/quest scripts — potion management and a hard KillQuest that auto-stocks, equips and re-activates potions based on the equipped class.
tags: helper, potions, quest
*/

//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreFarms.cs
//cs_include Scripts/CoreAdvanced.cs
//cs_include Scripts/CoreStory.cs
//cs_include Scripts/Other/Various/Potions.cs
//cs_include Scripts/Ultras-v2/Dependencies/CoreEngine.cs
//cs_include Scripts/Ultras-v2/Dependencies/CoreUltra.cs
//cs_include Scripts/Ultras-v2/Dependencies/UltraPotions.cs

using System;
using System.Linq;
using System.Collections.Generic;
using Skua.Core.Interfaces;
using Skua.Core.Models.Items;
using Skua.Core.Models.Quests;

public class QuestHelper
{
    public IScriptInterface Bot => IScriptInterface.Instance;
    public CoreBots Core => CoreBots.Instance;
    public CoreEngine C = new();

    private static CoreStory Story
    {
        get => _Story ??= new CoreStory();
        set => _Story = value;
    }
    private static CoreStory _Story;

    private static CoreAdvanced Adv
    {
        get => _Adv ??= new CoreAdvanced();
        set => _Adv = value;
    }
    private static CoreAdvanced _Adv;

    private static PotionBuyer Buyer
    {
        get => _Buyer ??= new PotionBuyer();
        set => _Buyer = value;
    }
    private static PotionBuyer _Buyer;

    // -------------------------------------------------------
    // Potion presets — mirrors UltraPotions.GetRecommendedPotions
    // but lives here so QuestHelper is self-contained.
    // -------------------------------------------------------

    /// <summary>
    /// Returns the three recommended potions for the currently equipped class.
    /// Order: tonic, elixir, potion (slot 5 consumable last).
    /// </summary>
    private string[] GetPotionsForClass()
    {
        string cls = Bot.Player?.CurrentClass?.Name ?? string.Empty;

        return cls switch
        {
            "ArchPaladin"   => new[] { "Fate Tonic", "Potent Destruction Elixir", "Felicitous Philtre" },
            "StoneCrusher"  => new[] { "Fate Tonic", "Potent Malevolence Elixir", "Felicitous Philtre" },
            "Lord Of Order" => new[] { "Fate Tonic", "Unstable Divine Elixir",    "Felicitous Philtre" },
            "King's Echo"   => new[] { "Body Tonic",  "Potent Destruction Elixir", "Felicitous Philtre" },
            "Yami no Ronin" => new[] { "Fate Tonic",  "Potent Destruction Elixir", "Felicitous Philtre" },
            "Dragon of Time" => new[] { "Fate Tonic", "Potent Destruction Elixir", "Felicitous Philtre" },
            "Shaman"        => new[] { "Sage Tonic",  "Potent Malevolence Elixir", "Felicitous Philtre" },
            _               => new[] { "Fate Tonic", "Potent Destruction Elixir", "Felicitous Philtre" },
        };
    }

    /// <summary>
    /// Returns the aura name that corresponds to a potion's active buff.
    /// Tonics and elixirs auto-apply on equip and don't have a re-checkable aura,
    /// so they return null — meaning "always activate, no guard needed".
    /// </summary>
    private string? GetPotionAura(string potion) => potion switch
    {
        "Potent Honor Potion" => "Potent Honor Malice",
        "Potent Life Potion"  => "Righteous",
        "Felicitous Philtre"  => "Felicitous Philtre",
        "Endurance Draught"   => "Endurance Draught",
        _                     => null,   // tonic / elixir — one-shot on equip
    };

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <summary>
    /// Applies the recommended enhancement preset for the given class.
    /// Explicit presets for classes used in hard fights — no guessing, no fallbacks.
    /// </summary>
    public void Enhance(string className)
    {
        if (!Core.CheckInventory(className))
        {
            Core.Logger($"[QuestHelper] Enhance: {className} not in inventory, skipping.");
            return;
        }

        Core.Logger($"[QuestHelper] Enhancing for: {className}");

        switch (className)
        {
            case "ArchPaladin":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Forge,
                    wSpecial: WeaponSpecial.Spiral_Carve,
                    cSpecial: CapeSpecial.Lament
                );
                break;

            case "King's Echo":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Wizard,
                    hSpecial: HelmSpecial.Forge,
                    wSpecial: WeaponSpecial.Lacerate,
                    cSpecial: CapeSpecial.Lament
                );
                break;

            case "Yami no Ronin":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Vim,
                    wSpecial: WeaponSpecial.Mana_Vamp,
                    cSpecial: CapeSpecial.Vainglory
                );
                break;

            case "Shaman":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Examen,
                    wSpecial: WeaponSpecial.Ravenous,
                    cSpecial: CapeSpecial.Lament
                );
                break;

            case "Legion Revenant":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Wizard,
                    hSpecial: HelmSpecial.Pneuma,
                    wSpecial: WeaponSpecial.Ravenous,
                    cSpecial: CapeSpecial.Vainglory
                );
                break;

            case "Void Highlord":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Anima,
                    wSpecial: WeaponSpecial.Valiance,
                    cSpecial: CapeSpecial.Vainglory
                );
                break;

            case "Dragon of Time":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Pneuma,
                    wSpecial: WeaponSpecial.Awe_Blast,
                    cSpecial: CapeSpecial.Vainglory
                );
                break;

            default:
                Core.Logger($"[QuestHelper] No preset for '{className}', using SmartEnhance.");
                Adv.SmartEnhance(className);
                break;
        }
    }

    /// <summary>
    /// Stocks, equips and activates the recommended potions for the currently equipped class.
    /// </summary>
    public void UsePotions(int desiredQuant = 5)
    {
        string[] potions = GetPotionsForClass();
        StockPotions(potions, desiredQuant);
        ActivatePotions(potions);
    }

    /// <summary>
    /// Re-fires the slot-5 potion if its aura has dropped.
    /// Call this once per kill loop iteration when using potions outside of HardKillQuest.
    /// </summary>
    public void ReactivatePotions()
    {
        string[] potions = GetPotionsForClass();
        ReactivateSlot5Potion(potions);
    }

    /// <summary>
    /// KillQuest with an optional per-loop action.
    /// Mirrors CoreStory.KillQuest but exposes an onLoop hook.
    /// </summary>
    public void KillQuest(int questID, string map, string monster, Action? onLoop = null)
    {
        if (Core.isCompletedBefore(questID))
            return;

        Quest? quest = Core.InitializeWithRetries(() => Core.EnsureLoad(questID));
        if (quest == null)
        {
            Core.Logger($"[QuestHelper] Quest {questID} could not be loaded.");
            return;
        }

        if (Bot.Quests.HasBeenCompleted(questID))
            return;

        List<ItemBase> requirements = quest.Requirements
            .Where(r => r != null && !string.IsNullOrEmpty(r.Name))
            .Where(r => !(r.Temp
                ? Bot.TempInv.Contains(r.Name, r.Quantity)
                : Core.CheckInventory(r.ID, r.Quantity)))
            .ToList();

        if (requirements.Count == 0)
        {
            Core.EnsureAccept(questID);
            Core.EnsureComplete(questID);
            return;
        }

        Core.EnsureAccept(questID);
        Core.Join(map);

        foreach (var req in requirements.Where(r => !r.Temp))
            Core.AddDrop(req.Name);

        Core.Logger($"[QuestHelper] Farming {monster} for quest {questID}");

        while (!Bot.ShouldExit)
        {
            bool allDone = requirements.All(r =>
                r.Temp
                    ? Bot.TempInv.Contains(r.Name, r.Quantity)
                    : Core.CheckInventory(r.ID, r.Quantity));

            if (allDone || Bot.Quests.CanComplete(questID))
                break;

            if (!Bot.Player.Alive)
            {
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                continue;
            }

            onLoop?.Invoke();
            Bot.Combat.Attack(monster);
            Bot.Sleep(500);
        }

        Core.EnsureComplete(questID);
        Bot.Wait.ForQuestComplete(questID);
        Bot.Sleep(1000);
    }

    /// <summary>
    /// Hard variant of KillQuest for tough bosses.
    /// Automatically determines the right potions for the equipped class,
    /// stocks them, activates them before the fight, and re-activates them
    /// after death or if their aura drops mid-fight.
    /// No potion parameters needed — just questID, map, and monster.
    /// </summary>
    public void HardKillQuest(int questID, string map, string monster, int desiredPotionQuant = 5, Action? onLoop = null)
    {
        if (Core.isCompletedBefore(questID))
            return;

        Quest? quest = Core.InitializeWithRetries(() => Core.EnsureLoad(questID));
        if (quest == null)
        {
            Core.Logger($"[QuestHelper] Quest {questID} could not be loaded.");
            return;
        }

        if (Bot.Quests.HasBeenCompleted(questID))
            return;

        List<ItemBase> requirements = quest.Requirements
            .Where(r => r != null && !string.IsNullOrEmpty(r.Name))
            .Where(r => !(r.Temp
                ? Bot.TempInv.Contains(r.Name, r.Quantity)
                : Core.CheckInventory(r.ID, r.Quantity)))
            .ToList();

        if (requirements.Count == 0)
        {
            Core.EnsureAccept(questID);
            Core.EnsureComplete(questID);
            return;
        }

        // Resolve potions for the current class once up front
        string[] potions = GetPotionsForClass();

        // Stock and activate before joining the fight map
        StockPotions(potions, desiredPotionQuant);
        ActivatePotions(potions);

        Core.EnsureAccept(questID);
        Core.Join(map);

        foreach (var req in requirements.Where(r => !r.Temp))
            Core.AddDrop(req.Name);

        Core.Logger($"[QuestHelper] Hard-farming {monster} for quest {questID}");

        bool wasDead = false;

        C.EnableSkills();

        while (!Bot.ShouldExit)
        {
            bool allDone = requirements.All(r =>
                r.Temp
                    ? Bot.TempInv.Contains(r.Name, r.Quantity)
                    : Core.CheckInventory(r.ID, r.Quantity));

            if (allDone || Bot.Quests.CanComplete(questID))
                break;

            if (!Bot.Player.Alive)
            {
                wasDead = true;
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                continue;
            }

            // Re-activate all potions after a death
            if (wasDead)
            {
                wasDead = false;
                Core.Logger("[QuestHelper] Died — re-applying potions.");
                ActivatePotions(potions);
            }

            // Per-loop: re-fire any slot-5 potion whose aura has dropped
            ReactivateSlot5Potion(potions);

            onLoop?.Invoke();

            // Cell-finding + MapID targeting (same as CoreStory._MonsterHuntBatch)
            var targetCellGroup = Bot.Monsters.MapMonsters?
                .Where(m => m != null && m.Name.FormatForCompare() == monster.FormatForCompare())
                .GroupBy(m => m.Cell)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            string targetCell = targetCellGroup?.Key ?? "Enter";

            if (!string.Equals(Bot.Player.Cell, targetCell, StringComparison.OrdinalIgnoreCase))
            {
                Bot.Map.Jump(targetCell, "Left");
                Bot.Wait.ForCellChange(targetCell);
                Bot.Player.SetSpawnPoint();
            }

            var target = Bot.Monsters.CurrentAvailableMonsters?
                .FirstOrDefault(m => m != null
                    && m.Name.FormatForCompare() == monster.FormatForCompare()
                    && m.HP > 0);

            if (target != null && (!Bot.Player.HasTarget || Bot.Player.Target?.MapID != target.MapID))
                Bot.Combat.Attack(target.MapID);

            Bot.Sleep(500);
        }

        C.DisableSkills();

        Core.EnsureComplete(questID);
        Bot.Wait.ForQuestComplete(questID);
        Bot.Sleep(1000);
    }

    // -------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------

    /// <summary>
    /// Buys any potions in the list that are below the desired quantity.
    /// </summary>
    private void StockPotions(string[] potions, int desiredQuant)
    {
        List<string> missing = potions
            .Where(p => Bot.Inventory.GetQuantity(p) < desiredQuant)
            .ToList();

        if (missing.Count == 0)
            return;

        Core.Logger($"[QuestHelper] Buying: {string.Join(", ", missing)}");
        Buyer.INeedYourStrongestPotions(
            Potions: missing.ToArray(),
            PotionQuant: desiredQuant,
            Seperate: true,
            BuyReagents: true
        );
    }

    /// <summary>
    /// Equips and activates every potion in the list, mirroring UltraPotions logic but
    /// without whitemap trips — safe to call from any map including active fight maps.
    /// - Checks Bot.Self.Auras to skip already-active potions
    /// - For items that are already equipped, skips activation (e.g. Felicitous Philtre)
    /// - Uses EquipUsableItem directly (no map change)
    /// - Falls back to Core.UsePotion() if the aura didn't auto-apply on equip
    /// </summary>
    private void ActivatePotions(string[] potions)
    {
        foreach (string potion in potions)
        {
            // Already active via aura — skip
            if (Bot.Self.Auras.Any(a => a != null && a.Name == potion))
            {
                Core.Logger($"[QuestHelper] {potion} already active.");
                continue;
            }

            // Already equipped — passive items (e.g. Felicitous Philtre) apply on equip, no skill needed
            if (Bot.Inventory.IsEquipped(potion))
            {
                Core.Logger($"[QuestHelper] {potion} already equipped.");
                continue;
            }

            Core.Logger($"[QuestHelper] Equipping: {potion}");
            Bot.Inventory.EquipUsableItem(potion);
            Bot.Sleep(700);

            // Some consumables (tonics/elixirs) auto-apply on equip
            if (Bot.Self.Auras.Any(a => a != null && a.Name == potion))
            {
                Core.Logger($"[QuestHelper] {potion} applied on equip.");
                continue;
            }

            // If it's now equipped but no aura appeared, it's a passive — nothing more to do
            if (Bot.Inventory.IsEquipped(potion))
            {
                Core.Logger($"[QuestHelper] {potion} equipped (passive).");
                continue;
            }

            if (!Bot.Inventory.IsEquipped(potion))
            {
                Core.Logger($"[QuestHelper] Failed to equip {potion}.");
                continue;
            }

            Core.Logger($"[QuestHelper] Using {potion}...");
            Core.UsePotion();
            Bot.Sleep(700);
        }
    }

    /// <summary>
    /// Per-loop check: only re-fires the last potion in the list (the slot-5 consumable)
    /// if its aura has dropped. Tonics/elixirs are one-shot and don't need mid-fight re-activation.
    /// </summary>
    private void ReactivateSlot5Potion(string[] potions)
    {
        // The last entry is always the slot-5 consumable (Honor Potion, Life Potion, etc.)
        string potion = potions[potions.Length - 1];
        string? aura = GetPotionAura(potion);

        // No known aura — nothing to check
        if (aura == null)
            return;

        if (C.HasAura(aura, true))
            return;

        if (!Bot.Inventory.IsEquipped(potion))
            Bot.Inventory.EquipUsableItem(potion);

        if (Bot.Skills.CanUseSkill(5))
            Bot.Skills.UseSkill(5);

        Bot.Sleep(300);
    }
}
