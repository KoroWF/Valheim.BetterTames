using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class StunBehaviorPatches
    {
        /// <summary>
        /// Verhindert, dass die KI eines "ausgeknockten" Tieres ausgeführt wird.
        /// Stattdessen wird es gezwungen, liegen zu bleiben.
        /// </summary>
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        public static bool PreventAIUpdateWhenStunned(MonsterAI __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            ZDO zdo = nview.GetZDO();
            if (zdo == null || !zdo.GetBool("isRecoveringFromStun", false))
            {
                return true; // Nicht im Schutzmodus, normale KI ausführen.
            }

            // Wenn im Schutzmodus:
            // 1. Sicherstellen, dass das Tier sich nicht bewegt.
            __instance.StopMoving();

            // 2. Das Tier in die "schlafend"-Animation zwingen.
            Character character = __instance.GetComponent<Character>();
            if (character != null)
            {
                character.GetZAnim()?.SetBool("sleeping", true);
            }

            // 3. Den Rest der normalen KI-Logik überspringen, um diesen Zustand beizubehalten.
            return false;
        }

        /// <summary>
        /// Verhindert, dass "ausgeknockte" Humanoide einen Angriff starten.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), "StartAttack")]
        [HarmonyPrefix]
        public static bool PreventAttackWhenStunned_Humanoid(Humanoid __instance)
        {
            if (!__instance.IsTamed()) return true;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && nview.GetZDO().GetBool("isRecoveringFromStun", false))
            {
                // Verhindere Angriffe, während das Tier am Boden ist.
                return false;
            }

            return true;
        }
    }
}