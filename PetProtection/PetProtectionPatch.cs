using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class PetProtectionPatch
    {
        private static readonly HashSet<string> s_exceptionPrefabs = new HashSet<string>();

        public static void UpdateExceptionPrefabs(string exceptionPrefabString)
        {
            s_exceptionPrefabs.Clear();
            if (string.IsNullOrWhiteSpace(exceptionPrefabString))
            {
                BetterTamesPlugin.LogIfDebug("No exception prefabs defined in config.", DebugFeature.PetProtection);
                return;
            }

            var exceptions = exceptionPrefabString.Split(',')
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s));

            foreach (var exception in exceptions)
            {
                s_exceptionPrefabs.Add(exception);
            }
            BetterTamesPlugin.LogIfDebug($"Loaded exception prefabs: {string.Join(", ", s_exceptionPrefabs)}", DebugFeature.PetProtection);
        }

        private static bool ShouldApplyPetProtection(Character character)
        {
            if (!character.IsTamed()) return false;

            ZNetView nview = character.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return false;

            string prefabName = ZNetScene.instance.GetPrefab(nview.GetZDO().GetPrefab()).name.ToLowerInvariant();

            if (s_exceptionPrefabs.Contains(prefabName))
            {
                BetterTamesPlugin.LogIfDebug($"{character.m_name} ({prefabName}) is in exception list. Protection not applied.", DebugFeature.PetProtection);
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(Character), "ApplyDamage")]
        [HarmonyPrefix]
        public static bool ApplyDamagePrefix(Character __instance, HitData hit)
        {
            if (!BetterTamesPlugin.ConfigInstance.Tames.PetProtectionEnabled.Value
                || ZNet.instance == null
                || !ShouldApplyPetProtection(__instance))
            {
                return true; // Vanilla-Verhalten, kein Schutz
            }

            // Ausnahme für das Schlachtermesser
            if (hit.GetAttacker() is Player player && player.GetCurrentWeapon()?.m_dropPrefab?.name == "KnifeButcher")
            {
                return true;
            }

            ZNetView nview = __instance.GetComponent<ZNetView>();
            ZDO zdo = nview?.GetZDO();
            if (zdo == null) return true;

            // Wenn schon im Schutzmodus, keinen weiteren Schaden nehmen
            if (zdo.GetBool("isRecoveringFromStun", false))
            {
                return false;
            }

            // Wenn der Schaden nicht tödlich ist, normal weiterlaufen lassen
            if (__instance.GetHealth() - hit.GetTotalDamage() > 1f)
            {
                return true;
            }

            // Schaden ist tödlich -> Schutzlogik anwenden
            if (nview.IsOwner())
            {
                ApplyPetProtectionLogic(__instance, nview, zdo);
            }
            else
            {
                // Client fordert Schutz vom Server an
                string zdoID_str = $"{zdo.m_uid.UserID}:{zdo.m_uid.ID}";
                ZRoutedRpc.instance.InvokeRoutedRPC(0L, BetterTamesPlugin.RPC_REQUEST_PET_PROTECTION, zdoID_str);
            }

            return false; // Vanilla-Schaden blockieren
        }

        public static void ApplyPetProtectionLogic(Character character, ZNetView nview, ZDO zdo)
        {
            // ... (Diese Methode haben wir bereits im vorherigen Schritt perfektioniert, sie bleibt unverändert)
            // ...
            // (Code von oben hier einfügen, falls er verloren gegangen ist)
        }

        [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
        [HarmonyPostfix]
        public static void UpdateAIPostfix(MonsterAI __instance, float dt)
        {
            if (!BetterTamesPlugin.ConfigInstance.Tames.PetProtectionEnabled.Value) return;

            Character character = __instance.GetComponent<Character>();
            if (character == null || !character.IsTamed()) return;

            ZNetView nview = character.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            ZDO zdo = nview.GetZDO();
            if (zdo == null || !zdo.GetBool("isRecoveringFromStun", false)) return;

            // Nur der Owner soll den Timer verwalten und das Aufwachen einleiten
            if (!nview.IsOwner()) return;

            float timeSinceStun = zdo.GetFloat("timeSinceStun", 0f) + dt;
            zdo.Set("timeSinceStun", timeSinceStun);

            if (timeSinceStun >= BetterTamesPlugin.ConfigInstance.Tames.PetProtectionStunDuration.Value)
            {
                // --- Aufwach-Logik ---
                Ragdoll ragdoll = character.GetComponent<Ragdoll>();
                CapsuleCollider mainCollider = character.GetCollider();

                // Wir holen uns den Animator direkt
                Animator animator = character.GetComponentInChildren<Animator>();

                if (ragdoll != null && mainCollider != null && animator != null)
                {
                    BetterTamesPlugin.LogIfDebug($"Waking up {character.m_name} from ragdoll state.", DebugFeature.PetProtection);

                    Vector3 averageRagdollPos = ragdoll.GetAverageBodyPosition();
                    character.transform.position = averageRagdollPos + Vector3.up * 0.1f;
                    character.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(character.transform.forward, Vector3.up));

                    // Animator und Kollision wieder aktivieren
                    mainCollider.enabled = true;
                    animator.enabled = true; // <-- KORRIGIERTE ZEILE

                    // Ragdoll deaktivieren
                    ragdoll.enabled = false;
                }

                // ... (der Rest der Methode bleibt gleich) ...
                zdo.Set("isRecoveringFromStun", false);
                zdo.Set("timeSinceStun", 0f);

                float maxHealth = character.GetMaxHealth();
                float healPercentage = BetterTamesPlugin.ConfigInstance.Tames.PetProtectionHealPercentage.Value;
                float healthToRestore = Mathf.Clamp(maxHealth * (healPercentage / 100f), 1f, maxHealth);
                zdo.Set(ZDOVars.s_health, healthToRestore);

                return;
            }
        }
    }
}