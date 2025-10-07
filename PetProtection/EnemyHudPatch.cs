using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    public static class EnemyHud_TestShow_Patch
    {
        [HarmonyPostfix]
        public static void HideKnockedOutHud(Character c, ref bool __result)
        {
            if (!__result || c == null || !c.IsTamed())
            {
                return;
            }

            ZNetView nview = c.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                // VEREINFACHT: Prüft nur noch den Haupt-Status.
                if (PetProtectionPatch.IsPetKnockedOut(nview.GetZDO().m_uid))
                {
                    __result = false; // Blende das HUD aus
                }
            }
        }
    }
}