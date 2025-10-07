using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames.PetProtection
{
    [HarmonyPatch]
    public static class PetProtectionPatch
    {
        // ... (s_exceptionPrefabs, s_wispInstances, wispPrefab bleiben gleich) ...
        private static readonly HashSet<string> s_exceptionPrefabs = new HashSet<string>();
        public static readonly Dictionary<ZDOID, GameObject> s_wispInstances = new Dictionary<ZDOID, GameObject>();
        private static GameObject wispPrefab;

        public static bool IsPetKnockedOut(ZDOID petId)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(petId);
            return zdo != null && zdo.GetBool("BT_Stunned", false);
        }

        #region Setup
        public static void Initialize()
        {
            wispPrefab = ZNetScene.instance.GetPrefab("LuredWisp");
            BetterTamesPlugin.LogIfDebug("Stunned Pets get Transformed into: " + wispPrefab, DebugFeature.PetProtection);
        }

        public static void UpdateExceptionPrefabs(string exceptionPrefabString)
        {
            s_exceptionPrefabs.Clear();
            if (!string.IsNullOrEmpty(exceptionPrefabString))
            {
                foreach (string prefab in exceptionPrefabString.Split(','))
                {
                    if (!string.IsNullOrWhiteSpace(prefab)) s_exceptionPrefabs.Add(prefab.Trim());
                }
            }
            BetterTamesPlugin.LogIfDebug($"Updated exception prefabs: {string.Join(", ", s_exceptionPrefabs)}", DebugFeature.PetProtection);
        }
        #endregion

        [HarmonyPatch(typeof(Character), "Damage")]
        [HarmonyPrefix]
        public static bool ApplyDamagePrefix(Character __instance, HitData hit)
        {
            if (__instance == null || !__instance.IsTamed()) return true;

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            ZDO zdo = nview.GetZDO();
            if (zdo == null) return true;

            // NEU: Prüfen, ob der Schaden vom Butcher Knife eines Spielers kommt.
            Character attacker = hit.GetAttacker();
            if (attacker != null && attacker.IsPlayer())
            {
                Player playerAttacker = attacker as Player;
                ItemDrop.ItemData currentWeapon = playerAttacker.GetCurrentWeapon();

                // Simple Überprüfung über den Namen des Items, das den Schaden verursacht hat.
                if (currentWeapon != null && currentWeapon.m_shared.m_name == "$item_knife_butcher")
                {
                    BetterTamesPlugin.LogIfDebug($"Pet {__instance.m_name} was hit by Butcher Knife. Pet Protection bypassed.", DebugFeature.PetProtection);
                    return true; // Pet Protection wird umgangen, das Tier stirbt.
                }
            }

            // NEU: Wenn das Tier bereits betäubt ist, blockiere ALLEN Schaden.
            if (zdo.GetBool("BT_Stunned", false))
            {
                BetterTamesPlugin.LogIfDebug($"Pet {__instance.m_name} is stunned - blocking further damage.", DebugFeature.PetProtection);
                return false; // Kein Schaden
            }


            // Normale Logik für Pet Protection
            if (!BetterTamesPlugin.ConfigInstance.Tames.PetProtectionEnabled.Value || s_exceptionPrefabs.Contains(__instance.name))
            {
                return true;
            }

            float currentHP = __instance.GetHealth();
            float maxHP = __instance.GetMaxHealth();
            float incomingDamage = hit.GetTotalDamage();
            float healthAfterDamage = currentHP - incomingDamage;
            float healthThreshold = maxHP * 0.01f; // 1% Lebenspunkte

            if (healthAfterDamage <= healthThreshold)
            {
                BetterTamesPlugin.LogIfDebug($"Pet {__instance.m_name} is about to fall below 1% HP. Triggering Pet Protection.", DebugFeature.PetProtection);

                // Diese ZDOs kann jeder Client setzen. Sie werden automatisch synchronisiert.
                zdo.Set(ZDOVars.s_health, healthThreshold);
                zdo.Set("BT_Stunned", true);

                // NUN DIE WICHTIGE UNTERSCHEIDUNG:
                if (ZNet.instance.IsServer())
                {
                    // WENN WIR DER SERVER SIND: Timer direkt starten.
                    int stunDurationInt = BetterTamesPlugin.ConfigInstance.Tames.PetProtectionStunDuration.Value;
                    float stunDuration = (float)stunDurationInt;
                    double startTime = ZNet.instance.GetTimeSeconds();
                    double revivalTime = startTime + stunDuration;
                    StunnedPetManager.AddStunnedPet(zdo.m_uid, (float)revivalTime);
                }
                else
                {
                    // WENN WIR EIN CLIENT SIND: Sende einen RPC an den Server, um den Timer zu starten.
                    BetterTamesPlugin.LogIfDebug($"Sending RPC to server to start stun timer for pet {zdo.m_uid}.", DebugFeature.PetProtection);
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZNet.instance.GetServerPeer().m_uid, BetterTamesPlugin.RPC_REQUEST_PET_STUN, zdo.m_uid);
                }

                return false; // Den ursprünglichen Schaden blockieren
            }

            return true;
        }

        // --- HILFSMETHODEN FÜR VISUELLE ÄNDERUNGEN ---
        // Diese werden jetzt vom ZDOListenerPatch aufgerufen

        public static void EnterStunVisuals(Character character)
        {
            if (character == null) return;
            ZDOID petId = character.GetZDOID();

            // Tier unsichtbar machen und Collider deaktivieren
            SetCharacterVisible(character, false);

            // Wisp spawnen
            if (wispPrefab != null && !s_wispInstances.ContainsKey(petId))
            {
                GameObject wispInstance = UnityEngine.Object.Instantiate(wispPrefab, character.transform.position, Quaternion.identity);

                // WICHTIG: Collider am Wisp deaktivieren, damit er nicht interagierbar ist
                foreach (Collider col in wispInstance.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }

                s_wispInstances[petId] = wispInstance;
                BetterTamesPlugin.LogIfDebug($"Wisp spawned for {character.m_name}.", DebugFeature.PetProtection);
            }
        }

        public static void ExitStunVisuals(Character character)
        {
            if (character == null) return;
            ZDOID petId = character.GetZDOID();

            // Tier wieder sichtbar machen und Collider aktivieren
            SetCharacterVisible(character, true);

            // --- KORRIGIERTE LOGIK ---
            // Versuche, den Wisp aus unserer Liste zu holen.
            if (s_wispInstances.TryGetValue(petId, out GameObject wisp))
            {
                // Schritt 1: Entferne den Eintrag IMMER aus der Liste.
                // Die visuelle Betäubung ist hiermit beendet, unsere Buchhaltung muss sauber sein.
                s_wispInstances.Remove(petId);
                BetterTamesPlugin.LogIfDebug($"Removed pet {petId} from wisp tracking dictionary.", DebugFeature.PetProtection);

                // Schritt 2: Wenn das Wisp-Objekt tatsächlich noch existiert, zerstöre es.
                if (wisp != null)
                {
                    UnityEngine.Object.Destroy(wisp);
                    BetterTamesPlugin.LogIfDebug($"Wisp GameObject for pet {petId} destroyed.", DebugFeature.PetProtection);
                }
            }
        }

        private static void SetCharacterVisible(Character character, bool visible)
        {
            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = visible;
            }
            foreach (Collider col in character.GetComponentsInChildren<Collider>())
            {
                col.enabled = visible;
            }
        }
    }
}