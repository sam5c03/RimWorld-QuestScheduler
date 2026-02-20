using RimWorld;
using System.Collections.Generic;
using Verse;

namespace QuestScheduler
{
    public enum SettingsTab { Quests, RaidPresets, CustomQuests }
    public enum RewardType { Item, Goodwill, Honor }

    public class TraitEntry
    {
        public readonly TraitDef def; public readonly int degree; public readonly string label; public readonly string description; public readonly string uniqueKey; public readonly TraitDegreeData data;
        public TraitEntry(TraitDef def, TraitDegreeData d)
        {
            this.def = def; this.degree = d.degree; this.label = d.label.CapitalizeFirst(); this.description = d.description; this.uniqueKey = $"{def.defName}|{d.degree}"; this.data = d;
        }
    }

    public class ScheduledQuest : IExposable
    {
        public string questDefName;
        public float intervalHours = 1.0f;
        public int nextTick = -1;
        public bool useCustomPoints = true;
        public float customPointsValue = 1000f;
        public void ExposeData()
        {
            Scribe_Values.Look(ref questDefName, "questDefName");
            Scribe_Values.Look(ref intervalHours, "intervalHours", 1.0f);
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            Scribe_Values.Look(ref useCustomPoints, "useCustomPoints", true);
            Scribe_Values.Look(ref customPointsValue, "customPointsValue", 1000f);
        }
    }

    public class CustomRewardData : IExposable
    {
        public RewardType type; public string thingDefName; public int amount = 1; public string factionDefName;
        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type"); Scribe_Values.Look(ref thingDefName, "thingDefName"); Scribe_Values.Look(ref amount, "amount", 1); Scribe_Values.Look(ref factionDefName, "factionDefName");
        }
    }

    public class QuestSchedulerSettings : ModSettings
    {
        public List<ScheduledQuest> activeSchedules = new List<ScheduledQuest>();
        public int presetAgeMin = 18; public int presetAgeMax = 60; public XenotypeDef presetXenotype = null; public float presetMaleRatio = 0.5f;
        public string customQuestDefName = "OpportunitySite_BanditCamp"; public float customQuestPoints = 1000f; public int customQuestAgeMin = 18; public int customQuestAgeMax = 60;
        public float customQuestFemaleRatio = 50f; public int customQuestTraitsMin = 1; public int customQuestTraitsMax = 3;
        public XenotypeDef customQuestXenotype = null; public int customQuestPawnCount = -1; public bool customQuestRequireSite = true;
        public bool useCustomRewards = false; public List<CustomRewardData> customRewards = new List<CustomRewardData>();
        public List<int> customSiteTiles = new List<int>();
        public bool stripEnemies = true; public bool stripAnimals = false; public bool paralysisEnemies = true; public bool paralysisAnimals = false; public float paralysisDays = 2.0f;
        public bool forceCleanBackstories = true; public int forcedTraitCountMin = 1; public int forcedTraitCountMax = 3; public List<string> blacklistedTraitKeys = new List<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref presetAgeMin, "presetAgeMin", 18); Scribe_Values.Look(ref presetAgeMax, "presetAgeMax", 60); Scribe_Values.Look(ref presetMaleRatio, "presetMaleRatio", 0.5f);
            Scribe_Values.Look(ref customQuestDefName, "customQuestDefName", "OpportunitySite_BanditCamp"); Scribe_Values.Look(ref customQuestPoints, "customQuestPoints", 1000f);
            Scribe_Values.Look(ref customQuestAgeMin, "customQuestAgeMin", 18); Scribe_Values.Look(ref customQuestAgeMax, "customQuestAgeMax", 60);
            Scribe_Values.Look(ref customQuestFemaleRatio, "customQuestFemaleRatio", 50f); Scribe_Values.Look(ref customQuestTraitsMin, "customQuestTraitsMin", 1);
            Scribe_Values.Look(ref customQuestTraitsMax, "customQuestTraitsMax", 3); Scribe_Values.Look(ref customQuestRequireSite, "customQuestRequireSite", true);
            Scribe_Values.Look(ref useCustomRewards, "useCustomRewards", false); Scribe_Values.Look(ref customQuestPawnCount, "customQuestPawnCount", -1);

            string cqXenoName = customQuestXenotype?.defName; Scribe_Values.Look(ref cqXenoName, "customQuestXenotypeName");
            if (Scribe.mode == LoadSaveMode.LoadingVars && !string.IsNullOrEmpty(cqXenoName)) customQuestXenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(cqXenoName);
            string xenoName = presetXenotype?.defName; Scribe_Values.Look(ref xenoName, "presetXenotypeName");
            if (Scribe.mode == LoadSaveMode.LoadingVars && !string.IsNullOrEmpty(xenoName)) presetXenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(xenoName);

            Scribe_Values.Look(ref stripEnemies, "stripEnemies", true); Scribe_Values.Look(ref stripAnimals, "stripAnimals", false);
            Scribe_Values.Look(ref paralysisEnemies, "paralysisEnemies", true); Scribe_Values.Look(ref paralysisAnimals, "paralysisAnimals", false);
            Scribe_Values.Look(ref paralysisDays, "paralysisDays", 2.0f); Scribe_Values.Look(ref forceCleanBackstories, "forceCleanBackstories", true);
            Scribe_Values.Look(ref forcedTraitCountMin, "forcedTraitCountMin", 1); Scribe_Values.Look(ref forcedTraitCountMax, "forcedTraitCountMax", 3);

            Scribe_Collections.Look(ref blacklistedTraitKeys, "blacklistedTraitKeys", LookMode.Value);
            Scribe_Collections.Look(ref activeSchedules, "activeSchedules", LookMode.Deep);
            Scribe_Collections.Look(ref customRewards, "customRewards", LookMode.Deep);
            Scribe_Collections.Look(ref customSiteTiles, "customSiteTiles", LookMode.Value);

            if (customSiteTiles == null) customSiteTiles = new List<int>();
            if (activeSchedules == null) activeSchedules = new List<ScheduledQuest>();
            if (blacklistedTraitKeys == null) blacklistedTraitKeys = new List<string>();
            if (customRewards == null) customRewards = new List<CustomRewardData>();
        }
    }
}