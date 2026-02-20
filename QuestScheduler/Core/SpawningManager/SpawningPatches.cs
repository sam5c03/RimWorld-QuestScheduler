using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace QuestScheduler
{
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_Generator
    {
        public static bool IsCustomSiteGeneration(int requestTile = -1)
        {
            if (CustomRaidGenerator.isSpawningCustomQuest) return true;
            int currentMapTile = Verse.MapGenerator.mapBeingGenerated?.Tile ?? -1;
            bool isCustom = (requestTile >= 0 && QuestSchedulerMod.settings.customSiteTiles.Contains(requestTile)) ||
                            (currentMapTile >= 0 && QuestSchedulerMod.settings.customSiteTiles.Contains(currentMapTile));
            return isCustom;
        }

        [HarmonyPrefix]
        public static void Prefix(ref PawnGenerationRequest request)
        {
            if (IsCustomSiteGeneration(request.Tile))
            {
                if (!request.FixedBiologicalAge.HasValue)
                    request.FixedBiologicalAge = Rand.Range(QuestSchedulerMod.settings.customQuestAgeMin, QuestSchedulerMod.settings.customQuestAgeMax);

                request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
                if (QuestSchedulerMod.settings.customQuestXenotype != null) request.ForcedXenotype = QuestSchedulerMod.settings.customQuestXenotype;

                if (!request.FixedGender.HasValue)
                    request.FixedGender = Rand.Value < (QuestSchedulerMod.settings.customQuestFemaleRatio / 100f) ? Gender.Female : Gender.Male;
            }
            else if (CustomRaidGenerator.isSpawningAnimal && !request.FixedBiologicalAge.HasValue)
            {
                request.FixedBiologicalAge = Rand.Range(1f, 10f);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
        {
            if (IsCustomSiteGeneration(request.Tile) && __result?.story != null)
            {
                if (request.KindDef != null && request.KindDef.factionLeader) return;

                if (QuestSchedulerMod.settings.forceCleanBackstories)
                {
                    __result.story.Childhood = DefDatabase<BackstoryDef>.AllDefs.Where(b => b.slot == BackstorySlot.Childhood && b.workDisables == WorkTags.None).RandomElementWithFallback();
                    __result.story.Adulthood = DefDatabase<BackstoryDef>.AllDefs.Where(b => b.slot == BackstorySlot.Adulthood && b.workDisables == WorkTags.None).RandomElementWithFallback();
                }

                __result.story.traits.allTraits.Clear();
                var valid = DefDatabase<TraitDef>.AllDefs.SelectMany(t => t.degreeDatas.Select(d => new TraitEntry(t, d))).Where(e => !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(e.uniqueKey)).ToList();
                int count = Rand.RangeInclusive(QuestSchedulerMod.settings.forcedTraitCountMin, QuestSchedulerMod.settings.forcedTraitCountMax);
                for (int i = 0; i < count && valid.Any(); i++)
                {
                    var e = valid.RandomElement();
                    __result.story.traits.GainTrait(new Trait(e.def, e.degree));
                    valid.RemoveAll(x => x.def == e.def);
                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnGroupMakerUtility), "GeneratePawns", new[] { typeof(PawnGroupMakerParms), typeof(bool) })]
    public static class Patch_ForcePawnCount
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
        {
            if (parms.tile >= 0 && QuestSchedulerMod.settings.customSiteTiles.Contains(parms.tile) && QuestSchedulerMod.settings.customQuestPawnCount > 0)
            {
                if (parms.faction != null && parms.faction.HostileTo(Faction.OfPlayer))
                {
                    List<Pawn> vanillaPawns = __result.ToList();
                    int targetCount = QuestSchedulerMod.settings.customQuestPawnCount;

                    Log.Message($"[QuestScheduler] 執行攔截：正在將 {vanillaPawns.Count} 名原版敵人替換為 {targetCount} 名自訂敵人。");

                    foreach (var p in vanillaPawns)
                    {
                        Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                    }

                    List<Pawn> newPawns = new List<Pawn>();
                    PawnKindDef genericKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mercenary_Gunner") ?? PawnKindDefOf.SpaceRefugee;

                    for (int i = 0; i < targetCount; i++)
                    {
                        PawnGenerationRequest req = new PawnGenerationRequest(
                            genericKind,
                            parms.faction,
                            context: PawnGenerationContext.NonPlayer,
                            tile: parms.tile,
                            forceGenerateNewPawn: true,
                            mustBeCapableOfViolence: true
                        );

                        req.FixedGender = Rand.Value < (QuestSchedulerMod.settings.customQuestFemaleRatio / 100f) ? Gender.Female : Gender.Male;
                        req.FixedBiologicalAge = Rand.Range(QuestSchedulerMod.settings.customQuestAgeMin, QuestSchedulerMod.settings.customQuestAgeMax);

                        if (QuestSchedulerMod.settings.customQuestXenotype != null)
                        {
                            req.ForcedXenotype = QuestSchedulerMod.settings.customQuestXenotype;
                        }

                        Pawn customPawn = PawnGenerator.GeneratePawn(req);
                        newPawns.Add(customPawn);
                    }

                    __result = newPawns;
                    Log.Message($"[QuestScheduler] 替換流程完成。");
                }
            }
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "GetFloatMenuOptions")]
    public static class Patch_ResearchMenu
    {
        [HarmonyPostfix]
        public static void Postfix(ThingWithComps __instance, ref IEnumerable<FloatMenuOption> __result)
        {
            if (__instance is Building_ResearchBench || __instance.def.defName.ToLower().Contains("researchbench"))
            {
                var opts = __result?.ToList() ?? new List<FloatMenuOption>();

                opts.Add(new FloatMenuOption("💎 按預設規格呼叫人礦 (一鍵)", () =>
                {
                    List<FloatMenuOption> fOpts = new List<FloatMenuOption>();
                    foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden))
                    {
                        fOpts.Add(new FloatMenuOption(fac.Name, () => CustomRaidGenerator.GenerateRaid(__instance.Map, fac, 5000f, QuestSchedulerMod.settings.presetXenotype, QuestSchedulerMod.settings.presetAgeMin, QuestSchedulerMod.settings.presetAgeMax, QuestSchedulerMod.settings.presetMaleRatio)));
                    }
                    Find.WindowStack.Add(new FloatMenu(fOpts));
                }));

                opts.Add(new FloatMenuOption("⚔️ 呼叫派系襲擊 (自訂規格)...", () =>
                {
                    List<FloatMenuOption> facOpts = new List<FloatMenuOption>();
                    foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden))
                    {
                        string label = $"{fac.Name} ({fac.PlayerRelationKind.GetLabel()})";
                        facOpts.Add(new FloatMenuOption(label, () => OpenPointsMenu(__instance.Map, fac)));
                    }
                    Find.WindowStack.Add(new FloatMenu(facOpts));
                }));

                opts.Add(new FloatMenuOption("🐾 呼叫癱瘓獸群...", () =>
                {
                    List<FloatMenuOption> animOpts = new List<FloatMenuOption>();
                    foreach (var a in DefDatabase<PawnKindDef>.AllDefs.Where(k => k.RaceProps.Animal).OrderBy(k => k.label))
                    {
                        animOpts.Add(new FloatMenuOption(a.LabelCap, () => OpenAnimalPointsMenu(__instance.Map, a), a.race.uiIcon, Color.white));
                    }
                    Find.WindowStack.Add(new FloatMenu(animOpts));
                }));

                __result = opts;
            }
        }

        private static void OpenPointsMenu(Map m, Faction f)
        {
            List<FloatMenuOption> pOpts = new List<FloatMenuOption>();
            float[] pts = { 1000f, 5000f, 10000f };
            foreach (float p in pts)
            {
                pOpts.Add(new FloatMenuOption($"{p:F0} 點數", () => Find.WindowStack.Add(new Dialog_RaidSettings(m, f, p))));
            }
            pOpts.Add(new FloatMenuOption("<b>自定義點數...</b>", () =>
            {
                Find.WindowStack.Add(new Dialog_RaidSettings(m, f, 3000f));
            }));
            Find.WindowStack.Add(new FloatMenu(pOpts));
        }

        private static void OpenAnimalPointsMenu(Map m, PawnKindDef animal)
        {
            List<FloatMenuOption> pOpts = new List<FloatMenuOption>();
            float[] pts = { 1000f, 5000f, 10000f };
            foreach (float p in pts)
            {
                pOpts.Add(new FloatMenuOption($"{p:F0} 點數", () => CustomRaidGenerator.GenerateAnimalRaid(m, animal, p)));
            }
            pOpts.Add(new FloatMenuOption("<b>自定義點數...</b>", () =>
            {
                CustomRaidGenerator.GenerateAnimalRaid(m, animal, 3000f);
            }));
            Find.WindowStack.Add(new FloatMenu(pOpts));
        }
    }
}