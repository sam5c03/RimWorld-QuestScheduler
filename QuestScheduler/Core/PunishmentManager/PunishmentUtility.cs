using RimWorld;
using System.Collections.Generic;
using System.Linq; // 確保加入此引用
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

            // --- 保留您原本的「特殊生物過濾」安全檢查 ---
            if (pawn.mutant != null) return; // 不處理突變體
            if (pawn.RaceProps != null && pawn.RaceProps.IsMechanoid) return; // 不處理機械族
            if (pawn.RaceProps != null && pawn.RaceProps.FleshType != null &&
                pawn.RaceProps.FleshType.defName.Contains("Entity")) return; // 不處理實體生物

            bool isAnimal = pawn.RaceProps != null && pawn.RaceProps.Animal;

            // --- 核心邏輯 A：自定義獸群處理 (包含溫馴開關) ---
            if (isAnimal && CustomRaidGenerator.isSpawningAnimal)
            {
                if (pawn.health != null && pawn.health.hediffSet != null)
                {
                    // 無條件移除狂亂症 (防止腐爛)
                    Hediff scaria = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Scaria);
                    if (scaria != null) pawn.health.RemoveHediff(scaria);
                }

                // 根據開關決定是否解除殺戮狀態
                if (CustomRaidGenerator.keepAnimalsTame)
                {
                    pawn.mindState?.mentalStateHandler?.Reset();
                    pawn.jobs?.StopAll();
                }

                // 自定義獸群處理完畢，跳過後面的全局處理
                return;
            }

            // --- 核心邏輯 B：全局制裁處理 (保留您原本的詳細過濾) ---
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
                            // --- 保留您原本針對特殊生物狀態的詳細過濾標籤 ---
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