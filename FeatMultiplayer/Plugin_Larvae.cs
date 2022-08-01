using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
		/*
		/// <summary>
		/// The vanilla PlayerLarveAround::TryToSpawnLarvae runs a coroutine that periodically checks
		/// the location of the player, then adds or removes larvae based on distance.
		/// 
		/// </summary>
		/// <param name="___larvaesStart"></param>
		/// <param name="___playerDirectEnvironment"></param>
		/// <param name="___larvaesEnd"></param>
		/// <param name="___worldUnitsHandler"></param>
		/// <param name="___maxLarvaeToSpawn"></param>
		/// <param name="___larvaesSpawned"></param>
		/// <param name="___updateInterval"></param>
		/// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerLarvaeAround), "TryToSpawnLarvae")]
        static IEnumerator PlayerLarvaeAround_TryToSpawnLarvae(
			TerraformStage ___larvaesStart,
			PlayerDirectEnvironment ___playerDirectEnvironment,
			TerraformStage ___larvaesEnd,
			WorldUnitsHandler ___worldUnitsHandler,
			int ___maxLarvaeToSpawn,
			List<GameObject> ___larvaesSpawned,
			float ___updateInterval,
			float ___radius
		)
        {
			for (; ; )
			{
				if (___worldUnitsHandler.IsWorldValuesAreBetweenStages(___larvaesStart, null) && !___playerDirectEnvironment.GetIsInLivable())
				{
					float num = Mathf.InverseLerp(___larvaesStart.GetStageStartValue(), ___larvaesEnd.GetStageStartValue(), ___worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue());
					if (ShouldSpawnMoreLarvae(___larvaesSpawned.Count, ___maxLarvaeToSpawn * num, ___radius))
					{
						PlaceLarvae();
					}
					CleanFarAwayLarvae();
				}
				yield return new WaitForSeconds(___updateInterval);
			}
		}

		static bool ShouldSpawnMoreLarvae(int ___larvaesSpawnedCount, float ___maxLarvaeToSpawnScaled, float radius)
        {
			return ___larvaesSpawnedCount < ___maxLarvaeToSpawnScaled;
        }

		static float LarvaeDensity(float minRadius, float maxRadius, int maxLarvaeToSpawn)
        {
			float area = (maxRadius - minRadius) * (maxRadius - minRadius) * Mathf.PI;
			return area / maxLarvaeToSpawn;
        }

		static void PlaceLarvae()
        {

        }

		static void CleanFarAwayLarvae()
        {

        }
		*/
    }
}
