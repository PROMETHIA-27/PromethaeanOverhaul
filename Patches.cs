using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PromethaeanOverhaul;

public static class Patches
{
    [HarmonyPatch(typeof(EnemyDirector), "Update")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> NoChaseOnEvac(IEnumerable<CodeInstruction> insts)
    {
        return new CodeMatcher(insts)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(EnemyDirector), "investigatePointTimer")),
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(OpCodes.Bgt_Un)
            )
            .Advance(2)
            .SetOperandAndAdvance(float.NegativeInfinity)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ItemGrenadeHuman), "Explosion")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> HumanGrenadeDamageBuff(IEnumerable<CodeInstruction> insts)
    {
        return new CodeMatcher(insts)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ParticleScriptExplosion), "Spawn"))
            )
            .Advance(-5)
            .SetOperandAndAdvance(150)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ItemGrenadeExplosive), "Explosion")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> GrenadeDamageBuff(IEnumerable<CodeInstruction> insts)
    {
        return new CodeMatcher(insts)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ParticleScriptExplosion), "Spawn"))
            )
            .Advance(-5)
            .SetOperandAndAdvance(250)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ItemGrenadeDuctTaped), "Explosion")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> DuctTapedGrenadeDamageBuff(IEnumerable<CodeInstruction> insts)
    {
        return new CodeMatcher(insts)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ParticleScriptExplosion), "Spawn"))
            )
            .Advance(-5)
            .SetOperandAndAdvance(150)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ItemMineExplosive), "OnTriggered")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> MineDamageBuff(IEnumerable<CodeInstruction> insts)
    {
        return new CodeMatcher(insts)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ParticleScriptExplosion), "Spawn"))
            )
            .Advance(-5)
            .SetOperandAndAdvance(350)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(EnemyDirector), "Awake")]
    [HarmonyPostfix]
    static void RemoveOverlyOppressiveMultipleSpawns(EnemyDirector __instance)
    {
        string[] toRemove = ["Floater", "Bowtie", "Upscream"];

        List<EnemySetup> removeSetups = [];

        for (int i = 0; i < __instance.enemiesDifficulty3.Count; i++)
        {
            var setup = __instance.enemiesDifficulty3[i];

            if (setup.name.Contains("Group") && toRemove.Any(setup.name.Contains))
            {
                removeSetups.Add(setup);
                Plugin.Logger.LogInfo($"Removed enemy setup `{setup.name}`");
            }
        }

        removeSetups.Select(__instance.enemiesDifficulty3.Remove);
    }

    [HarmonyPatch(typeof(PunManager), "UpdateEnergyRightAway")]
    [HarmonyPostfix]
    static void UpgradeEnergyRegen(PunManager __instance, string _steamID)
    {
        if (SemiFunc.PlayerAvatarGetFromSteamID(_steamID) == SemiFunc.PlayerAvatarLocal())
        {
            if (StatsManager.instance.playerUpgradeStamina.TryGetValue(_steamID, out int upgrades))
            {
                AccessTools
                    .Field(typeof(PlayerController), "sprintRechargeAmount")
                    .SetValue(PlayerController.instance, 2f + upgrades * 0.5f);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerController), "LateStart", MethodType.Enumerator)]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> RecalculateEnergyRegen(IEnumerable<CodeInstruction> insts)
    {
        return new CodeMatcher(insts)
            .MatchForward(false,
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(OpCodes.Add),
                new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(PlayerController), "EnergyStart"))
            )
            .Advance(3)
            .Insert(
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patches), "CalculateEnergyRegen"))
            )
            .InstructionEnumeration();
    }

    public static void CalculateEnergyRegen(string steamId)
    {
        Plugin.Logger.LogInfo("Calculating player stamina regen");

        int upgrades = StatsManager.instance.playerUpgradeStamina[steamId];
        AccessTools
            .Field(typeof(PlayerController), "sprintRechargeAmount")
            .SetValue(PlayerController.instance, 2f + upgrades * 0.5f);
    }

    [HarmonyPatch(typeof(EnemyRigidbody), "Awake")]
    [HarmonyPrefix]
    static void NoGrabStunFixed(EnemyRigidbody __instance)
    {
        if (__instance.enemy.Type != EnemyType.VeryLight)
        {
            __instance.grabStun = false;
        }
    }

    [HarmonyPatch(typeof(EnemyParent), "Despawn")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> NoMonsterRespawnAfterDeath(IEnumerable<CodeInstruction> insts)
    {
        return new CodeMatcher(insts)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(EnemyParent), "DespawnedTimer")),
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(EnemyParent), "DespawnedTimer"))
            )
            .RemoveInstructions(4)
            .Insert(
                new CodeInstruction(OpCodes.Ldc_R4, float.PositiveInfinity)
            )
            .InstructionEnumeration();
    }
}