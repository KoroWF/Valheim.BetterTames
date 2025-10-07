using BetterTames.PetProtection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterTames.Utils
{
    public static class RPCManager
    {
        private static readonly Dictionary<ZDOID, List<ZDOID>> serverPetTeleportCache = new Dictionary<ZDOID, List<ZDOID>>();

        public static void RegisterRPCs()
        {
            BetterTamesPlugin.LogIfDebug("Registering RPCs...", DebugFeature.Initialization);
            if (ZRoutedRpc.instance == null)
            {
                BetterTamesPlugin.LogIfDebug("ERROR: ZRoutedRpc.instance is null – RPC registration skipped!", DebugFeature.Initialization);
                return;
            }

            if (ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.Register<ZDOID, ZPackage>(BetterTamesPlugin.RPC_PREPARE_PETS_FOR_TELEPORT, RPC_PreparePetsForTeleport_Server);
                ZRoutedRpc.instance.Register<ZPackage>(BetterTamesPlugin.RPC_RECREATE_PETS_AT_DESTINATION, RPC_RecreatePetsAtDestination_Server);
                BetterTamesPlugin.LogIfDebug("Server-only Teleport RPCs registered.", DebugFeature.TeleportFollow);
                ZRoutedRpc.instance.Register<ZDOID>(BetterTamesPlugin.RPC_REQUEST_PET_STUN, RPC_RequestPetStun_Server);
                BetterTamesPlugin.LogIfDebug("Server-only PetStun RPC registered.", DebugFeature.PetProtection);
            }
        }

        #region Server-Side RPC Handlers

        private static void RPC_RequestPetStun_Server(long senderPeerID, ZDOID petZDOID)
        {
            if (!ZNet.instance.IsServer()) return;

            ZDO zdo = ZDOMan.instance.GetZDO(petZDOID);
            if (zdo == null || !zdo.IsValid())
            {
                BetterTamesPlugin.LogIfDebug($"Server received invalid ZDOID {petZDOID} for pet stun request.", DebugFeature.PetProtection);
                return;
            }

            // Sicherheitscheck: Ist das Tier wirklich betäubt?
            if (!zdo.GetBool("BT_Stunned", false))
            {
                // Wenn der Client schneller war als das ZDO-Update, setzen wir es hier serverseitig zur Sicherheit.
                // In der Regel sollte das ZDO aber schon auf 'true' sein.
                zdo.Set("BT_Stunned", true);
            }

            BetterTamesPlugin.LogIfDebug($"Server received pet stun request for {petZDOID} from peer {senderPeerID}.", DebugFeature.PetProtection);

            // Starte den Wiederbelebungs-Timer
            int stunDurationInt = BetterTamesPlugin.ConfigInstance.Tames.PetProtectionStunDuration.Value;
            float stunDuration = (float)stunDurationInt;
            double startTime = ZNet.instance.GetTimeSeconds();
            double revivalTime = startTime + stunDuration; ;
            StunnedPetManager.AddStunnedPet(petZDOID, (float)revivalTime);
        }

        private static void RPC_PreparePetsForTeleport_Server(long senderPeerID, ZDOID teleportingPlayerZDOID, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            // Bestehender Code für Cache (verkürzt)
            List<ZDOID> clientPetZDOs = new List<ZDOID>();  // Deine Logik zum Sammeln
            serverPetTeleportCache[teleportingPlayerZDOID] = clientPetZDOs;
            BetterTamesPlugin.LogIfDebug($"SERVER: Cached {clientPetZDOs.Count} pets for player {teleportingPlayerZDOID}.", DebugFeature.TeleportFollow);
        }

        private static void RPC_RecreatePetsAtDestination_Server(long senderPeerID, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            Vector3 destinationPos = pkg.ReadVector3();
            Quaternion playerRot = pkg.ReadQuaternion();

            ZNetPeer peer = ZNet.instance.GetPeer(senderPeerID);
            if (peer == null) return;

            ZDOID playerZDOID = peer.m_characterID;
            if (!serverPetTeleportCache.TryGetValue(playerZDOID, out List<ZDOID> cachedPets) || cachedPets.Count == 0)
            {
                return;
            }

            BetterTamesPlugin.LogIfDebug($"SERVER: Recreating {cachedPets.Count} pets for player {playerZDOID} at {destinationPos}.", DebugFeature.TeleportFollow);

            List<Vector3> spawnPoints = CalculateDistributedSpawnPositions(destinationPos, playerRot, cachedPets.Count);

            for (int i = 0; i < cachedPets.Count; i++)
            {
                ZDO zdo = ZDOMan.instance.GetZDO(cachedPets[i]);
                if (zdo != null && zdo.IsValid())
                {
                    Vector3 spawnPos = spawnPoints[i];
                    Quaternion spawnRot = Quaternion.LookRotation((destinationPos - spawnPos).normalized);

                    zdo.SetPosition(spawnPos);
                    zdo.SetRotation(spawnRot);
                    zdo.SetOwner(peer.m_uid);
                }
            }
            serverPetTeleportCache.Remove(playerZDOID);
        }

        private static List<Vector3> CalculateDistributedSpawnPositions(Vector3 center, Quaternion direction, int count)
        {
            List<Vector3> positions = new List<Vector3>();
            float angleStep = 360f / count;
            float radius = 2f;

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                positions.Add(center + offset);
            }
            return positions;
        }
        #endregion
    }
}