using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch(typeof(Tameable), "Interact")]
    public static class WispInteractPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Tameable __instance, Humanoid user)
        {
            if (__instance.gameObject.name.Contains("LuredWisp"))  // Oder prüfe Parent/Tag
            {
                return false;  // Blockiere Interact komplett
            }
            return true;
        }
    }
}