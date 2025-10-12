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

            bool willFollow = true; // After teleport, we assume pets will try to follow

            // Enforce follower limit after teleport delay
            MakeCommandablePatch.EnforceFollowerLimitLocal(player);
        }
    }
}