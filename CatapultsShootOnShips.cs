/*
Catapults now shoot on ships for a more interesting naval warfare.

Author: cmjten10 (https://steamcommunity.com/id/cmjten10/)
Mod Version: 1
Target K&C Version: AI Kingdoms beta 8ds
Date: 2022-02-25
*/
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace CatapultsShootOnShips
{
    public class ModMain : MonoBehaviour 
    {
        private const string authorName = "cmjten10";
        private const string modName = "Catapults Shoot On Ships";
        private const string modNameNoSpace = "CatapultsShootOnShips";
        private const string version = "v1";
        private static string modId = $"{authorName}.{modNameNoSpace}";

        // Logging
        private static UInt64 logId = 0;
                            
        public static KCModHelper helper;

        void Preload(KCModHelper __helper) 
        {
            helper = __helper;
            var harmony = HarmonyInstance.Create(modId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        // Logger in the log box in game.
        private static void LogInGame(string message, KingdomLog.LogStatus status = KingdomLog.LogStatus.Neutral)
        {
            KingdomLog.TryLog($"{modId}-{logId}", message, status);
            logId++; 
        }

        private static void SetCatapultTarget(SiegeCatapult siegeCatapult, int teamId)
        {
            if (siegeCatapult.catapult.GetStatus() == Catapult.Status.Idle)
            {
                IMoveTarget target = null;
                Vector3 currPos = siegeCatapult.GetPos();
                int range = siegeCatapult.catapult.range;

                // Prioritize offense (e.g., sieging enemy kingdom)
                // Refer to ShipCatapultContainer::Update for FindTarget
                // Refer to SiegeCatapult::Tick for ignore function
                target = Catapult.FindTarget(siegeCatapult.catapult, 
                    new Catapult.ShouldIgnore(siegeCatapult.ShouldIgnore), currPos);
                
                // Defend if no offensive moves to be made
                if (target == null)
                {
                    // Refer to ArcherGeneral::Update
                    target = ProjectileDefense.GetTarget(currPos, 0f, range, teamId) as IMoveTarget;
                }

                if (target != null)
                {
                    siegeCatapult.catapult.SetTarget(target);
                    siegeCatapult.catapult.manned = true;
                }
                else
                {
                    siegeCatapult.catapult.SetTarget(null);
                    siegeCatapult.catapult.manned = false;
                }
            }
        }

        [HarmonyPatch(typeof(TroopTransportShip), "Tick")]
        public static class ShootOnShipsPatch
        {
            private static System.Collections.IEnumerable TransportShipSlotsBackToFront()
            {
                yield return 2;
                yield return 0;
                yield return 1;
            }

            static void Postfix(TroopTransportShip __instance, float dt, int currFrame, 
                ArrayExt<IMoveableUnit> ___loadTarget)
            {
                // Only the catapult nearest to the back of the ship can shoot
                foreach (int i in TransportShipSlotsBackToFront())
                {
                    if (i >= ___loadTarget.Count) 
                    {
                        continue;
                    }

                    IMoveableUnit unit = ___loadTarget.data[i];
                    if (unit is SiegeCatapult)
                    {
                        SetCatapultTarget(unit as SiegeCatapult, __instance.TeamID());
                        break;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SiegeCatapult), "Tick")]
        public static class CatapultAutorotateOnShipsPatch
        {
            static void Postfix(SiegeCatapult __instance, float dt, int currFrame, int ___teamId)
            {
                __instance.catapult.autoRotate = __instance.IsBeingCarried();

                // Could also implement setting target here, but catapult has no knowledge of transport ship or other
                // catapults on the same transport ship, so the number of catapults that can shoot cannot be limited. 
                // All 3 catapults shooting is too overpowered.

                /*if (__instance.IsBeingCarried())
                {
                    SetCatapultTarget(__instance, ___teamId);
                }*/
            }
        }
    }
}
