using RimWorld;
using System.Collections.Generic;
using Verse;

namespace QuestScheduler
{
    public static class PunishmentUtility
    {
        public static bool IsDangerousState(MentalStateDef mDef)
        {
            if (mDef == null) return false;
            string name = mDef.defName;
            return name.Contains("Manhunter") || name == "Berserk" || name == "MurderousRage" ||
                   name == "SocialFighting" || name == "Slaughterer" || name == "Jailbreaker" ||
                   name.Contains("Tantrum") || name.Contains("Rebellion") || name.Contains("Breakout");
        }

        public static void ExecutePunishment(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned) return;

            if (pawn.mutant != null) return;
            if (pawn.RaceProps != null && pawn.RaceProps.IsMechanoid) return;
            if (pawn.RaceProps != null && pawn.RaceProps.FleshType != null &&
                pawn.RaceProps.FleshType.defName.Contains("Entity")) return;

            bool isAnimal = pawn.RaceProps != null && pawn.RaceProps.Animal;

            // --- 新增核心邏輯：如果是透過你的自定義按鈕生成的獸群，無條件強制移除狂亂症 (Scaria) ---
            if (isAnimal && CustomRaidGenerator.isSpawningAnimal)
            {
                if (pawn.health != null && pawn.health.hediffSet != null)
                {
                    Hediff scaria = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Scaria);
                    if (scaria != null)
                    {
                        pawn.health.RemoveHediff(scaria);
                    }
                }
            }
            // ---------------------------------------------------------------------------------

            bool shouldStrip = (isAnimal && QuestSchedulerMod.settings.stripAnimals) || (!isAnimal && QuestSchedulerMod.settings.stripEnemies);
            bool shouldParalyze = (isAnimal && QuestSchedulerMod.settings.paralysisAnimals) || (!isAnimal && QuestSchedulerMod.settings.paralysisEnemies);

            if (shouldStrip)
            {
                pawn.equipment?.DestroyAllEquipment();
                pawn.apparel?.DestroyAll();
                pawn.inventory?.innerContainer?.ClearAndDestroyContents();
            }

            if (shouldParalyze)
            {
                if (pawn.health != null && pawn.health.hediffSet != null)
                {
                    List<Hediff> hediffsToRemove = new List<Hediff>();
                    foreach (var h in pawn.health.hediffSet.hediffs)
                    {
                        if (h.def == HediffDefOf.Scaria)
                        {
                            hediffsToRemove.Add(h);
                        }
                        else if (h.def.isBad)
                        {
                            string defNameLow = h.def.defName.ToLower();
                            if (!defNameLow.Contains("shambler") &&
                                !defNameLow.Contains("ghoul") &&
                                !defNameLow.Contains("mutant") &&
                                !defNameLow.Contains("entity"))
                            {
                                hediffsToRemove.Add(h);
                            }
                        }
                    }

                    foreach (var h in hediffsToRemove)
                    {
                        pawn.health.RemoveHediff(h);
                    }

                    if (!pawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
                    {
                        Hediff anes = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn, null);
                        anes.Severity = QuestSchedulerMod.settings.paralysisDays * 2.0f;
                        pawn.health.AddHediff(anes);
                    }
                }
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
            }
        }
    }
}