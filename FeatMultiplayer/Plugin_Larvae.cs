using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {

        /// <summary>
        /// The vanilla game uses PlayerLarvaeAround::StartLarvaePlacement to setup some
        /// basic fields and start a coroutine of PlayerLarvaeAround::TryToSpawnLarvae.
        /// 
        /// On the host we have to completely replace this coroutine so that the spawning and
        /// clearing of larvae considers the client.
        /// 
        /// On the client, we don't allow this spawn routine to run at all.
        /// </summary>
        /// <param name="__instance">The current instance of the PlayerLarvaeAround.</param>
        /// <param name="___larvaesStart">A field.</param>
        /// <param name="___playerDirectEnvironment">A field.</param>
        /// <param name="___larvaesEnd">A field.</param>
        /// <param name="___worldUnitsHandler">A field.</param>
        /// <param name="___maxLarvaeToSpawn">A field.</param>
        /// <param name="___larvaesSpawned">A field.</param>
        /// <param name="___updateInterval">A field.</param>
        /// <param name="___radius">A field.</param>
        /// <param name="___larvaesToSpawn">A field.</param>
        /// <param name="___ignoredLayerMasks">A field.</param>
        /// <param name="___poolContainer">A field.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerLarvaeAround), nameof(PlayerLarvaeAround.StartLarvaePlacement))]
        static void PlayerLarvaeAround_StartLarvaePlacement(
            PlayerLarvaeAround __instance,
            TerraformStage ___larvaesStart,
            PlayerDirectEnvironment ___playerDirectEnvironment,
            TerraformStage ___larvaesEnd,
            WorldUnitsHandler ___worldUnitsHandler,
            int ___maxLarvaeToSpawn,
            List<GameObject> ___larvaesSpawned,
            float ___updateInterval,
            int ___radius,
            List<GroupDataItem> ___larvaesToSpawn,
            LayerMask ___ignoredLayerMasks,
            GameObject ___poolContainer
        )
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                LogInfo("Larvae; Radius = " + ___radius + ", Max spawn = " + ___maxLarvaeToSpawn);
                __instance.StopAllCoroutines();
                __instance.StartCoroutine(PlayerLarvaeAround_TryToSpawnLarvae_Override(
                    __instance,
                    ___larvaesStart, ___playerDirectEnvironment, ___larvaesEnd, ___worldUnitsHandler, ___maxLarvaeToSpawn,
                    ___larvaesSpawned, ___updateInterval, ___radius, ___larvaesToSpawn, ___ignoredLayerMasks, ___poolContainer
                ));
            }
            else
            if (updateMode == MultiplayerMode.CoopClient)
            {
                __instance.StopAllCoroutines();
            }
        }

        static IEnumerator PlayerLarvaeAround_TryToSpawnLarvae_Override(
            PlayerLarvaeAround __instance,
            TerraformStage ___larvaesStart,
            PlayerDirectEnvironment ___playerDirectEnvironment,
            TerraformStage ___larvaesEnd,
            WorldUnitsHandler ___worldUnitsHandler,
            int ___maxLarvaeToSpawn,
            List<GameObject> ___larvaesSpawned,
            float ___updateInterval,
            int ___radius,
            List<GroupDataItem> ___larvaesToSpawn,
            LayerMask ___ignoredLayerMasks,
            GameObject ___poolContainer
        )
        {
            for (; ; )
            {
                try
                {
                    if (playerLarvaeAroundNoLarvaeZoneEntered(__instance) <= 0
                        && ___worldUnitsHandler.IsWorldValuesAreBetweenStages(___larvaesStart, null) 
                        && !___playerDirectEnvironment.GetIsInLivable())
                    {
                        float num = Mathf.InverseLerp(___larvaesStart.GetStageStartValue(), ___larvaesEnd.GetStageStartValue(), ___worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue());

                        float maxLarvaeToSpawnScaled = ___maxLarvaeToSpawn * num;

                        var players = GetAllPlayerLocations();
                        var density = LarvaeDensity(___radius, maxLarvaeToSpawnScaled);
                        // LogInfo("Larvae; maxLarvaeToSpawnScaled = " + maxLarvaeToSpawnScaled + ", num = " + num);

                        if (ShouldSpawnMoreLarvae(___larvaesSpawned.Count, density, ___radius, players))
                        {
                            bool addLarvaesFromZone = ___worldUnitsHandler.IsWorldValuesAreBetweenStages(___larvaesEnd, null);

                            PlaceLarvae(___larvaesSpawned, ___radius, players, ___ignoredLayerMasks,
                                ___larvaesToSpawn, ___poolContainer, addLarvaesFromZone);
                        }
                        CleanFarAwayLarvae(___larvaesSpawned, 2 * ___radius, players);
                    }
                } 
                catch (Exception ex)
                {
                    LogError(ex);
                }
                yield return new WaitForSeconds(___updateInterval);
            }
        }

        struct XZ
        {
            internal int x;
            internal int z;
        }

        static List<XZ> cellsInCircle;

        static bool ShouldSpawnMoreLarvae(int currentCount, float maxDensity, int radius, List<Vector3> players)
        {
            /*
            var sw = new Stopwatch();
            sw.Start();
            */
            var radiusSquare = radius * radius;
            var delta = 4;
            float unitArea = delta * delta;

            if (cellsInCircle == null)
            {
                int r = 0;
                while (r < radius)
                {
                    r += delta;
                }
                cellsInCircle = new List<XZ>();
                for (int x = -r; x <= r; x += delta)
                {
                    for (int z = -r; z <= r; z += delta)
                    {
                        var distSquare = x * x + z * z;
                        if (distSquare < radiusSquare)
                        {
                            cellsInCircle.Add(new XZ { x = x, z = z });
                        }
                    }
                }
            }

            var cellSet = new HashSet<XZ>();

            for (int i = 0; i < players.Count; i++)
            {
                Vector3 player = players[i];
                int px = ((int)player.x / 4) * 4;
                int py = ((int)player.y / 4) * 4;
                foreach (var tempxz in cellsInCircle)
                {
                    var xz = new XZ { x = tempxz.x + px, z = tempxz.z + py };
                    cellSet.Add(xz);
                }
            }

            var area = cellSet.Count * unitArea;
            var currentDensity = currentCount / area;

            /*
            LogInfo("Larvae; cellSet = " + cellSet.Count + ", Spawn area = " + area + ", currentCount = " 
                + currentCount + ", currentDensity = " + currentDensity
                + ", maxDensity = " + maxDensity + ", t = " + sw.ElapsedTicks / 10000);
            */
            return currentDensity < maxDensity;
        }

        static float LarvaeDensity(float radius, float maxLarvaeToSpawn)
        {
            float area = radius * radius * Mathf.PI;
            return maxLarvaeToSpawn / area;
        }

        static List<Vector3> GetAllPlayerLocations()
        {
            List<Vector3> transforms = new();

            transforms.Add(GetPlayerMainController().transform.position);
            foreach (var otherPlayer in playerAvatars.Values)
            {
                transforms.Add(otherPlayer.rawPosition);
            }
            return transforms;
        }

        static List<GroupDataItem> GetValidSpawns(Vector3 position, List<GroupDataItem> ___larvaesToSpawn, 
            bool addLarvaesFromZone)
        {
            // the baseline spawn
            List<GroupDataItem> result = new(___larvaesToSpawn);

            // Since 0.5.006: Add zone spawns only after end of larvaes stage
            if (addLarvaesFromZone)
            {
                // zone specific spawn
                foreach (var lz in allLarvaeZones.Values)
                {
                    if (lz.bounds.Contains(position) && !lz.noSpawn)
                    {
                        result.AddRange(lz.spawns);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Exception to non-saveable group ids because of larvae.
        /// </summary>
        static readonly HashSet<string> larvaeGroupIds = new();

        static void PlaceLarvae(List<GameObject> ___larvaesSpawned, float ___radius, List<Vector3> players, 
            LayerMask ignoredLayerMasks, List<GroupDataItem> ___larvaesToSpawn, GameObject ___poolContainer,
            bool addLarvaesFromZone)
        {
            int playerIndex = UnityEngine.Random.Range(0, players.Count);
            Vector3 player = players[playerIndex];

            Vector2 randomInCircle = UnityEngine.Random.insideUnitCircle * (float)UnityEngine.Random.Range(1, ___radius);
            Vector3 playerRelative = new Vector3(player.x + randomInCircle.x, player.y, player.z + randomInCircle.y);
            Vector3 playerRelativeAbove = new Vector3(playerRelative.x, playerRelative.y + 6f, playerRelative.z);
            Vector3 downward = Vector3.up * -1f;

            if (Physics.Raycast(new Ray(playerRelativeAbove, downward), out var raycastHit, 10f, ignoredLayerMasks))
            {
                /* ???
                if (raycastHit.collider.gameObject == base.gameObject)
                {
                    return;
                }
                */
                /*
                foreach (var lp in ___larvaesToSpawn)
                {
                    LogInfo("       Spawn candidate: " + lp.id + " (" + Readable.GetGroupName(GroupsHandler.GetGroupViaId(lp.id)) + ") " + lp.chanceToSpawn + "%");
                }
                */
                var allowedSpawnsAt = GetValidSpawns(raycastHit.point, ___larvaesToSpawn, addLarvaesFromZone);
                var maxTries = 200;
                var tries = 0;
                while (allowedSpawnsAt.Count != 0 && tries < maxTries)
                {
                    int spawnIndex = UnityEngine.Random.Range(0, allowedSpawnsAt.Count);
                    GroupDataItem spawnCandidate = allowedSpawnsAt[spawnIndex];

                    if (spawnCandidate.chanceToSpawn >= UnityEngine.Random.Range(0, 100))
                    {
                        larvaeGroupIds.Add(spawnCandidate.id);

                        var larvaeGroup = GroupsHandler.GetGroupViaId(spawnCandidate.id);
                        var larvaeGo = WorldObjectsHandler.CreateAndInstantiateWorldObject(larvaeGroup, raycastHit.point, Quaternion.identity);

                        larvaeGo.transform.SetParent(___poolContainer.transform);

                        ___larvaesSpawned.Add(larvaeGo);
                        var larvaeWo = larvaeGo.GetComponentInChildren<WorldObjectAssociated>().GetWorldObject();
                        // FIXME work out the special case for larvae sync
                        larvaeWo.SetDontSaveMe(true);

                        float num = UnityEngine.Random.value * 360f;
                        Quaternion quaternion = Quaternion.Euler(0f, 0f, num);
                        Quaternion quaternion2 = Quaternion.LookRotation(raycastHit.normal) * (quaternion * Quaternion.Euler(90f, 0f, 0f));
                        larvaeGo.transform.rotation = quaternion2;
                        larvaeGo.transform.localScale = new Vector3(1f, 1f, 1f);
                        larvaeWo.SetPositionAndRotation(larvaeWo.GetPosition(), quaternion2);

                        LogInfo("Larvae; Spawning new [" + ___larvaesSpawned.Count + "] chance = " + spawnCandidate.chanceToSpawn + ", " + DebugWorldObject(larvaeWo));

                        SendWorldObjectToClients(larvaeWo, false);

                        break;
                    }
                    allowedSpawnsAt.RemoveAt(spawnIndex);
                    tries++;
                }
            }
        }

        static void CleanFarAwayLarvae(List<GameObject> ___larvaesSpawned, float ___radius, List<Vector3> players)
        {
            for (int i = ___larvaesSpawned.Count - 1; i >= 0; i--)
            {
                var larvae = ___larvaesSpawned[i];
                if (larvae == null)
                {
                    ___larvaesSpawned.RemoveAt(i);
                }
                else
                {
                    var inRange = false;
                    foreach (var pos in players)
                    {
                        var dist = Vector3.Distance(pos, larvae.transform.position);
                        if (dist < ___radius)
                        {
                            inRange = true;
                            break;
                        }
                    }

                    if (!inRange)
                    {
                        if (updateMode == MultiplayerMode.CoopHost)
                        {
                            var assocWo = larvae.GetComponent<WorldObjectAssociated>();
                            if (assocWo != null)
                            {
                                var wo = assocWo.GetWorldObject();
                                if (wo != null)
                                {
                                    LogInfo("Larvae; Removing " + DebugWorldObject(wo));
                                    wo.ResetPositionAndRotation();
                                    SendWorldObjectToClients(wo, false);

                                    WorldObjectsHandler.DestroyWorldObject(wo);
                                }
                            }
                            ___larvaesSpawned.RemoveAt(i);
                            Destroy(larvae);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The vanilla game calls PlayerLarvaeAround::OnTriggerEnter if it enters a 
        /// LarvaeZone's collider. It then adds the allowed spawns into the 
        /// PlayerLarvaeAround.larvaesToSpawn list.
        /// 
        /// In multiplayer mode, we don't care about entering/leaving these zones
        /// as we have to track and check every zone for every player all the time
        /// to have zone accurate spawns.
        /// </summary>
        /// <param name="__other">What object did we collide with?</param>
        /// <returns>True for singleplayer, false for multiplayer.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerLarvaeAround), "OnTriggerEnter")]
        static bool PlayerLarvaeAround_OnTriggerEnter()
        {
            return updateMode == MultiplayerMode.SinglePlayer;
        }

        /// <summary>
        /// The vanilla game calls PlayerLarvaeAround::OnTriggerExit if it leaves a 
        /// LarvaeZone's collider. It then removes the allowed spawns from the 
        /// PlayerLarvaeAround.larvaesToSpawn list.
        /// 
        /// In multiplayer mode, we don't care about entering/leaving these zones
        /// as we have to track and check every zone for every player all the time
        /// to have zone accurate spawns.
        /// </summary>
        /// <param name="__other">What object did we left with?</param>
        /// <returns>True for singleplayer, false for multiplayer.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerLarvaeAround), "OnTriggerExit")]
        static bool PlayerLarvaeAround_OnTriggerExit()
        {
            return updateMode == MultiplayerMode.SinglePlayer;
        }

        /// <summary>
        /// Make a copy of the LarvaeZone component as it is deleted when the player
        /// leaves the area.
        /// </summary>
        class LarvaeZoneCached
        {
            internal Bounds bounds;
            internal List<GroupDataItem> spawns;
            internal bool noSpawn;
        }

        /// <summary>
        /// Tracks all LarvaeZone instances.
        /// </summary>
        static Dictionary<string, LarvaeZoneCached> allLarvaeZones = new();

        /// <summary>
        /// In the vanilla game, when the LarvaeZone::Start is initialized by Unity,
        /// it simply sets up a layer mask.
        /// 
        /// On the host, we have to know about all larvae zones because we have to check
        /// which player is in which current zone and spawn only those larvae, that are permitted
        /// around each individual player (i.e., not globally).
        /// 
        /// On the client, we don't need this information.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LarvaeZone), "Start")]
        static void LarvaeZone_Start(LarvaeZone __instance)
        {
            var pool = __instance.GetLarvaesToAddToPool();
            var list = new List<string>();
            foreach (var p in pool)
            {
                list.Add(p.id);
            }
            var bounds = __instance.GetComponent<Collider>().bounds;
            var center = bounds.center;
            var extents = bounds.extents;
            var str = string.Join(",", (int)center.x, (int)center.y, (int)center.z,
                (int)extents.x, (int)extents.y, (int)extents.z);

            string info = "Larvae; Zone " + str + " [ " + string.Join(", ", list) + " ], NoSpawn = " + __instance.GetNoLarvaeZone();
            LogInfo(info);
            theLogger.LogInfo(info);
            // File.AppendAllLines(Application.persistentDataPath + "/larvae-dump.txt", new List<string>() { info });
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (!allLarvaeZones.ContainsKey(str))
                {
                    LarvaeZoneCached lzc = new();
                    lzc.bounds = bounds;
                    lzc.spawns = __instance.GetLarvaesToAddToPool();
                    lzc.noSpawn = __instance.GetNoLarvaeZone();
                    allLarvaeZones[str] = lzc;
                    foreach (var lp in lzc.spawns)
                    {
                        info = "       Spawn: " + lp.id + " (" + Readable.GetGroupName(GroupsHandler.GetGroupViaId(lp.id)) + ") " + lp.chanceToSpawn + "%";
                        LogInfo(info);
                        theLogger.LogInfo(info);
                        // File.AppendAllLines(Application.persistentDataPath + "/larvae-dump.txt", new List<string>() { info });
                    }
                }
            }
        }
    }
}
