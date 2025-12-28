using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class PetProtectionPatch
    {
        private static readonly HashSet<string> s_exceptionPrefabs = new HashSet<string>();
        private static readonly Dictionary<ZDOID, GameObject> s_wispInstances = new Dictionary<ZDOID, GameObject>();

        private static GameObject wispPrefab;

        public static bool IsPetKnockedOut(ZDOID petId)
        {
            return s_wispInstances.ContainsKey(petId);
        }

        public static bool IsTransformedToWisp(ZDOID petId)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(petId);
            return zdo != null && zdo.GetBool("BT_TransformedToWisp", false);
        }

        #region Setup and Initialization
        public static void Initialize()
        {
            wispPrefab = ZNetScene.instance.GetPrefab("LuredWisp");

            BetterTamesPlugin.LogIfDebug("Stunned Pets get Transformed into: " + wispPrefab, DebugFeature.PetProtection);

            if (wispPrefab != null)
            {
                BetterTamesPlugin.LogIfDebug("LuredWisp prefab cached successfully.", DebugFeature.PetProtection);
            }
            else
            {
                BetterTamesPlugin.LogIfDebug("ERROR: Could not cache LuredWisp prefab!", DebugFeature.PetProtection);
            }
        }

        public static void UpdateExceptionPrefabs(string exceptionPrefabString)
        {
            s_exceptionPrefabs.Clear();
            if (string.IsNullOrWhiteSpace(exceptionPrefabString)) return;

            var exceptions = exceptionPrefabString.Split(',');
            foreach (var exception in exceptions)
            {
                s_exceptionPrefabs.Add(exception.Trim().ToLowerInvariant());
            }
        }

        private static bool ShouldApplyPetProtection(Character character)
        {
            if (character == null || !character.IsTamed()) return false;
            if (character.GetComponent<Tameable>() == null) return false;
            if (character.GetComponent<MonsterAI>() == null && character.GetComponent<BaseAI>() == null) return false;
            ZNetView nview = character.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return false;
            string prefabName = ZNetScene.instance.GetPrefab(nview.GetZDO().GetPrefab()).name.ToLowerInvariant();
            return !s_exceptionPrefabs.Contains(prefabName);
        }
        #endregion
        [HarmonyPatch(typeof(Character), "ApplyDamage")]
        [HarmonyPrefix]
        public static bool ApplyDamagePrefix(Character __instance, HitData hit)
        {
            if (!__instance.IsTamed()) return true;

            float remainingHealth = __instance.GetHealth() - hit.GetTotalDamage();
            if (remainingHealth > 0f) return true;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            ZDO zdo = nview.GetZDO();

            if (zdo.GetBool("BT_MercyKill", false))
            {
                zdo.Set("BT_MercyKill", false);
                BetterTamesPlugin.LogIfDebug($"MercyKill executed for {__instance.m_name}", DebugFeature.PetProtection);
                return true;
            }


            if (!BetterTamesPlugin.ConfigInstance.Tames.PetProtectionEnabled.Value ||
                !PetProtectionPatch.ShouldApplyPetProtection(__instance))
            {
                return true;
            }


            if (nview.IsOwner())
            {
                if (zdo.GetBool("isRecoveringFromStun", false))
                    return false;

                PetProtectionPatch.ApplyWispTransform(__instance, nview, zdo);
                return false;
            }

            return true; 
        }

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void KnockoutTimerPrefix(MonsterAI __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return;

            ZDO zdo = nview.GetZDO();
            if (!zdo.GetBool("isRecoveringFromStun", false)) return;

            float revivalTimestamp = zdo.GetFloat("BT_RevivalTimestamp", 0f);
            if (revivalTimestamp > 0f && ZNet.instance.GetTimeSeconds() >= revivalTimestamp)
            {
                Character character = __instance.GetComponent<Character>();
                if (character != null)
                {
                    RevertWispTransform(character, nview, zdo);
                }
            }
        }

        private static void ApplyWispTransform(Character character, ZNetView nview, ZDO zdo)
        {
            // Bleibt gleich, aber stelle sicher, dass HP auf 1 gesetzt wird, um Tod zu vermeiden
            if (character == null || nview == null || zdo == null)
            {
                BetterTamesPlugin.LogIfDebug("ApplyWispTransform called with null parameter(s) - aborting.", DebugFeature.PetProtection);
                return;
            }


            zdo.Set("isRecoveringFromStun", true);
            zdo.Set(ZDOVars.s_health, 1f); // Sicherstellen, dass es überlebt
            zdo.Set("dead", true);

            float revivalTime = (float)ZNet.instance.GetTimeSeconds() + (float)BetterTamesPlugin.ConfigInstance.Tames.PetProtectionStunDuration.Value;
            zdo.Set("BT_RevivalTimestamp", revivalTime);

            if (EnemyHud.instance != null)
            {
                EnemyHud.instance.RemoveCharacterHud(character);
            }

            float wispTeleportTime = (float)ZNet.instance.GetTimeSeconds() + 2f;
            zdo.Set("BT_WispTeleportTimestamp", wispTeleportTime);
            zdo.Set("BT_TransformedToWisp", true);

            SetRenderersVisible(character, false);

            if (wispPrefab != null && character.GetComponent<MonsterAI>() != null)
            {
                GameObject wispInstance = UnityEngine.Object.Instantiate(wispPrefab, character.transform.position, Quaternion.identity);

                try
                {
                    var colliders = wispInstance.GetComponentsInChildren<Collider>(true);
                    foreach (var col in colliders)
                    {
                        if (col != null) col.enabled = false;
                    }

                    var rigidbodies = wispInstance.GetComponentsInChildren<Rigidbody>(true);
                    foreach (var rb in rigidbodies)
                    {
                        if (rb != null)
                        {
                            rb.isKinematic = true;
#if UNITY_2019_1_OR_NEWER
                            rb.detectCollisions = false;
#endif
                        }
                    }
                }
                catch (Exception ex)
                {
                    BetterTamesPlugin.LogIfDebug($"Exception while disabling colliders on wisp: {ex}", DebugFeature.PetProtection);
                }

                s_wispInstances[zdo.m_uid] = wispInstance;
            }

            try
            {
                MonsterAI originalPetAI = character?.GetComponent<MonsterAI>();
                GameObject followTarget = originalPetAI.GetFollowTarget();
                Player player = followTarget?.GetComponent<Player>();

                if (player != null)
                {
                    BetterTamesPlugin.LogIfDebug("Teleporting to a safe place (behind player).", DebugFeature.PetProtection);
                    DistanceTeleportLogic.ExecuteTeleportBehindPlayer(character, followTarget);
                }
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception during teleport: {ex}", DebugFeature.PetProtection);
            }
        }

        private static void RevertWispTransform(Character character, ZNetView nview, ZDO zdo)
        {
            BetterTamesPlugin.LogIfDebug($"Reverting {character.m_name} from wisp form.", DebugFeature.PetProtection);

            if (s_wispInstances.TryGetValue(zdo.m_uid, out GameObject wispInstance))
            {
                if (wispInstance != null)
                {
                    ZNetView wispZNetView = wispInstance.GetComponent<ZNetView>();
                    if (wispZNetView != null && ZNetScene.instance != null)
                    {
                        ZNetScene.instance.Destroy(wispInstance);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(wispInstance);
                    }
                }
                s_wispInstances.Remove(zdo.m_uid);
            }

            SetRenderersVisible(character, true);

            zdo.Set("BT_TransformedToWisp", false);
            zdo.Set("isRecoveringFromStun", false);
            zdo.Set("BT_RevivalTimestamp", 0f);
            zdo.Set("dead", false);

            float maxHealth = character.GetMaxHealth();
            float healPercentage = (float)BetterTamesPlugin.ConfigInstance.Tames.PetProtectionHealPercentage.Value;
            float healthToRestore = Mathf.Clamp(maxHealth * (healPercentage / 100f), 1f, maxHealth);
            zdo.Set(ZDOVars.s_health, healthToRestore);

        }

        private static void SetRenderersVisible(Character character, bool visible)
        {
            foreach (Collider col in character.GetComponentsInChildren<Collider>())
            {
                col.enabled = visible;
            }

            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = visible;
            }
        }
    }
}