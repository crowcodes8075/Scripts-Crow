/*
name: TestCoreSkills
description: Test harness to activate CoreSkills and run its skill loop.
tags: test, skills
*/
//cs_include Scripts/!New/CoreSkills.cs
using Skua.Core.Interfaces;

public class TestCoreSkills
{
    private CoreSkills Skills = new();

    public void ScriptMain(IScriptInterface bot)
    {
        Skills.EnableSkills();

        while (!bot.ShouldExit)
            bot.Sleep(1000);

        Skills.DisableSkills();
    }
}
