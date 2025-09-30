using System;
using BepInEx.Configuration;
using HarmonyLib;
using BetterTames.ConfigSynchronization;

namespace BetterTames.PetProtection 
{ 
    // Token: 0x0200000B RID: 11
    [HarmonyPatch]
    public static class MonsterAI_StunBehaviorPatches
    {
        // Token: 0x0600004C RID: 76 RVA: 0x00003A1C File Offset: 0x00001C1C
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        public static bool PreventAIUpdateWhenStunned(MonsterAI __instance)
        {
            ConfigSync configInstance = BetterTamesPlugin.ConfigInstance;
            bool flag;
            if (configInstance == null)
            {
                flag = true;
            }
            else
            {
                ConfigSync.TamesConfig tames = configInstance.Tames;
                bool? flag2;
                if (tames == null)
                {
                    flag2 = null;
                }
                else
                {
                    ConfigEntry<bool> petProtectionEnabled = tames.PetProtectionEnabled;
                    flag2 = ((petProtectionEnabled != null) ? new bool?(petProtectionEnabled.Value) : null);
                }
                bool? flag3 = flag2;
                flag = !flag3.GetValueOrDefault();
            }
            if (flag)
            {
                return true;
            }
            ZNetView component = __instance.GetComponent<ZNetView>();
            if (component == null || !component.IsValid())
            {
                return true;
            }
            ZDO zdo = component.GetZDO();
            Character component2 = __instance.GetComponent<Character>();
            if (zdo != null && component2 != null && component2.IsTamed() && zdo.GetBool("isRecoveringFromStun", false))
            {
                if (component2.m_speed != 0f)
                {
                    component2.m_speed = 0f;
                }
                if (component2.m_walkSpeed != 0f)
                {
                    component2.m_walkSpeed = 0f;
                }
                if (component2.m_runSpeed != 0f)
                {
                    component2.m_runSpeed = 0f;
                }
                if (component2.m_swimSpeed != 0f)
                {
                    component2.m_swimSpeed = 0f;
                }
                return false;
            }
            return true;
        }

        // Token: 0x0600004D RID: 77 RVA: 0x00003B28 File Offset: 0x00001D28
        [HarmonyPatch(typeof(Humanoid), "StartAttack")]
        [HarmonyPrefix]
        public static bool PreventAttackWhenStunned_Humanoid(Humanoid __instance)
        {
            ConfigSync configInstance = BetterTamesPlugin.ConfigInstance;
            bool flag;
            if (configInstance == null)
            {
                flag = true;
            }
            else
            {
                ConfigSync.TamesConfig tames = configInstance.Tames;
                bool? flag2;
                if (tames == null)
                {
                    flag2 = null;
                }
                else
                {
                    ConfigEntry<bool> petProtectionEnabled = tames.PetProtectionEnabled;
                    flag2 = ((petProtectionEnabled != null) ? new bool?(petProtectionEnabled.Value) : null);
                }
                bool? flag3 = flag2;
                flag = !flag3.GetValueOrDefault();
            }
            if (flag || !__instance.IsTamed())
            {
                return true;
            }
            ZNetView component = __instance.GetComponent<ZNetView>();
            if (component != null && component.IsValid())
            {
                ZDO zdo = component.GetZDO();
                if (zdo != null && zdo.GetBool("isRecoveringFromStun", false))
                {
                    BetterTamesPlugin.LogIfDebug("Attack attempt by " + __instance.m_name + " (Humanoid) blocked due to PetProtection stun.", DebugFeature.PetProtection);
                    return false;
                }
            }
            return true;
        }
    }
}
