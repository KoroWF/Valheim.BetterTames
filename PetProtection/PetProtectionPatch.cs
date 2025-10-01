using BetterTames.DistanceTeleport;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class PetProtectionPatch
    {
        private static readonly HashSet<string> s_exceptionPrefabs = new HashSet<string>();
        private static readonly Dictionary<ZDOID, GameObject> s_wispInstances = new Dictionary<ZDOID, GameObject>();
        private static GameObject wispPrefab;
        // FÜGE DIESE NEUE METHODE HINZU:
        /// <summary>
        /// Eine öffentliche Methode, mit der andere Klassen sicher prüfen können, ob ein Tier ausgeknockt ist.
        /// </summary>
        public static bool IsPetKnockedOut(ZDOID petId)
        {
            return s_wispInstances.ContainsKey(petId);
        }

        #region Setup and Initialization
        public static void Initialize()
        {

            wispPrefab = ZNetScene.instance.GetPrefab("LuredWisp");
            BetterTamesPlugin.LogIfDebug("Stunned Pets get Transformed into: " + wispPrefab , DebugFeature.PetProtection);

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
            if (!BetterTamesPlugin.ConfigInstance.Tames.PetProtectionEnabled.Value || !ShouldApplyPetProtection(__instance))
                return true;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            ZDO zdo = nview.GetZDO();
            if (zdo.GetBool("isRecoveringFromStun", false))
                return false;

            if (__instance.GetHealth() > hit.GetTotalDamage())
                return true;

            if (nview.IsOwner())
            {
                ApplyWispTransform(__instance, nview, zdo);
            }

            return false;
        }

        // In PetProtectionPatch.cs
        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPostfix]
        public static void KnockoutTimerPostfix(MonsterAI __instance)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return;

            ZDO zdo = nview.GetZDO();
            if (!zdo.GetBool("isRecoveringFromStun", false)) return;

                // Finde den zugehörigen Wisp in unserem Dictionary
                if (s_wispInstances.TryGetValue(zdo.m_uid, out GameObject wispInstance) && wispInstance != null)
                {
                    // 1. Hole die Character-Komponente vom Wisp (das "tamed ding", das wir porten wollen)
                    Character wispCharacter = wispInstance.GetComponent<Character>();

                    // 2. Hole die KI des *originalen* Tieres, um den Spieler zu finden
                    MonsterAI petAI = __instance.GetComponent<MonsterAI>();

                    // Stelle sicher, dass wir alles haben, was wir brauchen
                    if (wispCharacter != null && petAI != null)
                    {
                        GameObject followTarget = petAI.GetFollowTarget();
                        Player player = followTarget?.GetComponent<Player>();

                        // 3. Wenn wir den Spieler gefunden haben, führe den Teleport aus
                        if (player != null)
                        {
                            BetterTamesPlugin.LogIfDebug("Teleporting wisp to player.", DebugFeature.PetProtection);

                            // Rufe deine bewährte Methode mit den korrekten Objekten auf
                            DistanceTeleportLogic.ExecuteTeleportBehindPlayer(player, followTarget);
                        }
                    }
                }


            // --- BESTEHENDE LOGIK: RÜCKVERWANDLUNG PRÜFEN ---
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
            // ... (Logik zum Setzen von "isRecoveringFromStun", HP, und dem normalen RevivalTimestamp bleibt gleich) ...
            zdo.Set("isRecoveringFromStun", true);
            zdo.Set(ZDOVars.s_health, 1f);

            float revivalTime = (float)ZNet.instance.GetTimeSeconds() + BetterTamesPlugin.ConfigInstance.Tames.PetProtectionStunDuration.Value;
            zdo.Set("BT_RevivalTimestamp", revivalTime);

            // 2. Lebensbalken und Namen aus dem UI entfernen
            if (EnemyHud.instance != null)
            {
                EnemyHud.instance.RemoveCharacterHud(character);
            }

            // --- NEU: Zeitstempel für den Wisp-Teleport setzen (2 Sekunden in der Zukunft) ---
            float wispTeleportTime = (float)ZNet.instance.GetTimeSeconds() + 2f;
            zdo.Set("BT_WispTeleportTimestamp", wispTeleportTime);

            SetRenderersVisible(character, false);

            if (wispPrefab != null)
            {
                GameObject wispInstance = UnityEngine.Object.Instantiate(wispPrefab, character.transform.position, Quaternion.identity);

                MonsterAI originalPetAI = character.GetComponent<MonsterAI>();
                GameObject followTarget = originalPetAI.GetFollowTarget();

                Player player = followTarget?.GetComponent<Player>();

                // 3. Wenn wir den Spieler gefunden haben, führe den Teleport aus
                if (player != null)
                {
                    BetterTamesPlugin.LogIfDebug("Teleporting Soul to player.", DebugFeature.PetProtection);

                    // Rufe deine bewährte Methode mit den korrekten Objekten auf
                    DistanceTeleportLogic.ExecuteTeleportBehindPlayer(character, followTarget);
                }
                s_wispInstances[zdo.m_uid] = wispInstance;
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

            // KORREKTUR 4: Konsistent SetRenderersVisible verwenden
            SetRenderersVisible(character, true);

            zdo.Set("isRecoveringFromStun", false);
            zdo.Set("BT_RevivalTimestamp", 0f);

            float maxHealth = character.GetMaxHealth();
            float healPercentage = BetterTamesPlugin.ConfigInstance.Tames.PetProtectionHealPercentage.Value;
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