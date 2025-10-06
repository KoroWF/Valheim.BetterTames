using BetterTames.DistanceTeleport;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class PetProtectionPatch
    {
        private static readonly HashSet<string> s_exceptionPrefabs = new HashSet<string>();
        public static readonly Dictionary<ZDOID, GameObject> s_wispInstances = new Dictionary<ZDOID, GameObject>();
        private static GameObject wispPrefab;
        private static readonly Dictionary<ZDOID, (ZNetView nview, MonsterAI ai)> cachedComponents =
            new Dictionary<ZDOID, (ZNetView nview, MonsterAI ai)>(32);  // Pre-capacity (C# 7.3-kompatibel)

        /// <summary>
        /// Eine öffentliche Methode, mit der andere Klassen sicher prüfen können, ob ein Tier ausgeknockt ist.
        /// </summary>
        public static bool IsPetKnockedOut(ZDOID petId)
        {
            return s_wispInstances.ContainsKey(petId);
        }

        /// <summary>
        /// Prüft, ob ein Tier in Wisp-Form ist (über ZDO synchronisiert).
        /// </summary>
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
            if (string.IsNullOrEmpty(exceptionPrefabString)) return;

            string[] prefabs = exceptionPrefabString.Split(',');
            foreach (string prefab in prefabs)
            {
                if (!string.IsNullOrWhiteSpace(prefab))
                {
                    s_exceptionPrefabs.Add(prefab.Trim());
                }
            }

            BetterTamesPlugin.LogIfDebug($"Updated exception prefabs: {string.Join(", ", s_exceptionPrefabs)}", DebugFeature.PetProtection);
        }

        #endregion

        #region Harmony Patches
        [HarmonyPatch(typeof(Character), "Damage", new[] { typeof(HitData) })]  // FIX: Explizite Signatur für Überladung
        [HarmonyPrefix]
        public static bool ApplyDamagePrefix(Character __instance, HitData hit)
        {
            // Früher Null-Check
            if (__instance == null || hit == null) return true;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return true;

            // MercyKill-Check ZUERST (vor allem anderen – umgeht alles)
            bool isMercyKill = zdo.GetBool("BT_MercyKill", false);
            if (isMercyKill)
            {
                zdo.Set("BT_MercyKill", false);  // Reset sofort (einmalig für diesen Hit)
                BetterTamesPlugin.LogIfDebug($"MercyKill bypass activated for {__instance.m_name}. Flag reset.", DebugFeature.PetProtection);
                return true;  // Lass Schaden durch → Tod, kein Stun
            }

            // Nur für Tamed und enabled (nach Flag)
            if (!BetterTamesPlugin.ConfigInstance.Tames.PetProtectionEnabled.Value || !__instance.IsTamed()) return true;

            // Exception-Check
            if (s_exceptionPrefabs.Contains(__instance.gameObject.name)) return true;

            float currentHealth = zdo.GetFloat(ZDOVars.s_health);
            float maxHealth = __instance.GetMaxHealth();
            float incomingDamage = hit.GetTotalDamage();

            BetterTamesPlugin.LogIfDebug($"Pet {__instance.m_name}: Current HP {currentHealth}/{maxHealth}, Incoming {incomingDamage}", DebugFeature.PetProtection);

            if (currentHealth > incomingDamage) return true;  // Überlebt normal

            // Knockout-Logik: Setze auf 1 HP, stunne und transformiere
            float stunDuration = BetterTamesPlugin.ConfigInstance.Tames.PetProtectionStunDuration.Value;
            float revivalTime = (float)(ZNet.instance.GetTimeSeconds() + stunDuration);

            zdo.Set(ZDOVars.s_health, 1f);  // Minimal HP
            zdo.Set("isRecoveringFromStun", true);
            zdo.Set("BT_RevivalTimestamp", revivalTime);
            zdo.Set("BT_TransformedToWisp", true);

            BetterTamesPlugin.LogIfDebug($"Knocking out {__instance.m_name} for {stunDuration}s. Revival at {revivalTime}", DebugFeature.PetProtection);

            // Transformiere zu Wisp (lokal erst, sync via Flag)
            ApplyWispTransform(__instance, nview, zdo);

            // Sync an alle Clients
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_PET_PROTECTION_SYNC, nview.GetZDO().m_uid, true);

            // Blockiere den tödlichen Schaden
            return false;
        }
        /// <summary>
        /// Postfix auf MonsterAI.UpdateAI: Prüft Revival-Timer und cached Components für Wisp-Teleport.
        /// </summary>
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]  // <-- FIX: Explizites Attribut für den Target-Method
        [HarmonyPostfix]
        public static void KnockoutTimerPostfix(MonsterAI __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null || !zdo.GetBool("isRecoveringFromStun", false)) return;

            // Bestehende Logik: Rückverwandlung prüfen
            float revivalTimestamp = zdo.GetFloat("BT_RevivalTimestamp", 0f);
            if (revivalTimestamp > 0f && ZNet.instance.GetTimeSeconds() >= revivalTimestamp)
            {
                Character character = __instance.GetComponent<Character>();
                if (character != null)
                {
                    RevertWispTransform(character, nview, zdo);
                }
            }

            // Caching für Komponenten
            ZDOID zdoid = zdo.m_uid;
            (ZNetView nview, MonsterAI ai) cache;
            if (!cachedComponents.TryGetValue(zdoid, out cache) || cache.nview == null)
            {
                cache = (nview: nview, ai: __instance);  // Named Tuple (C# 7.1+)
                cachedComponents[zdoid] = cache;
            }

            // Finde den zugehörigen Wisp und teleporte, falls nötig
            if (s_wispInstances.TryGetValue(zdo.m_uid, out GameObject wispInstance) && wispInstance != null)
            {
                Character wispCharacter = wispInstance.GetComponent<Character>();
                MonsterAI petAI = cache.ai;  // Aus Cache

                if (wispCharacter != null && petAI != null)
                {
                    GameObject followTarget = petAI.GetFollowTarget();
                    Player player = followTarget?.GetComponent<Player>();

                    if (player != null)
                    {
                        BetterTamesPlugin.LogIfDebug("Teleporting wisp to player.", DebugFeature.PetProtection);
                        DistanceTeleportLogic.ExecuteTeleportBehindPlayer(wispCharacter, followTarget);
                    }
                }
            }
        }

        #endregion

        #region Transformation Methods
        public static void ApplyWispTransform(Character character, ZNetView nview, ZDO zdo)
        {
            BetterTamesPlugin.LogIfDebug($"Applying wisp transform to {character.m_name}.", DebugFeature.PetProtection);

            SetRenderersVisible(character, false);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_PET_SET_VISIBLE, zdo.m_uid, false);

            if (wispPrefab != null)
            {
                GameObject wispInstance = UnityEngine.Object.Instantiate(wispPrefab, character.transform.position, Quaternion.identity);
                s_wispInstances[zdo.m_uid] = wispInstance;

                // FIX: Deaktiviere Interactability auf Wisp
                Tameable wispTameable = wispInstance.GetComponent<Tameable>();
                if (wispTameable != null)
                {
                    wispTameable.enabled = false;  // Deaktiviert Interact
                    BetterTamesPlugin.LogIfDebug("Disabled Tameable on wisp instance.", DebugFeature.PetProtection);
                }

                Collider[] wispColliders = wispInstance.GetComponentsInChildren<Collider>();
                foreach (Collider col in wispColliders)
                {
                    col.enabled = false;  // Keine Physik/Interact
                }

                // Optional: Setze Layer zu "UI" oder "Ignore Raycast" für extra Sicherheit
                wispInstance.layer = LayerMask.NameToLayer("UI");
            }
        }

        private static void RevertWispTransform(Character character, ZNetView nview, ZDO zdo)
        {
            BetterTamesPlugin.LogIfDebug($"Reverting {character.m_name} from wisp form.", DebugFeature.PetProtection);

            if (s_wispInstances.TryGetValue(zdo.m_uid, out GameObject wispInstance))
            {
                if (wispInstance != null)
                {
                    UnityEngine.Object.Destroy(wispInstance);
                }
                s_wispInstances.Remove(zdo.m_uid);
            }

            // Synchronisiere Wisp-Entfernung an alle Clients
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REMOVE_WISP, zdo.m_uid);

            // Konsistent SetRenderersVisible verwenden
            SetRenderersVisible(character, true);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_PET_SET_VISIBLE, zdo.m_uid, true);

            zdo.Set("BT_TransformedToWisp", false);
            zdo.Set("isRecoveringFromStun", false);
            zdo.Set("BT_RevivalTimestamp", 0f);

            float maxHealth = character.GetMaxHealth();
            float healPercentage = BetterTamesPlugin.ConfigInstance.Tames.PetProtectionHealPercentage.Value;
            float healthToRestore = Mathf.Clamp(maxHealth * (healPercentage / 100f), 1f, maxHealth);
            zdo.Set(ZDOVars.s_health, healthToRestore);

            BetterTamesPlugin.LogIfDebug($"Revived {character.m_name} with {healthToRestore} HP.", DebugFeature.PetProtection);
        }

        public static void SetRenderersVisible(Character character, bool visible)
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
        #endregion
    }
}