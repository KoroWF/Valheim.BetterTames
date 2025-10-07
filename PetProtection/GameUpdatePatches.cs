using HarmonyLib;
using BetterTames.PetProtection;

namespace BetterTames.Patches
{
    [HarmonyPatch(typeof(ZNet), "Update")]
    public static class ZNet_Update_Patch
    {
        // Postfix bedeutet, dass unser Code direkt nach der originalen Update-Methode ausgeführt wird
        [HarmonyPostfix]
        public static void RunManagerUpdates()
        {
            // Hier rufen wir unsere statische Methode in jedem Frame auf
            StunnedPetManager.UpdateRevivalChecks();
        }
    }
}