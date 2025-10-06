using BetterTames.DistanceTeleport;
using BetterTames.PetProtection;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames.Utils
{
    public static class RPCManager
    {
        // Der Teleport-Cache für Haustiere gehört logisch zu den RPCs, die ihn verwalten.
        private static readonly Dictionary<ZDOID, List<ZDOID>> serverPetTeleportCache = new Dictionary<ZDOID, List<ZDOID>>();

        public static void RegisterRPCs()
        {
            BetterTamesPlugin.LogIfDebug("Registering RPCs...", DebugFeature.Initialization);
            if (ZRoutedRpc.instance == null)
            {
                BetterTamesPlugin.LogIfDebug("ERROR: ZRoutedRpc.instance is null – RPC registration skipped!", DebugFeature.Initialization);
                return;
            }

            // Teleport-Sync (allgemein)
            ZRoutedRpc.instance.Register<string, ZPackage>(BetterTamesPlugin.RPC_TELEPORT_SYNC, RPC_TeleportSync_Client);

            // MercyKill (für ButcherKnife-Bypass)
            BetterTamesPlugin.LogIfDebug($"Attempting to register RPC: {BetterTamesPlugin.RPC_REQUEST_MERCY_KILL}", DebugFeature.Initialization);
            ZRoutedRpc.instance.Register<ZDOID>(BetterTamesPlugin.RPC_REQUEST_MERCY_KILL, RPC_MercyKill_AllClients);
            BetterTamesPlugin.LogIfDebug($"RPC {BetterTamesPlugin.RPC_REQUEST_MERCY_KILL} registered successfully.", DebugFeature.Initialization);

            // Visibility und Wisp-Handling
            ZRoutedRpc.instance.Register<ZDOID, bool>(BetterTamesPlugin.RPC_PET_SET_VISIBLE, RPC_SetPetVisible_Client);
            ZRoutedRpc.instance.Register<ZDOID>(BetterTamesPlugin.RPC_REMOVE_WISP, RPC_RemoveWisp_Client);

            // Unfollow (für Max-Pets)
            ZRoutedRpc.instance.Register<ZDOID>(BetterTamesPlugin.RPC_REQUEST_UNFOLLOW, RPC_RequestUnfollow_Server);
            ZRoutedRpc.instance.Register<ZDOID>(BetterTamesPlugin.RPC_CONFIRM_UNFOLLOW, RPC_ConfirmUnfollow_Client);

            // FIX: PetProtection-Sync registrieren (für Stun/Revival)
            ZRoutedRpc.instance.Register<ZDOID, bool>(BetterTamesPlugin.RPC_PET_PROTECTION_SYNC, RPC_PetProtectionSync_Client);
            BetterTamesPlugin.LogIfDebug($"RPC {BetterTamesPlugin.RPC_PET_PROTECTION_SYNC} registered.", DebugFeature.PetProtection);

            // Server-only: Teleport-Prep/Recreate
            if (ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.Register<ZDOID, ZPackage>(BetterTamesPlugin.RPC_PREPARE_PETS_FOR_TELEPORT, RPC_PreparePetsForTeleport_Server);
                ZRoutedRpc.instance.Register<ZPackage>(BetterTamesPlugin.RPC_RECREATE_PETS_AT_DESTINATION, RPC_RecreatePetsAtDestination_Server);
                BetterTamesPlugin.LogIfDebug("Server-only RPCs registered.", DebugFeature.TeleportFollow);
            }
        }

        #region Server-Side RPC Handlers

        private static void RPC_PreparePetsForTeleport_Server(long senderPeerID, ZDOID teleportingPlayerZDOID, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            int petCount = pkg.ReadInt();
            List<ZDOID> clientPetZDOs = new List<ZDOID>();
            for (int i = 0; i < petCount; i++)
            {
                clientPetZDOs.Add(pkg.ReadZDOID());
            }

            BetterTamesPlugin.LogIfDebug($"SERVER: Received {petCount} pet ZDOIDs from player {teleportingPlayerZDOID} for teleport prep.", DebugFeature.TeleportFollow);

            List<ZDOID> preparedZDOs = new List<ZDOID>();
            foreach (ZDOID petZDOID in clientPetZDOs)
            {
                ZDO zdo = ZDOMan.instance.GetZDO(petZDOID);
                if (zdo != null && zdo.IsValid())
                {
                    // Temporär Haustier-Instanz zerstören und für die Neuerstellung vorbereiten
                    ZNetView znetView = ZNetScene.instance.FindInstance(zdo);
                    if (znetView != null)
                    {
                        ZNetScene.instance.Destroy(znetView.gameObject);
                    }
                    preparedZDOs.Add(petZDOID);
                }
            }

            if (preparedZDOs.Count > 0)
            {
                serverPetTeleportCache[teleportingPlayerZDOID] = preparedZDOs;
                BetterTamesPlugin.LogIfDebug($"SERVER: Cached {preparedZDOs.Count} pets for player {teleportingPlayerZDOID}.", DebugFeature.TeleportFollow);
            }
        }

        // In RPCManager.cs
        private static void RPC_RecreatePetsAtDestination_Server(long senderPeerID, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            Vector3 destinationPos = pkg.ReadVector3();
            Quaternion playerRot = pkg.ReadQuaternion();

            ZNetPeer peer = ZNet.instance.GetPeer(senderPeerID);
            if (peer == null) return;

            ZDOID playerZDOID = peer.m_characterID;
            if (!serverPetTeleportCache.TryGetValue(playerZDOID, out var cachedPets) || cachedPets.Count == 0)
            {
                return;
            }

            BetterTamesPlugin.LogIfDebug($"SERVER: Recreating {cachedPets.Count} pets for player {playerZDOID} at {destinationPos}.", DebugFeature.TeleportFollow);

            // KORREKTUR: Rufe die richtige Methode auf, um eine LISTE von Positionen zu erhalten.
            var spawnPoints = DistanceTeleportLogic.CalculateDistributedSpawnPositions(destinationPos, playerRot, cachedPets.Count);

            for (int i = 0; i < cachedPets.Count; i++)
            {
                ZDO zdo = ZDOMan.instance.GetZDO(cachedPets[i]);
                if (zdo != null && zdo.IsValid())
                {
                    Vector3 spawnPos = spawnPoints[i];
                    Quaternion spawnRot = Quaternion.LookRotation((destinationPos - spawnPos).normalized);

                    zdo.SetPosition(spawnPos);
                    zdo.SetRotation(spawnRot);
                    zdo.SetOwner(peer.m_uid); // Wichtig: Den Besitz an den Client zurückgeben
                }
            }
            serverPetTeleportCache.Remove(playerZDOID);
        }
        #endregion

        #region Client-Side RPC Handlers
        private static void RPC_SetPetVisible_Client(long sender, ZDOID petZDOID, bool visible)
        {
            BetterTamesPlugin.LogIfDebug("Set Colider and Visible of Tame." + petZDOID, DebugFeature.PetProtection);
            GameObject obj = ZNetScene.instance.FindInstance(petZDOID);
            if (obj == null) return;

            ZNetView zview = obj.GetComponent<ZNetView>();
            if (zview == null || !zview.IsValid()) return;

            Character character = obj.GetComponent<Character>();
            if (character == null) return;

            foreach (Collider col in character.GetComponentsInChildren<Collider>())
                col.enabled = visible;

            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>())
                renderer.enabled = visible;
        }

        private static void RPC_RemoveWisp_Client(long sender, ZDOID petZDOID)
        {
            if (!PetProtection.PetProtectionPatch.IsTransformedToWisp(petZDOID))
                return;

            if (PetProtection.PetProtectionPatch.s_wispInstances.TryGetValue(petZDOID, out GameObject wisp))
            {
                if (wisp != null)
                {
                    UnityEngine.Object.Destroy(wisp);
                    BetterTamesPlugin.LogIfDebug($"Destroyed Wisp for {petZDOID} on client.", DebugFeature.PetProtection);
                }
                PetProtection.PetProtectionPatch.s_wispInstances.Remove(petZDOID);
            }
        }


        // MercyKill (All-Clients: Setzt Flag server-seitig für Bypass)
        private static void RPC_MercyKill_AllClients(long sender, ZDOID targetZDOID)
        {
            ZDO targetZDO = ZDOMan.instance.GetZDO(targetZDOID);
            if (targetZDO == null)
            {
                BetterTamesPlugin.LogIfDebug($"MercyKill RPC failed: ZDO {targetZDOID} not found.", DebugFeature.PetProtection);
                return;
            }

            // Setze Flag – Server priorisiert (sync't auto zu Clients)
            targetZDO.Set("BT_MercyKill", true);
            BetterTamesPlugin.LogIfDebug($"[RPC] MercyKill Flag set for {targetZDOID} by sender {sender}.", DebugFeature.PetProtection);

            // Kein Reset hier – ApplyDamagePrefix resetet es nach Check
        }


        private static IEnumerator ResetMercyFlagAfterFrame(ZDOID targetZDOID)
        {
            yield return null;  // Warte 1 Frame
            ZDO zdo = ZDOMan.instance.GetZDO(targetZDOID);
            if (zdo != null)
            {
                zdo.Set("BT_MercyKill", false);
                BetterTamesPlugin.LogIfDebug($"MercyKill Flag reset for {targetZDOID}.", DebugFeature.PetProtection);
            }
        }

        private static void RPC_TeleportSync_Client(long sender, string zdoID_str, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || ZNet.instance.IsServer()) return;

                ZDOID zdoid = ParseZDOID(zdoID_str);
                if (zdoid.IsNone()) return;

                ZDO zdo = ZDOMan.instance.GetZDO(zdoid);
                if (zdo == null || !zdo.IsValid()) return;

                Vector3 position = pkg.ReadVector3();
                Quaternion rotation = pkg.ReadQuaternion();

                ZNetView znetView = ZNetScene.instance.FindInstance(zdo);
                if (znetView != null)
                {
                    Character character = znetView.GetComponent<Character>();
                    // Nur bewegen, wenn das Tier nicht bereits teleportiert (verhindert Jitter)
                    if (character != null && !character.IsTeleporting())
                    {
                        znetView.transform.position = position;
                        znetView.transform.rotation = rotation;
                    }
                }
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception in RPC_TeleportSync_Client: {ex}", DebugFeature.TeleportFollow);
            }
        }

        // Wird vom Client an den Server geschickt
        private static void RPC_RequestUnfollow_Server(long sender, ZDOID petZDOID)
        {
            if (!ZNet.instance.IsServer()) return;

            GameObject obj = ZNetScene.instance.FindInstance(petZDOID);
            if (obj == null) return;

            MonsterAI ai = obj.GetComponent<MonsterAI>();
            if (ai == null) return;

            ai.SetFollowTarget(null);
            BetterTamesPlugin.LogIfDebug($"[Server] Pet {obj.name} unfollowed by request from peer {sender}", DebugFeature.MakeCommandable);

            // Schicke die Bestätigung zurück an den Client
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, BetterTamesPlugin.RPC_CONFIRM_UNFOLLOW, petZDOID);
        }

        // Wird auf dem Client ausgeführt, um seinen lokalen Zustand zu aktualisieren
        private static void RPC_ConfirmUnfollow_Client(long sender, ZDOID petZDOID)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(petZDOID);
            if (zdo == null) return;

            zdo.Set(ZDOVars.s_follow, ""); // Lokalen Follow-Eintrag leeren
            BetterTamesPlugin.LogIfDebug($"[Client] Confirmed unfollow for pet {petZDOID}", DebugFeature.MakeCommandable);
        }

        #endregion

        #region Helper Methods

        // TODO: Diese Logik sollte in eine `DistanceTeleportLogic`-Klasse.
        private static List<Vector3> CalculateDistributedSpawnPositions(Vector3 center, Quaternion direction, int count)
        {
            var positions = new List<Vector3>(count);  // Pre-allocate
            float radius = 3f;
            float angleStep = 30f;

            // Batch Floor-Find: Zuerst grobe Height holen
            if (ZoneSystem.instance.FindFloor(center + Vector3.up * 10f, out float baseHeight))
            {
                baseHeight += 0.2f;
            }
            else baseHeight = center.y;

            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) / 2f) * angleStep;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * (direction * Vector3.back * radius);
                Vector3 spawnPos = center + offset;
                spawnPos.y = baseHeight;  // Nutze gecachte Height statt pro Pet FindFloor
                positions.Add(spawnPos);
            }
            return positions;
        }

        private static ZDOID ParseZDOID(string zdoID_str)
        {
            if (string.IsNullOrEmpty(zdoID_str)) return ZDOID.None;

            string[] parts = zdoID_str.Split(':');
            if (parts.Length != 2) return ZDOID.None;

            if (long.TryParse(parts[0], out long userID) && uint.TryParse(parts[1], out uint id))
            {
                return new ZDOID(userID, id);
            }

            return ZDOID.None;
        }


        #endregion
        // FIX: PetProtection-Sync (Client: Stun/Revival syncen)
        private static void RPC_PetProtectionSync_Client(long sender, ZDOID petZDOID, bool isStunned)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(petZDOID);
            if (zdo == null) return;

            // FIX: Kein GetCharacter – nutze FindInstance + GetComponent
            ZNetView nview = ZNetScene.instance.FindInstance(zdo);
            if (nview == null) return;

            Character pet = nview.GetComponent<Character>();
            if (pet == null) return;

            if (isStunned)
            {
                PetProtectionPatch.SetRenderersVisible(pet, false);  // Verstecke Original
                                                                     // Spawn Wisp lokal, wenn nicht da
                if (!PetProtectionPatch.s_wispInstances.ContainsKey(petZDOID))
                {
                    PetProtectionPatch.ApplyWispTransform(pet, nview, zdo);
                }
            }
            else
            {
                PetProtectionPatch.SetRenderersVisible(pet, true);  // Zeige wieder
                if (PetProtectionPatch.s_wispInstances.TryGetValue(petZDOID, out GameObject wisp) && wisp != null)
                {
                    UnityEngine.Object.Destroy(wisp);
                    PetProtectionPatch.s_wispInstances.Remove(petZDOID);
                }
            }
            BetterTamesPlugin.LogIfDebug($"[RPC] Synced {(isStunned ? "stun" : "revival")} for {pet.m_name} ({petZDOID}).", DebugFeature.PetProtection);
        }
    }
}