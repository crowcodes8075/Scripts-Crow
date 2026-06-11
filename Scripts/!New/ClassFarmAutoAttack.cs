/*
name: ClassFarmAutoAttack
description: Equips Farm class and auto-attacks on the spot.
tags: classfarm, autoattack, farm
*/
//cs_include Scripts/CoreBots.cs

using Skua.Core.Interfaces;
using Skua.Core.Models;

public class ClassFarmAutoAttack
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots Core => CoreBots.Instance;

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions(disableClassSwap: true);

        Bot.Options.AttackWithoutTarget = true;
        Bot.Combat.StopAttacking = false;

        try
        {
            Core.Logger("Equipping Farm Class...");
            Core.EquipClass(ClassType.Farm);
            const string monster = "*";

            Core.Logger("Auto-attacking on the spot with Farm class.");

            while (!Bot.ShouldExit)
            {
                if (!Bot.Player.Alive)
                {
                    Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                    continue;
                }

                if (!Bot.Player.HasTarget)
                    Bot.Combat.Attack(monster);

                Core.Sleep(500);
            }
        }
        finally
        {
            Bot.Options.AttackWithoutTarget = false;
            Bot.Combat.StopAttacking = false;
            Core.SetOptions(false);
        }
    }
}
