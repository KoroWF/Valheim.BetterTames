using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    public static class EnemyHud_TestShow_Patch
    {
        /// <summary>
        /// Dieser Postfix-Patch läuft, nachdem das Spiel entschieden hat, ob ein HUD angezeigt werden soll.
        /// Wir überschreiben das Ergebnis zu 'false', wenn das Tier von uns ausgeknockt wurde oder ein Wisp ist.
        /// </summary>
        [HarmonyPostfix]
        public static void HideKnockedOutHud(Character c, ref bool __result)
        {
            // Wenn das Spiel bereits entschieden hat, das HUD nicht anzuzeigen, müssen wir nichts tun.
            if (!__result)
            {
                return;
            }

            // Wenn der Charakter kein gültiges, gezähmtes Tier ist, ignorieren wir ihn.
            if (c == null || !c.IsTamed())
            {
                return;
            }

            ZNetView nview = c.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                ZDOID zdoId = nview.GetZDO().m_uid;
                // Prüfe, ob das Tier ausgeknockt ist (lokal) oder in Wisp-Form ist (synchronisiert)
                if (PetProtectionPatch.IsPetKnockedOut(zdoId) || PetProtectionPatch.IsTransformedToWisp(zdoId))
                {
                    BetterTamesPlugin.LogIfDebug($"Hiding HUD for {c.m_name} (ZDOID: {zdoId}) due to Wisp or knocked out state.", DebugFeature.PetProtection);
                    __result = false; // Blende das HUD aus
                }
            }
        }
    }
}