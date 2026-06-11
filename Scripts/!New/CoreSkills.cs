/*
name: CoreSkills
description: Minimal skill automation for King's Echo, Void Highlord, and default 1-4 skill rotation.
tags: core, skills
*/
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Skua.Core.Interfaces;
using Skua.Core.Models.Auras;

public class CoreSkills
{
    private IScriptInterface Bot => IScriptInterface.Instance;
    private CancellationTokenSource? _cts;
    private Task? _skillTask;
    private readonly int skillsDelay = 50;

    public void ScriptMain(IScriptInterface bot)
    {
        Bot.Skills.Stop();
        EnableSkills();

        while (!Bot.ShouldExit)
            Bot.Sleep(1000);

        DisableSkills();
    }

    public void EnableSkills()
    {
        if (_skillTask != null && !_skillTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _skillTask = Task.Run(() => SkillLoop(_cts.Token));
    }

    public void DisableSkills()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }
        finally
        {
            _skillTask = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task SkillLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UseSkills();
            }
            catch { }

            try
            {
                await Task.Delay(skillsDelay, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch
            {
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch { }
            }
        }
    }

    private void UseSkills()
    {
        if (!Bot.Player.Alive)
        {
            Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
            return;
        }

        if (!Bot.Player.HasTarget)
            return;

        string className = NormalizeClassName(Bot.Player.CurrentClass?.Name);
        if (string.IsNullOrWhiteSpace(className))
            return;

        switch (className)
        {
            case "king's echo":
                KingsEcho();
                break;
            case "void highlord":
            case "void highlord (ioda)":
                VoidHighLord();
                break;
            default:
                BasicClass();
                break;
        }
    }

    private static string NormalizeClassName(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return string.Empty;

        return string.Join(" ", className.Trim().ToLowerInvariant().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private void KingsEcho()
    {
        int energyStacks = GetAuraStacks("Residual Energy", true);

        if (Bot.Player.Mana < 28)
        {
            if (Cast(4))
                return;
        }

        if (energyStacks >= 22)
        {
            if (Cast(4))
                return;
        }

        if (Cast(1))
            return;

        if (Cast(2))
            return;
    }

    private void VoidHighLord()
    {
        if (IsHealthHigh(60))
        {
            if (Cast(3))
                return;
        }

        if (HasAura("Unshackled", true))
        {
            if (Cast(4))
                return;
        }

        if (IsHealthHigh(60))
        {
            if (Cast(1))
                return;
        }

        if (Cast(2))
            return;
    }

    private void BasicClass()
    {
        if (Cast(1))
            return;
        if (Cast(2))
            return;
        if (Cast(3))
            return;
        if (Cast(4))
            return;
    }

    private bool Cast(int index) =>
        index >= 1 && index <= 5
        && Bot.Skills != null
        && Bot.Skills.CanUseSkill(index)
        && TryUseSkill(index);

    private bool TryUseSkill(int index)
    {
        if (Bot.Skills == null)
            return false;

        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                Bot.Skills.UseSkill(index);
                return true;
            }
            catch
            {
                if (attempt == maxAttempts - 1)
                    return false;
                Bot.Sleep(50);
            }
        }

        return false;
    }

    private bool HasAura(string name, bool self = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return (self ? Bot.Self?.Auras : Bot.Target?.Auras)
            ?.Any(a => a != null && name.Equals(a.Name, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private float GetAuraStacksFloat(string auraName, bool self = true)
        => (self
            ? Bot.Self?.Auras?.FirstOrDefault(a => a?.Name == auraName)?.Value
            : Bot.Target?.Auras?.FirstOrDefault(a => a?.Name == auraName)?.Value) ?? 0f;

    private int GetAuraStacks(string auraName, bool self = true)
        => (int)Math.Round(GetAuraStacksFloat(auraName, self)) + 1;

    private int GetAuraSecondsRemaining(string auraName, bool self = true)
    {
        var aura = GetAuraByName(auraName, self);
        return aura != null && aura.UnixTimeStamp > 0 && aura.Duration > 0
            ? Math.Max(0, (int)(DateTimeOffset.FromUnixTimeMilliseconds(aura.UnixTimeStamp)
                                 .AddSeconds(aura.Duration) - DateTimeOffset.UtcNow).TotalSeconds)
            : 0;
    }

    private bool Stacks(string auraName, float quantity, bool self = true)
        => !string.IsNullOrWhiteSpace(auraName)
           && quantity > 0f
           && GetAuraStacksFloat(auraName, self) >= quantity;

    private bool Left(string auraName, int duration, bool self = true)
        => !string.IsNullOrWhiteSpace(auraName)
           && duration >= 0
           && GetAuraSecondsRemaining(auraName, self) <= duration;

    private Aura? GetAuraByName(string auraName, bool self = true)
    {
        if (string.IsNullOrWhiteSpace(auraName))
            return null;

        return (self ? Bot.Self?.Auras : Bot.Target?.Auras)
            ?.FirstOrDefault(a => a != null && auraName.Equals(a.Name, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsHealthHigh(int percent)
    {
        if (Bot.Player.MaxHealth <= 0)
            return false;

        return Bot.Player.Health * 100 / Bot.Player.MaxHealth >= percent;
    }
}
