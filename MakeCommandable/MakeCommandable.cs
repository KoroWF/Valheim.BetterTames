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

            if (user is not Player player || player != Player.m_localPlayer)
            {
                return true;
            }

            Character character = __instance.GetComponent<Character>();
            MonsterAI monsterAI = __instance.GetComponent<MonsterAI>();
            ZNetView znetView = __instance.GetComponent<ZNetView>();

            if (character == null || !character.IsTamed() || monsterAI == null || znetView == null || !znetView.IsValid())
            {
                return true;
            }

            ZDO zdo = znetView.GetZDO();
            string petName = __instance.GetHoverName();
            string playerName = player.GetPlayerName();

            bool zdoSaysFollowing = zdo.GetString(ZDOVars.s_follow, "") == playerName;

            if (!zdoSaysFollowing)
            {

                int maxPets = BetterTamesPlugin.ConfigInstance.Tames.MaxFollowingPets.Value;

                if (maxPets != -1)
                {
                    if (CheckMaxFollowerLimit(player, maxPets))
                    {
                        user.Message(MessageHud.MessageType.Center, $"Too many companions. Maximum allowed: {maxPets}");
                        __result = true;
                        return false;
                    }
                }
            }

            __instance.Command(user, true);

            string finalFollowStatus = zdo.GetString(ZDOVars.s_follow, "") == playerName ? "follow." : "stay.";

            BetterTamesPlugin.LogIfDebug($"Command issued to {petName}: {finalFollowStatus}", DebugFeature.MakeCommandable);

            __result = true;
            return false;
        }

        /// <summary>
        /// Prüft, wie viele Tiere dem Spieler bereits in einem Umkreis von 64m folgen.
        /// </summary>
        private static bool CheckMaxFollowerLimit(Player player, int maxPets)
        {
            if (maxPets <= 0) return false;

            int currentFollowerCount = 0;
            string playerName = player.GetPlayerName();
            float checkRadius = 64f;


            foreach (Collider col in Physics.OverlapSphere(player.transform.position, checkRadius))
            {
                Character c = col.GetComponent<Character>();

                if (c != null && c.IsTamed() && c.GetComponent<ZNetView>()?.GetZDO() is ZDO petZdo)
                {

                    if (petZdo.GetString(ZDOVars.s_follow, "") == playerName)
                    {
                        currentFollowerCount++;
                    }
                }
            }

            return currentFollowerCount >= maxPets;
        }
    }
}