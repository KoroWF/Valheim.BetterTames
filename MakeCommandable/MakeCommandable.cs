using BetterTames;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterTames.MakeCommandable
{
    [HarmonyPatch(typeof(Tameable), "Interact")]
    public static class MakeCommandablePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            if (hold || alt) return true;

            Character character = __instance.GetComponent<Character>();
            if (character == null || !character.IsTamed()) return true;

            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer) return true;

            MonsterAI monsterAI = __instance.GetComponent<MonsterAI>();
            int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;

            bool willFollow = (monsterAI != null && monsterAI.GetFollowTarget() == null);  // Stay → Follow?

            if (maxPets == 0) return true;  // Keine Begleiter erlaubt, Original-Logik verwenden

            if (!willFollow)  // Wenn es schon folgt, einfach toggle zu Stay – kein Limit-Check nötig
            {
                __instance.Command(user, true);
                __result = true;
                return false;
            }

            // Command ausführen: Follow (immer erlauben, dann enforcen falls nötig)
            __instance.Command(user, true);

            BetterTamesPlugin.Instance.StartCoroutine(DelayedEnforce(player, 0.2f));  // Neuer Call

            __result = true;
            return false;

        }

        public static void EnforceFollowerLimitLocal(Player player)
        {
            int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;
            if (maxPets == -1) return;

            Vector3 playerPos = player.transform.position;
            string playerName = player.GetPlayerName();

            var followers = GetAndSortFollowers(player);

            if (followers.Count <= maxPets) return;

            int toUnfollow = followers.Count - maxPets;


            if (toUnfollow > 0)
            {
                player.Message(MessageHud.MessageType.Center, $"Max. nur '{followers.Count}/{maxPets}' erlaubt. Auto entfolgen vom weit entferntesten Tame.");
            }

            foreach (var far in followers.Take(toUnfollow))
            {
                if (far == null) continue;

                var ai = far.GetComponent<MonsterAI>();
                if (ai == null) continue;

                ai.SetFollowTarget(null);

                var zview = far.GetComponent<ZNetView>();
                if (zview != null)
                {
                    var zdo = zview.GetZDO();
                    if (zdo != null) zdo.Set(ZDOVars.s_follow, "");
                }

                BetterTamesPlugin.LogIfDebug($"{far.GetHoverName()} set to Stay locally to enforce limit for {playerName}.", DebugFeature.MakeCommandable);
            }


            // Collect the ZDOIDs of all pets to unfollow (the furthest `toUnfollow` pets).
            List<ZDOID> excessZDOs = followers
                .Take(toUnfollow)
                .Select(f => f.GetComponent<ZNetView>().GetZDO().m_uid)
                .ToList();


            if (!ZNet.instance.IsServer())
            {
                var playerView = player.GetComponent<ZNetView>();
                ZPackage pkg = new ZPackage();
                pkg.Write(excessZDOs.Count);
                foreach (var id in excessZDOs) pkg.Write(id);

                // Send RPC so server receives (server will process the package)
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REQUEST_ENFORCE_FOLLOWER_LIMIT, playerView.GetZDO().m_uid, pkg);

                BetterTamesPlugin.LogIfDebug($"[CLIENT] RPC sent to server for enforce: {excessZDOs.Count} excess pets.", DebugFeature.MakeCommandable);
            }
        }
        private static IEnumerator DelayedEnforce(Player player, float delay)
        {
            yield return new WaitForSeconds(delay);  // Warte ZDO-Sync

            // Optional: Local unfollow für Instant-Feedback (wie bisher)
            EnforceFollowerLimitLocal(player);  // Das triggert Scan + RPC

            BetterTamesPlugin.LogIfDebug($"[DELAYED] Enforced after {delay}s for {player.GetPlayerName()}", DebugFeature.MakeCommandable);
        }

        private static List<Character> GetAndSortFollowers(Player player)
        {
            int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;
            if (maxPets == -1) return new List<Character>();  // Early out

            Vector3 playerPos = player.transform.position;
            string playerName = player.GetPlayerName();
            float checkRadius = 64f;

            return Physics.OverlapSphere(playerPos, checkRadius)
                .Select(col => col.GetComponent<Character>())
                .Where(c => c != null && c.IsTamed())
                .Select(c => (Character: c, ZView: c.GetComponent<ZNetView>(), AI: c.GetComponent<MonsterAI>()))
                .Where(t => t.ZView != null && (
                    t.ZView.GetZDO().GetString(ZDOVars.s_follow, "") == playerName
                    || (t.AI != null && t.AI.GetFollowTarget() == player.gameObject)
                ))
                .OrderByDescending(t => Vector3.Distance(playerPos, t.Character.transform.position))
                .Select(t => t.Character)
                .ToList();
        }
    }
}