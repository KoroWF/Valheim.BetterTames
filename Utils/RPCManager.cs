using BetterTames.DistanceTeleport;
using BetterTames.PetProtection;
using System;
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

            // Client-seitige RPCs
            ZRoutedRpc.instance.Register<string, ZPackage>(BetterTamesPlugin.RPC_TELEPORT_SYNC, RPC_TeleportSync_Client);

            // Server-seitige RPCs
            if (ZNet.instance.IsServer())
            {
                //ZRoutedRpc.instance.Register<string>(BetterTamesPlugin.RPC_REQUEST_PET_PROTECTION, RPC_RequestPetProtection_Server);
                ZRoutedRpc.instance.Register<ZDOID, ZPackage>(BetterTamesPlugin.RPC_PREPARE_PETS_FOR_TELEPORT, RPC_PreparePetsForTeleport_Server);
                ZRoutedRpc.instance.Register<ZPackage>(BetterTamesPlugin.RPC_RECREATE_PETS_AT_DESTINATION, RPC_RecreatePetsAtDestination_Server);
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

        #endregion

        #region Helper Methods

        // TODO: Diese Logik sollte in eine `DistanceTeleportLogic`-Klasse.
        private static List<Vector3> CalculateDistributedSpawnPositions(Vector3 center, Quaternion direction, int count)
        {
            var positions = new List<Vector3>();
            float radius = 3f; // Startradius
            float angleStep = 30f; // Winkel zwischen den Tieren

            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) / 2f) * angleStep;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * (direction * Vector3.back);
                Vector3 spawnPos = center + offset * radius;

                // Finde den Boden für die exakte Position
                if (ZoneSystem.instance.FindFloor(spawnPos + Vector3.up, out float floorHeight))
                {
                    spawnPos.y = floorHeight + 0.2f;
                }
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
    }
}