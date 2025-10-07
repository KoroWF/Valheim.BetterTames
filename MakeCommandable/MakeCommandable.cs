using BetterTames;
using HarmonyLib;
using System;
using System.Collections.Generic;
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
            ZNetView zview = __instance.GetComponent<ZNetView>();
            if (zview == null || !zview.IsValid()) return true;

            ZDO zdo = zview.GetZDO();
            string currentFollow = zdo.GetString(ZDOVars.s_follow, "");

            int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;

            bool isCurrentlyFollowing = !string.IsNullOrEmpty(currentFollow) && currentFollow == player.GetPlayerName();
            bool willFollow = !isCurrentlyFollowing;

            BetterTamesPlugin.LogIfDebug($"[DEBUG] Pet: {__instance.GetHoverName()}, s_follow: '{currentFollow}', MonsterAI.FollowTarget: {(monsterAI?.GetFollowTarget()?.name ?? "null")}, willFollow: {willFollow}, IsOwner: {zview.IsOwner()}", DebugFeature.MakeCommandable);

            if (!willFollow)
            {
                __instance.Command(user, true);
                zdo.Set(ZDOVars.s_follow, "");
                if (monsterAI != null) monsterAI.SetFollowTarget(null);
                BetterTamesPlugin.UpdateFollowerCount(player, false);
                BetterTamesPlugin.LogIfDebug($"Command issued to {__instance.GetHoverName()}: Stay", DebugFeature.MakeCommandable);
                __result = true;
                return false;
            }

            // Follow-Check (Limit)
            List<Character> followers = GetAndSortFollowers(player);
            if (followers.Count >= maxPets)
            {
                // Unfollow fernstes
                if (followers.Count > 0)
                {
                    Character farthest = followers[0];
                    ZNetView farZview = farthest.GetComponent<ZNetView>();
                    if (farZview != null && farZview.IsValid() && (farZview.IsOwner() || ZNet.instance.IsServer()))
                    {
                        ZDO farZdo = farZview.GetZDO();
                        farZdo.Set(ZDOVars.s_follow, "");
                        MonsterAI farAI = farthest.GetComponent<MonsterAI>();
                        if (farAI != null) farAI.SetFollowTarget(null);
                        BetterTamesPlugin.UpdateFollowerCount(player, false);
                        BetterTamesPlugin.LogIfDebug($"Unfollowed farthest {farthest.GetHoverName()} to make room.", DebugFeature.MakeCommandable);
                    }
                }
                else
                {
                    user.Message(MessageHud.MessageType.Center, $"Follower-Scan fehlgeschlagen. Befiehl fernstes Pet manuell 'Stay'.");
                    __result = false;
                    return false;
                }
            }

            __instance.Command(user, true);
            zdo.Set(ZDOVars.s_follow, player.GetPlayerName());
            if (monsterAI != null) monsterAI.SetFollowTarget(player.gameObject);

            BetterTamesPlugin.UpdateFollowerCount(player, true);

            BetterTamesPlugin.LogIfDebug($"Command issued to {__instance.GetHoverName()}: Follow (new count: {BetterTamesPlugin.GetFollowerCount(player)})", DebugFeature.MakeCommandable);

            __result = true;
            return false;
        }

        private static List<Character> GetAndSortFollowers(Player player)
        {
            float checkRadius = 64f;
            List<Character> followers = new List<Character>(16);
            Collider[] cols = Physics.OverlapSphere(player.transform.position, checkRadius);
            foreach (Collider col in cols)
            {
                Character c = col.GetComponent<Character>();
                ZNetView zview = c?.GetComponent<ZNetView>();
                if (c != null && c.IsTamed() && zview != null && zview.GetZDO().GetString(ZDOVars.s_follow, "") == player.GetPlayerName())
                {
                    followers.Add(c);
                }
            }

            Character[] sorted = new Character[followers.Count];
            followers.CopyTo(sorted);
            Array.Sort(sorted, Comparer<Character>.Create((a, b) =>
                Vector3.Distance(player.transform.position, b.transform.position)
                .CompareTo(Vector3.Distance(player.transform.position, a.transform.position))));

            followers.Clear();
            followers.AddRange(sorted);
            return followers;
        }
    }
}