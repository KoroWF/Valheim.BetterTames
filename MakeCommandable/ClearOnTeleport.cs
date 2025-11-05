using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BetterTames.Utils;
using HarmonyLib;
using UnityEngine;

namespace BetterTames.MakeCommandable
{
    [HarmonyPatch(typeof(Tameable), "Interact")]
    public static class ClearOnTeleport
    {
     [HarmonyPatch(typeof(Player), "TeleportTo")]
    public static class Player_TeleportTo_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
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

        private static IEnumerator PostTeleportCleanupCoroutine(Player player, int maxPets, float checkRadius)
        {
            BetterTamesPlugin.LogIfDebug("=== Coroutine STARTED ===", DebugFeature.MakeCommandable);

            float timeout = 8f; // Reduziert für schnelleres Testen
            float waited = 0f;
            yield return null;

            while ((ZRoutedRpc.instance == null || Object.FindObjectsOfType<ZNetView>().Length == 0) && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            BetterTamesPlugin.LogIfDebug($"Coroutine waited {waited:F2}s (timeout {timeout}s). Proceeding.", DebugFeature.MakeCommandable);

            if (player == null || player != Player.m_localPlayer) yield break;

            string playerName = player.GetPlayerName();
            var nearbyFollowers = new List<Character>();

            // Suche (unverändert)
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

            // Random Unfollow (wie gewünscht: OrderBy Random)
            var sortedFollowers = nearbyFollowers
                .Where(c => c != null)
                .OrderBy(_ => UnityEngine.Random.value) // Random-Auswahl
                .ToList();

            int numberToUnfollow = sortedFollowers.Count - maxPets;
            int clearedCount = 0;

            for (int i = 0; i < numberToUnfollow; i++)
            {
                Character follower = sortedFollowers[i];
                if (follower == null) continue;

                try
                {
                    MonsterAI monsterAI = follower.GetComponent<MonsterAI>();
                    ZNetView znetView = follower.GetComponent<ZNetView>();
                    ZDO zdo = znetView?.GetZDO();

                    if (zdo != null && zdo.IsValid())
                    {
                        if (zdo.IsOwner()) // Solo: Immer true
                        {
                            monsterAI?.SetFollowTarget(null);
                            zdo.Set(ZDOVars.s_follow, "");
                            BetterTamesPlugin.LogIfDebug($"[Owner] Unfollowed pet {follower.GetHoverName()} (cleared ZDO.s_follow).", DebugFeature.MakeCommandable);
                            clearedCount++;
                        }
                        else
                        {
                            long ownerId = (long)zdo.m_uid.UserID;
                            if (ZRoutedRpc.instance != null)
                            {
                                try
                                {
                                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REQUEST_UNFOLLOW, new object[] { zdo.m_uid.ToString() });
                                    BetterTamesPlugin.LogIfDebug($"Requested owner {ownerId} to unfollow pet {follower.GetHoverName()}.", DebugFeature.MakeCommandable);
                                    clearedCount++;
                                }
                                catch (System.Exception ex)
                                {
                                    BetterTamesPlugin.LogIfDebug($"Failed to invoke unfollow RPC for {follower.GetHoverName()}: {ex}", DebugFeature.MakeCommandable);
                                }
                            }
                            else
                            {
                                BetterTamesPlugin.LogIfDebug($"ZRoutedRpc null - cannot request unfollow for {follower.GetHoverName()}.", DebugFeature.MakeCommandable);
                            }
                        }
                    }
                    else
                    {
                        monsterAI?.SetFollowTarget(null);
                        BetterTamesPlugin.LogIfDebug($"Fallback: locally unfollowed {follower.GetHoverName()} (no ZDO).", DebugFeature.MakeCommandable);
                        clearedCount++;
                    }
                }
                catch (System.Exception ex)
                {
                    BetterTamesPlugin.LogIfDebug($"Error unfollowing {follower?.name}: {ex}", DebugFeature.MakeCommandable);
                }
            }

            BetterTamesPlugin.LogIfDebug($"=== Coroutine FINISHED: Cleared {clearedCount}/{numberToUnfollow} excess pets (random) ===", DebugFeature.MakeCommandable);
        }
    }
}