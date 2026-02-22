using RimWorld;
using System.Linq;
using Verse;

namespace QuestScheduler
{
    public static class CustomRaidGenerator
    {
        public static bool isSpawning = false;
        public static bool isSpawningAnimal = false;
        public static bool isSpawningCustomQuest = false;
        public static XenotypeDef forcedXeno;
        public static float minAge, maxAge;
        public static float targetFemaleRatio = 50f;
        public static float targetMaleRatio = 0.5f;

        // --- 新增：記錄是否保持溫馴的開關狀態 ---
        public static bool keepAnimalsTame = false;

        public static void GenerateRaid(Map m, Faction f, float p, XenotypeDef x, int min, int max, float mRatio)
        {
            isSpawning = true; forcedXeno = x; minAge = min; maxAge = max; targetMaleRatio = mRatio; targetFemaleRatio = (1f - mRatio) * 100f;
            try
            {
                var kind = f.def.pawnGroupMakers?.SelectMany(gm => gm.options).OrderBy(o => o.kind.combatPower).FirstOrDefault()?.kind ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Baseliner");
                IncidentParms parms = new IncidentParms { target = m, faction = f, points = p, pawnKind = kind };
                IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            }
            finally { isSpawning = false; }
        }

        public static void GenerateAnimalRaid(Map m, PawnKindDef a, float p, bool keepTame = false)
        {
            isSpawningAnimal = true;
            keepAnimalsTame = keepTame; // 記錄本次生成的溫馴設定
            try
            {
                IncidentParms parms = new IncidentParms { target = m, pawnKind = a, points = p };
                IncidentDefOf.ManhunterPack.Worker.TryExecute(parms);
            }
            finally
            {
                isSpawningAnimal = false;
                // 注意：這裡不立即重置 keepAnimalsTame，因為 SpawnSetup 後續還需要讀取它
            }
        }
    }
}