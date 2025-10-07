using HarmonyLib;
using BetterTames.PetProtection;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class StunBehaviorPatches
    {
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        public static bool PreventAIUpdateWhenStunned(MonsterAI __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            ZDO zdo = nview.GetZDO();
            if (zdo == null || !zdo.GetBool("BT_Stunned", false))
            {
                return true;  // Normale KI
            }

            // Stun: AI stoppen (lokal)
            __instance.StopMoving();
            Character character = __instance.GetComponent<Character>();
            if (character != null)
            {
                character.GetZAnim()?.SetBool("sleeping", true);
            }
            return false;  // Skip AI
        }

        [HarmonyPatch(typeof(Humanoid), "StartAttack")]
        [HarmonyPrefix]
        public static bool PreventAttackWhenStunned_Humanoid(Humanoid __instance)
        {
            if (!__instance.IsTamed()) return true;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && nview.GetZDO().GetBool("BT_Stunned", false))
            {
                return false;  // Kein Angriff
            }

            return true;
        }
    }
}