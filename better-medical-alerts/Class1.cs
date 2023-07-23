using RimWorld;
using Verse;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Linq;

namespace better_medical_alerts
{
    [StaticConstructorOnStartup]
    public static class BetterMedicalALerts
    {
        static BetterMedicalALerts()
        {
            Harmony.DEBUG = true;

            var harmony = new Harmony("com.example.patch");
            harmony.PatchAll();
        }
    }

    public class Alert_BloodLoss : Alert_Critical
        {
            private List<Pawn> sickPawnsResult = new List<Pawn>();

            private List<Pawn> SickPawns
            {
                get
                {
                    sickPawnsResult.Clear();
                    foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonistsAndPrisoners_NoCryptosleep)
                    {
                        if (ModsConfig.BiotechActive && (item.Deathresting || (item.genes != null && item.genes.HasGene(GeneDefOf.Deathless))))
                        {
                            continue;
                        }
                        for (int i = 0; i < item.health.hediffSet.hediffs.Count; i++)
                        {
                            Hediff hediff = item.health.hediffSet.hediffs[i];
                            if (hediff.CurStage != null && hediff.def == HediffDefOf.BloodLoss && !hediff.FullyImmune())
                            {
                                //if (item.health.hediffSet.BleedRateTotal > 0.05F)
                                if (HealthUtility.TicksUntilDeathDueToBloodLoss(item) < 20000)
                                {
                                    sickPawnsResult.Add(item);
                                    break;
                                }
                            }
                        }
                    }
                    return sickPawnsResult;
                }
            }

            public override string GetLabel()
            {
                return "PawnsBleedingOut".Translate();
            }

            public override TaggedString GetExplanation()
            {
                StringBuilder stringBuilder = new StringBuilder();
                bool flag = false;
                foreach (Pawn sickPawn in SickPawns)
                {
                    stringBuilder.AppendLine("  - " + sickPawn.NameShortColored.Resolve());
                    foreach (Hediff hediff in sickPawn.health.hediffSet.hediffs)
                    {
                        if (hediff.CurStage != null && hediff.CurStage.lifeThreatening && hediff.Part != null && hediff.Part != sickPawn.RaceProps.body.corePart)
                        {
                            flag = true;
                            break;
                        }
                    }
                }
                if (flag)
                {
                    return "PawnsBleedingOutDesc".Translate(stringBuilder.ToString());
                }
                return "PawnsBleedingOutDesc".Translate(stringBuilder.ToString());
            }

            public override AlertReport GetReport()
            {
                return AlertReport.CulpritsAre(SickPawns);
            }
    }

    public class Alert_InfectionDeadly : Alert_Critical
    {
        private List<Pawn> sickPawnsResult = new List<Pawn>();

        private List<Pawn> SickPawns
        {
            get
            {
                sickPawnsResult.Clear();
                foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonistsAndPrisoners_NoCryptosleep)
                {
                    if (ModsConfig.BiotechActive && (item.Deathresting || (item.genes != null && item.genes.HasGene(GeneDefOf.Deathless))))
                    {
                        continue;
                    }
                    for (int i = 0; i < item.health.hediffSet.hediffs.Count; i++)
                    {
                        Hediff hediff = item.health.hediffSet.hediffs[i];
                        if (hediff.CurStage != null && hediff.def == HediffDefOf.WoundInfection && !hediff.FullyImmune())
                        {
                            HediffComp_Immunizable im = hediff.TryGetComp<HediffComp_Immunizable>();
                            if (im != null)
                            {
                                var ir = im.Pawn.health.immunity.GetImmunityRecord(im.Def);
                                if (ir != null)
                                {
                                    float immunity_per_tick = ir.ImmunityChangePerTick(item, sick: true, hediff);
                                    float severity_per_tick = im.SeverityChangePerDay() / GenDate.TicksPerDay;

                                    int remaining_immunity_ticks = (int)((1.0F - ir.immunity)/ immunity_per_tick);
                                    int remaining_severity_ticks = (int)((1.0F - hediff.Severity) / severity_per_tick);


                                    //Log.Message("ticks remaining til 1.0 immunity: " + remaining_immunity_ticks.ToString("0"));
                                    //Log.Message("ticks remaining til 1.0 severity: " + remaining_severity_ticks.ToString("0"));

                                    if (remaining_immunity_ticks > remaining_severity_ticks)
                                    {
                                        sickPawnsResult.Add(item);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                return sickPawnsResult;
            }
        }

        public override string GetLabel()
        {
            return "PawnsInfectedDeadly".Translate();
        }

        public override TaggedString GetExplanation()
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool flag = false;
            foreach (Pawn sickPawn in SickPawns)
            {
                stringBuilder.AppendLine("  - " + sickPawn.NameShortColored.Resolve());
                foreach (Hediff hediff in sickPawn.health.hediffSet.hediffs)
                {
                    if (hediff.CurStage != null && hediff.CurStage.lifeThreatening && hediff.Part != null && hediff.Part != sickPawn.RaceProps.body.corePart)
                    {
                        flag = true;
                        break;
                    }
                }
            }
            if (flag)
            {
                return "+".Translate(stringBuilder.ToString());
            }
            return "PawnsInfectedDeadlyDesc".Translate(stringBuilder.ToString());
        }

        public override AlertReport GetReport()
        {
            return AlertReport.CulpritsAre(SickPawns);
        }
    }



    [HarmonyPatch(typeof(Alert_LifeThreateningHediff))]
    [HarmonyPatch(nameof(Alert_LifeThreateningHediff.GetReport))] //annotation boiler plate to tell Harmony what to patch. Refer to docs.
    static class Patch
    {

        static void Postfix(ref AlertReport __result, Alert_LifeThreateningHediff __instance, List<Pawn> ___sickPawnsResult) //pass the __result by ref to alter it.
        {
            //Log.Message("WHEEEEEE");

            ___sickPawnsResult.Clear();
            foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonistsAndPrisoners_NoCryptosleep)
            {
                if (ModsConfig.BiotechActive && (item.Deathresting || (item.genes != null && item.genes.HasGene(GeneDefOf.Deathless))))
                {
                    continue;
                }
                for (int i = 0; i < item.health.hediffSet.hediffs.Count; i++)
                {
                    Hediff hediff = item.health.hediffSet.hediffs[i];
                    if (hediff.CurStage != null && !hediff.FullyImmune() && hediff.CurStage.lifeThreatening)
                    {
                        if (hediff.def == HediffDefOf.BloodLoss || hediff.def == HediffDefOf.WoundInfection)
                        {
                            //do nothing
                        }
                        else
                        {
                            if (hediff.CurStage.lifeThreatening)
                            {
                                ___sickPawnsResult.Add(item);
                                break;
                            }
                        }
                    }
                }
            }

            __result = AlertReport.CulpritsAre(___sickPawnsResult);
        }
    }

    //Unfinished transpiler version of emergency medical alert disable
    /*
    [HarmonyPatch(typeof(Alert_LifeThreateningHediff))]
    [HarmonyPatch("SickPawns", MethodType.Getter)]
    public static class Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //var foundMassUsageMethod = false;
            var insertionIndex = -1;
            //var endIndex = -1;

            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Blt)
                {
                    Log.Error("START " + (i + 1));

                    insertionIndex = i;
                }
            }
            if (insertionIndex > -1)
            {
                //codes[startIndex+1].opcode = OpCodes.Nop;
                codes.Insert(insertionIndex, new CodeInstruction(OpCodes.Blt));
                //codes.RemoveRange(startIndex + 1, endIndex - startIndex - 1);
            }

            return codes.AsEnumerable();
        }
    }*/
}