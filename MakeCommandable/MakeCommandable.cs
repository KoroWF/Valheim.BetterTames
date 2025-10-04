using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
            if (monsterAI != null && monsterAI.GetFollowTarget() == null)
            {
                int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;

                if (maxPets != -1)
                {
                    int currentFollowerCount = 0;
                    string playerName = player.GetPlayerName();

                    // NEU: Definiere einen Radius, in dem gesucht wird (z.B. 50 Meter)
                    // Diesen Wert könntest du auch in die Config auslagern!
                    float checkRadius = 64f;

                    // GEÄNDERT: Wir nutzen Physics.OverlapSphere anstatt Character.GetAllCharacters()
                    foreach (Collider col in Physics.OverlapSphere(player.transform.position, checkRadius))
                    {
                        // Hole die Charakter-Komponente vom gefundenen Objekt
                        Character c = col.GetComponent<Character>();

                        if (c != null && c.IsTamed())
                        {
                            // Die Logik zur Überprüfung des Followers bleibt gleich
                            if (c.GetComponent<ZNetView>()?.GetZDO().GetString(ZDOVars.s_follow, "") == playerName)
                            {
                                currentFollowerCount++;
                            }
                        }
                    }

                    if (currentFollowerCount >= maxPets)
                    {
                        user.Message(MessageHud.MessageType.Center, "Zu viele Begleiter in deiner Nähe folgen dir bereits.");
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

        [HarmonyPatch(typeof(Player), "TeleportTo")]
        public static class Player_TeleportTo_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                // Nur für den lokalen Spieler ausführen
                if (__instance != Player.m_localPlayer)
                {
                    return;
                }

                // Wir verwenden exakt die gleiche Logik wie im Interact-Patch
                int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;
                if (maxPets == -1) return;

                string playerName = __instance.GetPlayerName();
                float checkRadius = 50f; // Oder aus der Config

                List<Character> nearbyFollowers = new List<Character>();
                // Wichtig: Die Position von __instance nutzen, da wir gerade teleportiert sind
                foreach (Collider col in Physics.OverlapSphere(__instance.transform.position, checkRadius))
                {
                    Character c = col.GetComponent<Character>();
                    if (c != null && c.IsTamed() && c.GetComponent<ZNetView>()?.GetZDO().GetString(ZDOVars.s_follow, "") == playerName)
                    {
                        nearbyFollowers.Add(c);
                    }
                }

                // Korrektur-Logik: Wenn am Ankunftsort zu viele Tames warten...
                if (nearbyFollowers.Count > maxPets)
                {
                    __instance.Message(MessageHud.MessageType.Center, "Begleiter-Limit am Ankunftsort überschritten.");

                    var sortedFollowers = nearbyFollowers.OrderByDescending(c =>
                        Vector3.Distance(__instance.transform.position, c.transform.position)
                    ).ToList();

                    int numberToUnfollow = sortedFollowers.Count - maxPets;

                    for (int i = 0; i < numberToUnfollow; i++)
                    {
                        sortedFollowers[i].GetComponent<MonsterAI>()?.SetFollowTarget(null);
                    }
                }
            }
        }

    }
}