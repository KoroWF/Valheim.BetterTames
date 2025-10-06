using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class ButcherKnifePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.VeryHigh)]  // FIX: Läuft vor ApplyDamagePrefix (setzt Flag früh)
        public static bool Prefix(Character __instance, HitData hit)
        {
            // Früher Null-Check
            if (__instance == null)
            {
                BetterTamesPlugin.LogIfDebug("ButcherKnifePatch Prefix skipped: __instance is null.", DebugFeature.PetProtection);
                return true;
            }

            BetterTamesPlugin.LogIfDebug("ButcherKnifePatch Prefix called for character: " + __instance.m_name, DebugFeature.PetProtection);

            if (CheckButcherKnifeBypass(__instance, hit))
            {
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    ZDOID targetZDOID = nview.GetZDO().m_uid;
                    BetterTamesPlugin.LogIfDebug($"Butcher Knife used on {__instance.m_name} (ZDOID: {targetZDOID}). IsOwner: {nview.IsOwner()}", DebugFeature.PetProtection);

                    if (nview.IsOwner())
                    {
                        BetterTamesPlugin.LogIfDebug($"Owner setting BT_MercyKill flag for {__instance.m_name} locally.", DebugFeature.PetProtection);
                        nview.GetZDO().Set("BT_MercyKill", true);
                    }
                    else
                    {
                        BetterTamesPlugin.LogIfDebug($"Non-owner sending MercyKill RPC for ZDOID: {targetZDOID} to all clients.", DebugFeature.PetProtection);
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REQUEST_MERCY_KILL, targetZDOID);
                        BetterTamesPlugin.LogIfDebug($"MercyKill RPC sent to all for ZDOID: {targetZDOID}.", DebugFeature.PetProtection);
                    }

                    return true;  // Lass Schaden durch (Flag wirkt in nachfolgendem Prefix)
                }
                else
                {
                    BetterTamesPlugin.LogIfDebug("ZNetView is null or invalid, cannot process MercyKill.", DebugFeature.PetProtection);
                }
            }

            return true; // Normaler Fluss
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