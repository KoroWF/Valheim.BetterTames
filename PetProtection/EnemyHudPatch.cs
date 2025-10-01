using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    public static class EnemyHud_TestShow_Patch
    {
        /// <summary>
        /// Dieser Postfix-Patch läuft, nachdem das Spiel entschieden hat, ob ein HUD angezeigt werden soll.
        /// Wir überschreiben das Ergebnis zu 'false', wenn das Tier von uns ausgeknockt wurde.
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

            // Prüfe über unsere sichere Methode, ob das Tier ausgeknockt ist.
            if (c.GetComponent<ZNetView>() is ZNetView nview && PetProtectionPatch.IsPetKnockedOut(nview.GetZDO().m_uid))
            {
                // Wenn ja, überschreibe das Ergebnis und zwinge das HUD zum Ausblenden.
                __result = false;
            }
        }
    }
}