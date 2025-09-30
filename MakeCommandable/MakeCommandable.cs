using HarmonyLib;

namespace BetterTames.MakeCommandable
{
    [HarmonyPatch(typeof(Tameable), "Interact")]
    public static class MakeCommandablePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            // Wenn 'hold' oder 'alt' gedrückt wird, führe die normale Spiel-Logik aus.
            if (hold || alt)
            {
                return true; // continue to original method
            }

            Character character = __instance.GetComponent<Character>();
            if (character == null || !character.IsTamed())
            {
                return true; // continue to original method
            }

            // Führe unseren Befehl aus und überspringe die normale Spiel-Logik.
            __instance.Command(user, true); // Schaltet Follow/Stay um

            MonsterAI monsterAI = __instance.GetComponent<MonsterAI>();
            string command = (monsterAI != null && monsterAI.GetFollowTarget() != null) ? "Follow" : "Stay";
            BetterTamesPlugin.LogIfDebug($"Command issued to {__instance.GetHoverName()}: {command}", DebugFeature.MakeCommandable);

            __result = true; // Signalisiert, dass die Interaktion erfolgreich war.
            return false; // skip original method
        }
    }
}