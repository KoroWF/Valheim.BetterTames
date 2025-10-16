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
            ZNetView nview = __instance.GetComponent<ZNetView>();
            // Defensive check: nview muss gültig sein, bevor wir GetZDO aufrufen.
            if (nview == null || !nview.IsValid())
                return true;

            ZDO zdo = nview.GetZDO();
            // --- NEU: Butcherknife-Bypass für Server-Owner --- Umgehe die rpclösung unten
            try
            {
                if (ZNet.instance != null && nview.IsOwner())
                {
                    Character attacker = hit.GetAttacker();
                    if (attacker != null && attacker.IsPlayer())
                    {
                        Player player = (Player)attacker;
                        var weapon = player.GetCurrentWeapon();
                        if (weapon != null && weapon.m_dropPrefab != null &&
                            string.Equals(weapon.m_dropPrefab.name, "KnifeButcher", StringComparison.OrdinalIgnoreCase))
                        {
                            BetterTamesPlugin.LogIfDebug($"Zonehost Bypass for butcherknife hit on {__instance.m_name}. Bypassing PetProtection and allowing damage.", DebugFeature.PetProtection);
                            return true; // Schaden passieren lassen -> mögliches Töten
                        }
                    }
                }
                else
                {
                    BetterTamesPlugin.LogIfDebug("Not a server-owner or Zonehost butcherknife hit, proceeding with normal checks.", DebugFeature.PetProtection);
                    if (zdo?.GetBool("BT_MercyKill", false) ?? false)
                    {
                        BetterTamesPlugin.LogIfDebug($"{__instance.m_name} marked for MercyKill (ZDOID: {zdo.m_uid}). Bypassing pet protection. Initial Health: {__instance.GetHealth()}, Hit Damage: {hit.GetTotalDamage()}", DebugFeature.PetProtection);
                        zdo.Set("BT_MercyKill", false); // Flag zurücksetzen
                        return true; // Lass den Schaden durch, unabhängig vom Owner
                    }


                }
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception in server-bypass butcherknife check: {ex}", DebugFeature.PetProtection);
            }

            if (!BetterTamesPlugin.ConfigInstance.Tames.PetProtectionEnabled.Value || !ShouldApplyPetProtection(__instance))
                return true;

            // Wenn wir nicht der Owner des ZNetView sind, handle specially:
            if (!nview.IsOwner())
            {
                // Wenn der Treffer tödlich ist und vom ButcherKnife stammt, sende MercyKill-Request an Zonehost
                if (__instance.GetHealth() <= hit.GetTotalDamage())
                {
                    try
                    {
                        Character attacker = hit.GetAttacker();
                        if (attacker != null && attacker.IsPlayer())
                        {
                            Player player = (Player)attacker;
                            var weapon = player.GetCurrentWeapon();
                            if (weapon != null && weapon.m_dropPrefab != null &&
                                string.Equals(weapon.m_dropPrefab.name, "KnifeButcher", StringComparison.OrdinalIgnoreCase))
                            {
                                BetterTamesPlugin.LogIfDebug($"Non-owner detected lethal butcherknife hit on {__instance.m_name}. Sending MercyKill request to server for ZDOID: {zdo.m_uid}", DebugFeature.PetProtection);

                                // Sende die Anfrage an den Zonehost / Server; dieser broadcastet dann die Notify-RPC,
                                // und der Owner-Client führt das tatsächliche Kill() aus beim Empfang.
                                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_REQUEST_MERCY_KILL, new object[] { zdo.m_uid });

                                // Verhindere lokalen Schadensdurchlauf — Owner wird die autoritative Kill-Aktion ausführen.
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        BetterTamesPlugin.LogIfDebug($"Exception while sending MercyKill request from non-owner: {ex}", DebugFeature.PetProtection);
                    }
                }

                // Nicht tödlich oder keine ButcherKnife: lasse normalen Schaden durch (clientside)
                return true;
            }

            if (zdo.GetBool("isRecoveringFromStun", false))
                return false;

            if (__instance.GetHealth() > hit.GetTotalDamage())
                return true;

            // Nur der Owner führt die Wisp-Transformation aus
            ApplyWispTransform(__instance, nview, zdo);

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

            float revivalTime = (float)ZNet.instance.GetTimeSeconds() + (float)BetterTamesPlugin.ConfigInstance.Tames.PetProtectionStunDuration.Value;
            zdo.Set("BT_RevivalTimestamp", revivalTime);

            // 2. Lebensbalken und Namen aus dem UI entfernen
            if (EnemyHud.instance != null)
            {
                EnemyHud.instance.RemoveCharacterHud(character);
            }

            // --- NEU: Zeitstempel für den Wisp-Teleport setzen (2 Sekunden in der Zukunft) ---
            float wispTeleportTime = (float)ZNet.instance.GetTimeSeconds() + 2f;
            zdo.Set("BT_WispTeleportTimestamp", wispTeleportTime);
            zdo.Set("BT_TransformedToWisp", true);

            SetRenderersVisible(character, false);

            if (wispPrefab != null)
            {
                GameObject wispInstance = UnityEngine.Object.Instantiate(wispPrefab, character.transform.position, Quaternion.identity);

                // Disable colliders on the wisp so it doesn't block physics or trigger hits
                try
                {
                    var colliders = wispInstance.GetComponentsInChildren<Collider>(true);
                    int disabledCount = 0;
                    foreach (var col in colliders)
                    {
                        if (col != null && col.enabled)
                        {
                            col.enabled = false;
                            disabledCount++;
                        }
                    }

                    // Also make any rigidbodies kinematic and disable collision detection if present
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

                    BetterTamesPlugin.LogIfDebug($"Wisp instance created. Disabled {disabledCount} colliders on wisp.", DebugFeature.PetProtection);
                }
                catch (Exception ex)
                {
                    BetterTamesPlugin.LogIfDebug($"Exception while disabling colliders on wisp: {ex}", DebugFeature.PetProtection);
                }

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

            zdo.Set("BT_TransformedToWisp", false);
            zdo.Set("isRecoveringFromStun", false);
            zdo.Set("BT_RevivalTimestamp", 0f);

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