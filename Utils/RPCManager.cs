using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using BetterTames.DistanceTeleport;
using BetterTames.PetProtection;
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

<<<<<<< HEAD
<<<<<<< Updated upstream
            if (ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.Register<ZDOID, ZPackage>(BetterTamesPlugin.RPC_PREPARE_PETS_FOR_TELEPORT, RPC_PreparePetsForTeleport_Server);
                ZRoutedRpc.instance.Register<ZPackage>(BetterTamesPlugin.RPC_RECREATE_PETS_AT_DESTINATION, RPC_RecreatePetsAtDestination_Server);
            }
=======
            // Use RPCHelper which encapsulates readiness and server/client checks.
            RPCHelper.RegisterClient<ZDOID>(BetterTamesPlugin.RPC_REQUEST_MERCY_KILL, RPC_RequestMercyKill_Server);

            RPCHelper.RegisterClient<string>(BetterTamesPlugin.RPC_NOTIFY_MERCY_KILL, RPC_NotifyMercyKill_Client);
            RPCHelper.RegisterClient<string, ZPackage>(BetterTamesPlugin.RPC_TELEPORT_SYNC, RPC_TeleportSync_Client);

            // evtl. weitere client sync handlers hier (use RPCHelper.RegisterClient...)
>>>>>>> a5f1efda1b988fcb24ec202b4d10a4964abc7bf7
        }

        private static void RPC_RequestMercyKill_Server(long sender, ZDOID targetZDOID)
        {
            if(ZNet.instance == null || ZNet.instance.IsServer()) return;

            BetterTamesPlugin.LogIfDebug($"RPC_MercyKill_AllClients triggered for ZDOID: {targetZDOID} from sender: {sender}", DebugFeature.PetProtection);
=======
            // Use RPCHelper which encapsulates readiness and server/client checks.
            RPCHelper.RegisterServer<ZDOID>(BetterTamesPlugin.RPC_REQUEST_MERCY_KILL, RPC_RequestMercyKill_Server);
            RPCHelper.RegisterClient<string>(BetterTamesPlugin.RPC_NOTIFY_MERCY_KILL, RPC_NotifyMercyKill_Client);

            RPCHelper.RegisterClient<string, ZPackage>(BetterTamesPlugin.RPC_TELEPORT_SYNC, RPC_TeleportSync_Client);

            RPCHelper.RegisterServer<string>(BetterTamesPlugin.RPC_REQUEST_UNFOLLOW, RPC_RequestUnfollow_Server);
            RPCHelper.RegisterClient<string>(BetterTamesPlugin.RPC_EXECUTE_UNFOLLOW, RPC_ExecuteUnfollow_Client);
            // evtl. weitere client sync handlers hier (use RPCHelper.RegisterClient...)
        }
        #region Mercy Kill RPCs
        private static void RPC_RequestMercyKill_Server(long sender, ZDOID targetZDOID)
        {
            BetterTamesPlugin.LogIfDebug($"RPC_MercyKill_Server triggered for ZDOID: {targetZDOID} from sender: {sender}", DebugFeature.PetProtection);
>>>>>>> Stashed changes
            ZDO targetZDO = ZDOMan.instance.GetZDO(targetZDOID);
            if (targetZDO == null)
            {
                BetterTamesPlugin.LogIfDebug($"ZDO for ZDOID {targetZDOID} not found. Cannot process MercyKill.", DebugFeature.PetProtection);
                return;
            }

            GameObject targetObject = ZNetScene.instance.FindInstance(targetZDOID);
            if (targetObject == null)
            {
                BetterTamesPlugin.LogIfDebug($"GameObject for ZDOID {targetZDOID} not found, but ZDO exists. Setting flag via ZDO.", DebugFeature.PetProtection);
            }
            else
            {
                BetterTamesPlugin.LogIfDebug($"Found GameObject for ZDOID {targetZDOID}.", DebugFeature.PetProtection);
            }

            // Setze die Flag direkt über die ZDO, unabhängig vom GameObject
            targetZDO.Set("BT_MercyKill", true);
            BetterTamesPlugin.LogIfDebug($"BT_MercyKill flag set for ZDOID {targetZDOID} via ZDO. Pet protection bypassed on next damage.", DebugFeature.PetProtection);
<<<<<<< HEAD
<<<<<<< Updated upstream
        }

=======

            // Immediately notify clients (including the owner) to mark locally — helps avoid replication race
            try
            {
                // Finde den aktuellen Owner (Client) des ZDO
                long ownerID = targetZDO.GetOwner();
                ZNetPeer senderID = ZNet.instance.GetPeer(ownerID);

                ZRoutedRpc.instance.InvokeRoutedRPC(senderID.m_uid, BetterTamesPlugin.RPC_NOTIFY_MERCY_KILL, new object[] { targetZDOID.ToString() });
                BetterTamesPlugin.LogIfDebug($"NotifyMercyKill broadcast sent for ZDOID {targetZDOID}.", DebugFeature.PetProtection);
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception while broadcasting NotifyMercyKill: {ex}", DebugFeature.PetProtection);
            }
        }

=======

            // Immediately notify clients (including the owner) to mark locally — helps avoid replication race
            try
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BetterTamesPlugin.RPC_NOTIFY_MERCY_KILL, new object[] { targetZDOID.ToString() });
                BetterTamesPlugin.LogIfDebug($"NotifyMercyKill broadcast sent for ZDOID {targetZDOID}.", DebugFeature.PetProtection);
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception while broadcasting NotifyMercyKill: {ex}", DebugFeature.PetProtection);
            }
        }

>>>>>>> a5f1efda1b988fcb24ec202b4d10a4964abc7bf7
        // Client receives immediate notify from server and marks the ZDO locally (owner will see it fast)
        private static void RPC_NotifyMercyKill_Client(long sender, string targetZDOID_str)
        {
            try
            {
<<<<<<< HEAD
=======
                if (ZNet.instance == null || ZNet.instance.IsServer()) return;
>>>>>>> a5f1efda1b988fcb24ec202b4d10a4964abc7bf7

                ZDOID zdoid = ParseZDOID(targetZDOID_str);
                if (zdoid.IsNone()) return;

                ZDO zdo = ZDOMan.instance.GetZDO(zdoid);
                if (zdo == null) return;

                // Set local flag immediately so owner logic doesn't lose the race
                zdo.Set("BT_MercyKill", true);
                BetterTamesPlugin.LogIfDebug($"Client received NotifyMercyKill for ZDOID {zdoid}. Local BT_MercyKill = {zdo.GetBool("BT_MercyKill", false)}", DebugFeature.PetProtection);

                // If this client is the owner of the ZDO, perform the authoritative kill locally immediately.
                try
                {
                    if (zdo.IsValid() && zdo.IsOwner())
                    {
                        BetterTamesPlugin.LogIfDebug($"This client is owner for ZDOID {zdoid}. Attempting local owner-side kill.", DebugFeature.PetProtection);

                        ZNetView zview = ZNetScene.instance.FindInstance(zdo);
                        if (zview != null)
                        {
                            Character character = zview.GetComponent<Character>();
                            if (character != null)
                            {
                                // Owner executes the kill (authoritative AI/clientside logic)
                                character.SetHealth(0);
                                BetterTamesPlugin.LogIfDebug($"Owner performed local Kill() for {character.m_name} (ZDOID: {zdoid}).", DebugFeature.PetProtection);
                            }
                            else
                            {
                                BetterTamesPlugin.LogIfDebug($"Owner: found ZNetView but no Character component for ZDOID {zdoid}.", DebugFeature.PetProtection);
                            }
                        }
                        else
                        {
                            BetterTamesPlugin.LogIfDebug($"Owner: no instance found for ZDOID {zdoid} when trying to perform local kill.", DebugFeature.PetProtection);
                        }
                    }
                }
                catch (Exception exOwnerKill)
                {
                    BetterTamesPlugin.LogIfDebug($"Exception while attempting owner local kill in RPC_NotifyMercyKill_Client: {exOwnerKill}", DebugFeature.PetProtection);
                }
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception in RPC_NotifyMercyKill_Client: {ex}", DebugFeature.PetProtection);
            }
        }
<<<<<<< HEAD
        #endregion

        #region Teleport Sync RPCs
>>>>>>> Stashed changes
=======
>>>>>>> a5f1efda1b988fcb24ec202b4d10a4964abc7bf7
        private static void RPC_TeleportSync_Client(long sender, string zdoID_str, ZPackage pkg)
        {
            try
            {

                ZDOID zdoid = ParseZDOID(zdoID_str);
                if (zdoid.IsNone()) return;

                ZDO zdo = ZDOMan.instance.GetZDO(zdoid);
                if (zdo == null || !zdo.IsValid()) return;

                Vector3 position = pkg.ReadVector3();
                Quaternion rotation = pkg.ReadQuaternion();

                rotation = rotation.normalized;
                if (float.IsNaN(rotation.x) || float.IsNaN(rotation.y) || float.IsNaN(rotation.z) || float.IsNaN(rotation.w))
                {
                    BetterTamesPlugin.LogIfDebug("RPC_TeleportSync_Client: received invalid rotation quaternion, ignoring rotation.", DebugFeature.TeleportFollow);
                    rotation = Quaternion.identity;
                }

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

<<<<<<< Updated upstream

<<<<<<< HEAD
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
=======
        #endregion

        #region Unfollow RPCs
        // Neu: Owner-RPC-Handler um autoritativ Unfollow zu setzen
        private static void RPC_RequestUnfollow_Server(long sender, string targetZDOID_str)
        {

            ZDOID zdoid = ParseZDOID(targetZDOID_str);
            if (zdoid.IsNone()) return;

            ZDO zdo = ZDOMan.instance.GetZDO(zdoid);
            if (zdo == null) return;

            // Finde den aktuellen Owner (Client) des ZDO
            long ownerID = zdo.GetOwner();
            ZNetPeer senderID = ZNet.instance.GetPeer(ownerID);

            BetterTamesPlugin.LogIfDebug($"ZDO Owner wird ausgelesen vom Tier: {ownerID} ist der Owner. Bearbeite Unfollow.", DebugFeature.MakeCommandable);
            if (ownerID == 0)
            {
                BetterTamesPlugin.LogIfDebug($"ZDO {zdoid} hat keinen Owner. Unfollow nicht möglich.", DebugFeature.MakeCommandable);
                return;
            }

            // Leite den Befehl an den Owner-Client weiter
            ZRoutedRpc.instance.InvokeRoutedRPC(senderID.m_uid, BetterTamesPlugin.RPC_EXECUTE_UNFOLLOW, new object[] { targetZDOID_str });

            BetterTamesPlugin.LogIfDebug($"Unfollow-Request von {sender} an Owner-Client {ownerID} für {zdoid} weitergeleitet.", DebugFeature.MakeCommandable);
        }

        private static void RPC_ExecuteUnfollow_Client(long sender, string targetZDOID_str)
        {
            try
            {

                ZDOID zdoid = ParseZDOID(targetZDOID_str);
                if (zdoid.IsNone()) return;

                ZDO zdo = ZDOMan.instance.GetZDO(zdoid);
                if (zdo == null) return;

                // Der Server hat diesen RPC nur an den Owner gesendet.
                if (!zdo.IsValid() || !zdo.IsOwner())
                {
                    BetterTamesPlugin.LogIfDebug($"RPC_ExecuteUnfollow_Client: Received RPC but I am not the Owner for {zdoid}. Ignoring.", DebugFeature.MakeCommandable);
                    return;
                }

                // Führe die autoritative Änderung aus
                zdo.Set(ZDOVars.s_follow, "");

                ZNetView zview = ZNetScene.instance.FindInstance(zdo);
                if (zview != null)
                {
                    Character character = zview.GetComponent<Character>();
                    character?.GetComponent<MonsterAI>()?.SetFollowTarget(null);
                }

                BetterTamesPlugin.LogIfDebug($"RPC_ExecuteUnfollow_Client: Successfully cleared follow for {zdoid} (Executed by Owner).", DebugFeature.MakeCommandable);
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Exception in RPC_ExecuteUnfollow_Client: {ex}", DebugFeature.MakeCommandable);
>>>>>>> Stashed changes
            }
            return positions;
        }

    #endregion

=======
>>>>>>> a5f1efda1b988fcb24ec202b4d10a4964abc7bf7
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


    }
}
