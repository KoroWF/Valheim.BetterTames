using HarmonyLib;
using UnityEngine;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class StunBehaviorPatches
    {
        /// <summary>
        /// Verhindert, dass die KI eines "ausgeknockten" Tieres ausgeführt wird.
        /// Stattdessen wird es gezwungen, liegen zu bleiben.
        /// Zusätzlich: Synchronisiert die Sichtbarkeit basierend auf dem ZDO-Flag "BT_TransformedToWisp"
        /// damit Side-Clients die Unsichtbarkeit korrekt übernehmen.
        /// </summary>
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        public static bool PreventAIUpdateWhenStunned(MonsterAI __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            // Ensure visibility state is synced on all clients based on the ZDO flag.
            SyncRenderersWithZDO(__instance);

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

        // Helper: sync renderers/colliders on clients according to ZDO flag "BT_TransformedToWisp".
        private static void SyncRenderersWithZDO(MonsterAI ai)
        {
            if (ai == null) return;

            ZNetView nview = ai.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return;

            bool transformed = zdo.GetBool("BT_TransformedToWisp", false);
            Character character = ai.GetComponent<Character>();
            if (character == null) return;

            bool desiredVisible = !transformed;

            // Check current renderer state to avoid toggling every frame unnecessarily.
            bool anyRendererEnabled = false;
            foreach (Renderer r in character.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && r.enabled)
                {
                    anyRendererEnabled = true;
                    break;
                }
            }

            if (anyRendererEnabled != desiredVisible)
            {
                ApplyRendererVisibility(character, desiredVisible);
            }
        }

        private static void ApplyRendererVisibility(Character character, bool visible)
        {
            if (character == null) return;

            // Toggle colliders
            foreach (Collider col in character.GetComponentsInChildren<Collider>(true))
            {
                if (col != null)
                {
                    col.enabled = visible;
                }
            }

            // Toggle renderers
            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}