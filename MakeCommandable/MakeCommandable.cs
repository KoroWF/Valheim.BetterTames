using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BetterTames.Utils;
using HarmonyLib;
using UnityEngine;

namespace BetterTames.MakeCommandable
{
    [HarmonyPatch(typeof(Tameable), "Interact")]
    public static class MakeCommandablePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            if (hold || alt)
            {
                return true;
            }

            Character character = __instance.GetComponent<Character>();
            if (character == null || !character.IsTamed())
            {
                return true;
            }

            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer)
            {
                return true;
            }

            MonsterAI monsterAI = __instance.GetComponent<MonsterAI>();

            if (monsterAI != null)
            {
                // --- Änderung: prüfe zusätzlich die ZDO-Variable, weil auf Nicht-Owner-Clients monsterAI.GetFollowTarget() oft null ist ---
                ZNetView znetView = __instance.GetComponent<ZNetView>();
                ZDO zdo = znetView?.GetZDO();
                string playerName = player.GetPlayerName();

                bool aiSaysFollowing = monsterAI.GetFollowTarget() != null;
                bool zdoSaysFollowing = false;
                if (zdo != null && zdo.IsValid())
                {
                    // safe read; ZDO string enthält den Namen des Spielers, dem gefolgt wird
                    zdoSaysFollowing = zdo.GetString(ZDOVars.s_follow, "") == playerName;
                }

                if (aiSaysFollowing || zdoSaysFollowing)
                {
                    if (zdo.IsOwner())
                    {
                        // Owner kann autoritativ ändern
                        monsterAI.SetFollowTarget(null);
                        zdo.Set(ZDOVars.s_follow, "");
                        user.Message(MessageHud.MessageType.Center, __instance.GetHoverName() + " bleibt.");
                        BetterTamesPlugin.LogIfDebug($"Owner cleared follow for {__instance.GetHoverName()} (local).", DebugFeature.MakeCommandable);
                    }
                    else
                    {
                        // Nicht-Owner: fordere Owner per RPC auf, das Follow zu entfernen
                        long ownerId = (long)zdo.m_uid.UserID;
                        if (ZRoutedRpc.instance != null)
                        {
                            try
                            {
                                user.Message(MessageHud.MessageType.Center, __instance.GetHoverName() + " bleibt.");
                                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REQUEST_UNFOLLOW, new object[] { zdo.m_uid.ToString() });
                                BetterTamesPlugin.LogIfDebug($"Requested owner {ownerId} to unfollow pet {__instance.GetHoverName()} (zdo.s_follow matched).", DebugFeature.MakeCommandable);
                            }
                            catch (System.Exception ex)
                            {
                                BetterTamesPlugin.LogIfDebug($"Failed to invoke unfollow RPC for {__instance.GetHoverName()}: {ex}", DebugFeature.MakeCommandable);
                            }
                        }
                        else
                        {
                            BetterTamesPlugin.LogIfDebug($"ZRoutedRpc.instance is null - cannot request owner {ownerId} to unfollow pet {__instance.GetHoverName()}.", DebugFeature.MakeCommandable);
                        }
                    }

                    __result = true;
                    return false;
                }
            }

            if (monsterAI != null && monsterAI.GetFollowTarget() == null)
            {
                int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;

                if (maxPets != -1)
                {
                    int currentFollowerCount = 0;
                    string playerName = player.GetPlayerName();

                    // NEU: Definiere einen Radius, in dem gesucht wird (z.B. 64 Meter)
                    float checkRadius = 64f;

                    foreach (Collider col in Physics.OverlapSphere(player.transform.position, checkRadius))
                    {
                        Character c = col.GetComponent<Character>();

                        if (c != null && c.IsTamed())
                        {
                            if (c.GetComponent<ZNetView>()?.GetZDO().GetString(ZDOVars.s_follow, "") == playerName)
                            {
                                currentFollowerCount++;
                            }
                        }
                    }

                    if (currentFollowerCount >= maxPets)
                    {
                        user.Message(MessageHud.MessageType.Center, "Zu viele Begleiter in deiner Nähe folgen dir bereits. Maximal erlaubt: " + maxPets);
                        __result = true;
                        return false;
                    }
                }
            }

            __instance.Command(user, true);

            string command = (monsterAI != null && monsterAI.GetFollowTarget() != null) ? "Follow" : "Stay";
            BetterTamesPlugin.LogIfDebug($"Command issued to {__instance.GetHoverName()}: {command}", DebugFeature.MakeCommandable);

            __result = true;
            return false;
        }
    }
}