using System.Collections;
using Jotunn.Managers;
using Jotunn.Entities;
using UnityEngine;

namespace BetterTames.Utils
{
    public static class RPCManager
    {
        // Wir speichern die RPC-Objekte statisch, um sie überall aufrufen zu können
        public static CustomRPC MercyKillRPC;
        public static CustomRPC TeleportSyncRPC;

        public static void RegisterRPCs()
        {
            BetterTamesPlugin.LogIfDebug("Registering Modern Jötunn RPCs...", DebugFeature.Initialization);

            // Mercy Kill RPC (Anfrage vom Client -> Logik auf Server -> Antwort an Clients)
            MercyKillRPC = NetworkManager.Instance.AddRPC(
                "BT_MercyKill",
                RPC_RequestMercyKill_Server,
                RPC_NotifyMercyKill_Client
            );

            // Teleport Sync RPC (Meist Server an Clients)
            TeleportSyncRPC = NetworkManager.Instance.AddRPC(
                "BT_TeleportSync",
                null, // Server-Handler falls nötig, hier null
                RPC_TeleportSync_Client
            );
        }

        // SERVER HANDLER: Empfängt ZDOID vom Client
        private static IEnumerator RPC_RequestMercyKill_Server(long sender, ZPackage pkg)
        {
            ZDOID targetZDOID = pkg.ReadZDOID();
            BetterTamesPlugin.LogIfDebug($"Server received MercyKill request for {targetZDOID}", DebugFeature.PetProtection);

            ZDO zdo = ZDOMan.instance.GetZDO(targetZDOID);
            if (zdo != null)
            {
                zdo.Set("BT_MercyKill", true);
                // Rückmeldung an alle/sender schicken
                ZPackage response = new ZPackage();
                response.Write(targetZDOID);
                MercyKillRPC.SendPackage(ZRoutedRpc.Everybody, response);
            }
            yield return null;
        }

        // CLIENT HANDLER: Führt visuellen Kill aus
        private static IEnumerator RPC_NotifyMercyKill_Client(long sender, ZPackage pkg)
        {
            ZDOID targetZDOID = pkg.ReadZDOID();
            GameObject go = ZNetScene.instance.FindInstance(targetZDOID);
            if (go && go.TryGetComponent<Character>(out var character))
            {
                character.SetHealth(0);
                BetterTamesPlugin.LogIfDebug($"Client executed MercyKill on {character.m_name}", DebugFeature.PetProtection);
            }
            yield return null;
        }

        // CLIENT HANDLER: Teleportation synchronisieren
        private static IEnumerator RPC_TeleportSync_Client(long sender, ZPackage pkg)
        {
            ZDOID id = pkg.ReadZDOID();
            Vector3 pos = pkg.ReadVector3();
            Quaternion rot = pkg.ReadQuaternion();

            GameObject go = ZNetScene.instance.FindInstance(id);
            if (go && go.TryGetComponent<Character>(out var character) && !character.IsTeleporting())
            {
                go.transform.position = pos;
                go.transform.rotation = rot;
            }
            yield return null;
        }
    }
}