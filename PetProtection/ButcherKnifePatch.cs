using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class ButcherKnifePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
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

                    // If we are the owner of the ZDO, set the flag locally and allow damage
                    if (nview.IsOwner())
                    {
                        BetterTamesPlugin.LogIfDebug($"Owner setting BT_MercyKill flag for {__instance.m_name} locally.", DebugFeature.PetProtection);
                        nview.GetZDO().Set("BT_MercyKill", true);
                        // Allow damage to proceed on the owner
                        return true;
                    }
                    else
                    {
                        // Non-owner: send RPC to server (zonehost) and DO NOT apply local damage.
                        // This avoids a race where the client kills the pet locally before the server sets the flag.
                        BetterTamesPlugin.LogIfDebug($"Non-owner sending MercyKill RPC for ZDOID: {targetZDOID} to server and blocking local damage.", DebugFeature.PetProtection);
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REQUEST_MERCY_KILL, new object[] { targetZDOID });
                        BetterTamesPlugin.LogIfDebug($"MercyKill RPC sent to server for ZDOID: {targetZDOID}. Local damage blocked until server processes request.", DebugFeature.PetProtection);

                        // Prevent local damage; server will handle the mercy kill if allowed.
                        return false;
                    }
                }
                else
                {
                    BetterTamesPlugin.LogIfDebug("ZNetView is null or invalid, cannot process MercyKill.", DebugFeature.PetProtection);
                }
            }

            return true; // Normal damage flow when not using butcher knife bypass
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