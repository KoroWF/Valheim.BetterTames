using System;
using HarmonyLib;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class ButcherKnifePatch
    {
        [HarmonyPatch(typeof(Character), "Damage")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Character __instance, HitData hit)
        {
            if (!__instance.IsTamed()) return true;
            if (hit.GetAttacker() is not Player) return true;

            float remainingHealth = __instance.GetHealth() - hit.GetTotalDamage();
            if (remainingHealth > 0f) return true; // Nur tödliche Hits

            if (!CheckButcherKnifeBypass(__instance, hit)) return true;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
            {
                BetterTamesPlugin.LogIfDebug("ZNetView is null or invalid, cannot process MercyKill.", DebugFeature.PetProtection);
                return true;
            }

            ZDOID zdoid = nview.GetZDO().m_uid;
            BetterTamesPlugin.LogIfDebug($"Butcher Knife used on {__instance.m_name} (ZDOID: {zdoid}). IsOwner: {nview.IsOwner()}", DebugFeature.PetProtection);

            if (nview.IsOwner())
            {
                BetterTamesPlugin.LogIfDebug($"Owner setting BT_MercyKill flag for {__instance.m_name} locally.", DebugFeature.PetProtection);
                nview.GetZDO().Set("BT_MercyKill", true);
            }
            else
            {
                BetterTamesPlugin.LogIfDebug($"Non-owner sending MercyKill RPC for ZDOID: {zdoid} to server.", DebugFeature.PetProtection);
                try
                {
                    ZPackage pkg = new ZPackage();
                    pkg.Write(zdoid);
                    Utils.RPCManager.MercyKillRPC.SendPackage(ZNet.instance.GetServerPeer().m_uid, pkg);
                }
                catch (Exception ex)
                {
                    BetterTamesPlugin.LogIfDebug($"Exception sending MercyKill RPC: {ex}", DebugFeature.PetProtection);
                }
            }

            return true;
        }

        private static bool CheckButcherKnifeBypass(Character character, HitData hit)
        {
            if (character == null || !character.IsTamed()) return false;
            if (hit.GetAttacker() is not Player player) return false;

            var weapon = player.GetCurrentWeapon();
            return weapon?.m_dropPrefab?.name == "KnifeButcher";
        }
    }
}
