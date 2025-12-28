using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BetterTames.MakeCommandable
{
    [HarmonyPatch(typeof(Player), "UpdateTeleport")]
    public class Player_UpdateTeleport_Patch
    {
        private static bool wasTeleporting = false;

        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            // Prüfe, ob der Teleport gerade beendet wurde
            if (wasTeleporting && !__instance.IsTeleporting())
            {
                BetterTamesPlugin.LogIfDebug("=== TeleportTo Postfix TRIGGERED for player ===", DebugFeature.MakeCommandable);

                if (__instance != Player.m_localPlayer) return;

                int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;
                if (maxPets == -1)
                {
                    BetterTamesPlugin.LogIfDebug("MaxPets == -1, skipping cleanup.", DebugFeature.MakeCommandable);
                    return;
                }

                float checkRadius = 64f;

                try
                {
                    BetterTamesPlugin.LogIfDebug("Attempting to start PostTeleportCleanupCoroutine...", DebugFeature.MakeCommandable);
                    Player.m_localPlayer.StartCoroutine(PostTeleportCleanupCoroutine(Player.m_localPlayer, maxPets, checkRadius));
                    BetterTamesPlugin.LogIfDebug("Started PostTeleportCleanupCoroutine successfully.", DebugFeature.MakeCommandable);
                }
                catch (System.Exception ex)
                {
                    BetterTamesPlugin.LogIfDebug($"Failed starting PostTeleportCleanupCoroutine: {ex.Message} | Stack: {ex.StackTrace}", DebugFeature.MakeCommandable);
                }
            }
            wasTeleporting = __instance.IsTeleporting();
        }


        private static IEnumerator PostTeleportCleanupCoroutine(Player player, int maxPets, float checkRadius)
        {
            BetterTamesPlugin.LogIfDebug("=== Coroutine STARTED ===", DebugFeature.MakeCommandable);

            float waited = 0f;
            float timeout = 8f;

            while (player.IsTeleporting() && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }


            yield return null;

            BetterTamesPlugin.LogIfDebug($"Coroutine waited {waited:F2}s. Player finished teleporting. Checking for pets now.", DebugFeature.MakeCommandable);
            if (player == null || player != Player.m_localPlayer) yield break;

            string playerName = player.GetPlayerName();
            var nearbyFollowers = new List<Character>();


            foreach (Collider col in Physics.OverlapSphere(player.transform.position, checkRadius))
            {
                Character c = col.GetComponent<Character>();
                if (c == null || !c.IsTamed()) continue;
                if (nearbyFollowers.Contains(c)) continue;

                bool isFollowing = false;
                MonsterAI monsterAI = c.GetComponent<MonsterAI>();
                if (monsterAI != null)
                {
                    try
                    {
                        var followTarget = monsterAI.GetFollowTarget();
                        if (followTarget != null && followTarget == player) isFollowing = true;
                    }
                    catch { }
                }
                if (!isFollowing)
                {
                    var zview = c.GetComponent<ZNetView>();
                    ZDO zdo = zview?.GetZDO();
                    if (zdo != null && zdo.IsValid() && zdo.GetString(ZDOVars.s_follow, "") == playerName)
                    {
                        isFollowing = true;
                    }
                }
                if (isFollowing) nearbyFollowers.Add(c);
            }

            BetterTamesPlugin.LogIfDebug($"PostTeleport: found {nearbyFollowers.Count} followers within {checkRadius}m (max {maxPets}).", DebugFeature.MakeCommandable);

            if (nearbyFollowers.Count <= maxPets) yield break;


            var sortedFollowers = nearbyFollowers
                .Where(c => c != null)
                .OrderBy(_ => UnityEngine.Random.value)
                .ToList();

            int numberToUnfollow = sortedFollowers.Count - maxPets;
            int clearedCount = 0;

            for (int i = 0; i < numberToUnfollow; i++)
            {
                Character follower = sortedFollowers[i];
                if (follower == null) continue;

                try
                {
                    Tameable tameable = follower.GetComponent<Tameable>();
                    MonsterAI monsterAI = follower.GetComponent<MonsterAI>();

                    if (tameable != null)
                    {

                        tameable.Command(player, true);


                        if (monsterAI?.GetFollowTarget() == null)
                        {
                            BetterTamesPlugin.LogIfDebug($"[Command] Successfully requested unfollow for {follower.GetHoverName()}.", DebugFeature.MakeCommandable);
                            clearedCount++;
                        }
                        else
                        {

                            BetterTamesPlugin.LogIfDebug($"[Command] Failed to set {follower.GetHoverName()} to 'Stay' immediately (Network/Authority).", DebugFeature.MakeCommandable);
                            clearedCount++;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    BetterTamesPlugin.LogIfDebug($"Error during cleanup of {follower?.name}: {ex}", DebugFeature.MakeCommandable);
                }
            }
        }
    }

}
