using BetterTames;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterTames.MakeCommandable
{
    [HarmonyPatch(typeof(Player), "TeleportTo")]
    public static class Player_TeleportTo_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }

            BetterTamesPlugin.Instance.StartCoroutine(DelayedFollowerCheck(__instance, 10f));
        }

        internal static IEnumerator DelayedFollowerCheck(Player player, float delay)
        {
            yield return new WaitForEndOfFrame();


            yield return new WaitForSeconds(delay);

            int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;
            if (maxPets == -1) yield break;

            string playerName = player.GetPlayerName();
            float checkRadius = 64f;  // Reduziert

            List<Character> followers = new List<Character>(16);  // Pre-allocate
            Collider[] cols = Physics.OverlapSphere(player.transform.position, checkRadius);
            foreach (Collider col in cols)
            {
                Character c = col.GetComponent<Character>();  // Korrekt: GetComponent
                ZNetView zview = c?.GetComponent<ZNetView>();  // Korrekt: GetComponent
                if (c != null && c.IsTamed() && zview != null && zview.GetZDO().GetString(ZDOVars.s_follow, "") == playerName)
                {
                    followers.Add(c);
                }
            }

            if (followers.Count > maxPets)
            {
                BetterTamesPlugin.LogIfDebug($"[DelayedFollowerCheck] {followers.Count} followers found, limit is {maxPets}. Correcting...", DebugFeature.MakeCommandable);

                // Manuelle Sortierung statt LINQ (für C# 7.3-kompatibel)
                Character[] sorted = new Character[followers.Count];
                followers.CopyTo(sorted);
                Array.Sort(sorted, Comparer<Character>.Create((a, b) =>
                    Vector3.Distance(player.transform.position, b.transform.position)
                    .CompareTo(Vector3.Distance(player.transform.position, a.transform.position))));  // Descending

                int numberToUnfollow = sorted.Length - maxPets;
                for (int i = 0; i < numberToUnfollow; i++)
                {
                    Tameable tameable = sorted[i].GetComponent<Tameable>();
                    if (tameable != null)
                    {
                        ZNetView zview = tameable.GetComponent<ZNetView>();
                        if (zview != null && zview.IsValid() && (zview.IsOwner() || ZNet.instance.IsServer()))
                        {
                            ZDO zdo = zview.GetZDO();
                            zdo.Set(ZDOVars.s_follow, "");

                            MonsterAI ai = sorted[i].GetComponent<MonsterAI>();
                            if (ai != null) ai.SetFollowTarget(null);

                            BetterTamesPlugin.LogIfDebug($"{sorted[i].GetHoverName()} unfollowed via ZDO.", DebugFeature.MakeCommandable);
                        }
                        // Entferne else { RPC-Fallback } – ZDO reicht
                    }

                    BetterTamesPlugin.LogIfDebug($"{sorted[i].GetHoverName()} bleibt hier zurück.", DebugFeature.MakeCommandable);
                }
            }
        }
    }
}