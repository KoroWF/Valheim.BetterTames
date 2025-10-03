using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class ButcherKnifePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance, HitData hit)
        {
            // Debug: Bestätige, dass der Patch ausgeführt wird
            BetterTamesPlugin.LogIfDebug("ButcherKnifePatch Prefix called for character: " + (__instance != null ? __instance.m_name : "null"), DebugFeature.PetProtection);

            // Prüfe, ob die Bedingungen für den ButcherKnife-Bypass erfüllt sind
            if (CheckButcherKnifeBypass(__instance, hit))
            {
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    ZDOID targetZDOID = nview.GetZDO().m_uid;
                    BetterTamesPlugin.LogIfDebug($"Butcher Knife used on {__instance.m_name} (ZDOID: {targetZDOID}). IsOwner: {nview.IsOwner()}", DebugFeature.PetProtection);

                    // Setze die Flag lokal, wenn du der Owner bist
                    if (nview.IsOwner())
                    {
                        BetterTamesPlugin.LogIfDebug($"Owner setting BT_MercyKill flag for {__instance.m_name} locally.", DebugFeature.PetProtection);
                        nview.GetZDO().Set("BT_MercyKill", true);
                    }
                    else
                    {
                        // Sende RPC an alle, wenn ein anderer Client angreift
                        BetterTamesPlugin.LogIfDebug($"Non-owner sending MercyKill RPC for ZDOID: {targetZDOID} to all clients.", DebugFeature.PetProtection);
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REQUEST_MERCY_KILL, targetZDOID);
                        BetterTamesPlugin.LogIfDebug($"MercyKill RPC sent to all for ZDOID: {targetZDOID}.", DebugFeature.PetProtection);
                    }

                    // Lass den Schaden durch, die Flag übernimmt den Bypass
                    return true;
                }
                else
                {
                    BetterTamesPlugin.LogIfDebug("ZNetView is null or invalid, cannot process MercyKill.", DebugFeature.PetProtection);
                }
            }

            return true; // Normaler Schadensfluss, wenn kein ButcherKnife
        }

        private static bool CheckButcherKnifeBypass(Character character, HitData hit)
        {
            BetterTamesPlugin.LogIfDebug("Checking ButcherKnife bypass conditions...", DebugFeature.PetProtection);
            Character attacker = hit.GetAttacker();
            BetterTamesPlugin.LogIfDebug($"Attacker: {attacker != null}, IsPlayer: {attacker?.IsPlayer() ?? false}", DebugFeature.PetProtection);

            if (attacker != null && attacker.IsPlayer())
            {
                Player playerAttacker = (Player)attacker;
                ItemDrop.ItemData currentWeapon = playerAttacker.GetCurrentWeapon();
                BetterTamesPlugin.LogIfDebug($"CurrentWeapon: {currentWeapon != null}, Name: {currentWeapon?.m_dropPrefab.name ?? "null"}", DebugFeature.PetProtection);

                if (currentWeapon != null && currentWeapon.m_dropPrefab.name == "KnifeButcher")
                {
                    BetterTamesPlugin.LogIfDebug($"IsTamed check for {character.m_name}: {character.IsTamed()}", DebugFeature.PetProtection);
                    if (character.IsTamed())
                    {
                        BetterTamesPlugin.LogIfDebug("Butcher Knife used on tamed animal. Bypassing pet protection.", DebugFeature.PetProtection);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}