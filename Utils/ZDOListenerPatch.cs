using HarmonyLib;
using System;
using UnityEngine;
using BetterTames.PetProtection;

namespace BetterTames.Utils
{
    [HarmonyPatch(typeof(ZDO), "Set", new Type[] { typeof(string), typeof(bool) })]
    public static class ZDO_Set_Bool_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ZDO __instance, string name, bool value)
        {
            // Wir interessieren uns nur für unser Flag
            if (name != "BT_Stunned") return;

            ZNetView nview = ZNetScene.instance.FindInstance(__instance);
            if (nview == null) return;

            Character character = nview.GetComponent<Character>();
            if (character == null || !character.IsTamed()) return;

            // Sicherstellen, dass die Logik nicht doppelt ausgeführt wird
            bool currentValue = __instance.GetBool(name, !value);
            if (currentValue != value) return;

            // Logik basierend auf dem neuen Zustand von BT_Stunned
            if (value)
            {
                // Pet wurde betäubt -> Visuelle Transformation starten
                PetProtectionPatch.EnterStunVisuals(character);
                BetterTamesPlugin.LogIfDebug($"[ZDO Listener] Pet {character.m_name} entered stunned state visuals.", DebugFeature.PetProtection);
            }
            else
            {
                // Pet wurde wiederbelebt -> Visuelle Transformation rückgängig machen
                PetProtectionPatch.ExitStunVisuals(character);
                BetterTamesPlugin.LogIfDebug($"[ZDO Listener] Pet {character.m_name} exited stunned state visuals.", DebugFeature.PetProtection);
            }
        }
    }
}