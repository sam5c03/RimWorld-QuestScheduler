using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace QuestScheduler
{
    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class Patch_SpawnSetup
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || __instance == null) return;
            bool isCustomSpawn = CustomRaidGenerator.isSpawning || CustomRaidGenerator.isSpawningAnimal || CustomRaidGenerator.isSpawningCustomQuest;
            bool isHostile = __instance.Faction != null && __instance.Faction.HostileTo(Faction.OfPlayer);
            bool isManhunter = __instance.InAggroMentalState;
            if (__instance.Faction == Faction.OfPlayer && !isManhunter) return;
            if (isCustomSpawn || isHostile || isManhunter) PunishmentUtility.ExecutePunishment(__instance);
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), "TryStartMentalState")]
    public static class Patch_MentalStateTrigger
    {
        [HarmonyPostfix]
        public static void Postfix(MentalStateHandler __instance, bool __result, Pawn ___pawn, MentalStateDef stateDef)
        {
            if (__result && ___pawn != null && PunishmentUtility.IsDangerousState(stateDef))
                PunishmentUtility.ExecutePunishment(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class Patch_PredatorHuntTrigger
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_JobTracker __instance, Pawn ___pawn, Job newJob)
        {
            if (___pawn == null || newJob == null || newJob.def == null) return;
            if (newJob.def.defName == "PredatorHunt" && newJob.targetA.Thing is Pawn target && target.Faction == Faction.OfPlayer)
                PunishmentUtility.ExecutePunishment(___pawn);
        }
    }
}