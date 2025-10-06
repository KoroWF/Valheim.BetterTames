using BetterTames;
using HarmonyLib;
using System;
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
            if (!willFollow)  // Wenn es schon folgt, einfach toggle zu Stay – kein Limit-Check nötig
            {
                __instance.Command(user, true);
                BetterTamesPlugin.UpdateFollowerCount(player, false);  // Nun -1 Follower
                BetterTamesPlugin.LogIfDebug($"Command issued to {__instance.GetHoverName()}: Stay (new count: {BetterTamesPlugin.GetFollowerCount(player)})", DebugFeature.MakeCommandable);
                __result = true;
                return false;
            }

            // Hier: willFollow == true → Wir wollen +1 Follower
            // Genauer Count via Scan (wie in Teleport-Patch)
            List<Character> followers = GetAndSortFollowers(player);
            int currentCount = followers.Count;

            BetterTamesPlugin.LogIfDebug($"Current followers: {currentCount}, willFollow: {willFollow}, max: {maxPets}", DebugFeature.MakeCommandable);

            // Wenn Limit erreicht/unterbrochen, unfollowe den fernsten, um Platz zu machen
            if (maxPets != -1 && currentCount >= maxPets)
            {
                if (followers.Count > 0)  // Es gibt welche zum Unfolgen
                {
                    Character farthest = followers[0];  // Erster ist der fernste (sortiert descending)
                    Tameable tameable = farthest.GetComponent<Tameable>();
                    if (tameable != null)
                    {
                        ZNetView zview = tameable.GetComponent<ZNetView>();
                        if (zview != null && zview.IsValid())
                        {
                            if (zview.IsOwner())
                            {
                                tameable.Command(player, true);  // Stay
                            }
                            else
                            {
                                long ownerId = zview.GetZDO().GetOwner();
                                if (ownerId != 0)
                                {
                                    ZRoutedRpc.instance.InvokeRoutedRPC(ownerId, BetterTamesPlugin.RPC_REQUEST_UNFOLLOW, zview.GetZDO().m_uid);
                                    BetterTamesPlugin.LogIfDebug($"Requesting owner {ownerId} to unfollow {tameable.name}", DebugFeature.MakeCommandable);
                                }
                            }
                            BetterTamesPlugin.LogIfDebug($"{farthest.GetHoverName()} bleibt hier zurück (Platz für neues Tier gemacht).", DebugFeature.MakeCommandable);
                            currentCount--;  // Nun -1
                        }
                    }
                }
                else
                {
                    // Fallback: Wenn kein Follower gefunden, aber Count war >= max → Cache-Fehler? Blocken mit Message
                    user.Message(MessageHud.MessageType.Center, $"Zu viele Begleiter ({currentCount}/{maxPets}). Befehle den fernsten zuerst 'Stay'.");
                    __result = false;
                    return false;
                }
            }

            // Command ausführen: Follow
            bool wasFollowing = (monsterAI != null && monsterAI.GetFollowTarget() != null);
            __instance.Command(user, true);  // Führt Follow aus

            // Update Liste nach Command (+1)
            BetterTamesPlugin.UpdateFollowerCount(player, true);

            string command = "Follow";
            BetterTamesPlugin.LogIfDebug($"Command issued to {__instance.GetHoverName()}: {command} (new count: {BetterTamesPlugin.GetFollowerCount(player)})", DebugFeature.MakeCommandable);

            __result = true;
            return false;
        }

        // Helper: Holt Followers, filtert und sortiert nach Distanz descending (fernste zuerst)
        private static List<Character> GetAndSortFollowers(Player player)
        {
            float checkRadius = 32f;
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

            // Sortiere descending nach Distanz (C# 7.3-kompatibel, ohne LINQ)
            Character[] sorted = new Character[followers.Count];
            followers.CopyTo(sorted);
            Array.Sort(sorted, Comparer<Character>.Create((a, b) =>
                Vector3.Distance(player.transform.position, b.transform.position)
                .CompareTo(Vector3.Distance(player.transform.position, a.transform.position))));  // Fernste zuerst

            followers.Clear();
            followers.AddRange(sorted);
            return followers;
        }
    }
}