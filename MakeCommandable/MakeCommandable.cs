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

            // Nur der lokale Spieler soll diese Logik ausführen
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

                    // KORREKTUR: Hol dir den Spielernamen vom Player-Objekt
                    string playerName = player.GetPlayerName();

                    foreach (Character c in Character.GetAllCharacters())
                    {
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
                        user.Message(MessageHud.MessageType.Center, "Zu viele Begleiter folgen dir bereits.");
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