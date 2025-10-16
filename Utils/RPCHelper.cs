using System;
using UnityEngine;

namespace BetterTames.Utils
{
    /// <summary>
    /// Small helper to centralize safe RPC registration/serialization patterns.
    /// Designed to mirror the helpful parts of Jotunn's RPC handling without pulling the dependency.
    /// </summary>
    public static class RPCHelper
    {
        public static bool Ready => ZRoutedRpc.instance != null && ZNet.instance != null;

        // --- Registration helpers (server-only / client-only) ---
        public static void RegisterServer<T1>(string rpcName, Action<long, T1> handler)
        {
            if (!Ready || !ZNet.instance.IsServer()) return;
            try { ZRoutedRpc.instance.Register<T1>(rpcName, handler); }
            catch (Exception ex) { BetterTamesPlugin.LogIfDebug($"RegisterServer failed for {rpcName}: {ex}", DebugFeature.Initialization); }
        }

        public static void RegisterClient<T1>(string rpcName, Action<long, T1> handler)
        {
            if (!Ready || ZNet.instance.IsServer()) return;
            try { ZRoutedRpc.instance.Register<T1>(rpcName, handler); }
            catch (Exception ex) { BetterTamesPlugin.LogIfDebug($"RegisterClient failed for {rpcName}: {ex}", DebugFeature.Initialization); }
        }

        public static void RegisterClient<T1, T2>(string rpcName, Action<long, T1, T2> handler)
        {
            if (!Ready || ZNet.instance.IsServer()) return;
            try { ZRoutedRpc.instance.Register<T1, T2>(rpcName, handler); }
            catch (Exception ex) { BetterTamesPlugin.LogIfDebug($"RegisterClient failed for {rpcName}: {ex}", DebugFeature.Initialization); }
        }

        public static void RegisterClient<T1, T2, T3>(string rpcName, Action<long, T1, T2, T3> handler)
        {
            if (!Ready || ZNet.instance.IsServer()) return;
            try { ZRoutedRpc.instance.Register<T1, T2, T3>(rpcName, handler); }
            catch (Exception ex) { BetterTamesPlugin.LogIfDebug($"RegisterClient failed for {rpcName}: {ex}", DebugFeature.Initialization); }
        }

        // --- ZDOID helpers (consistent formatting/parsing) ---
        public static ZDOID ParseZDOID(string zdoID_str)
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

        public static string ZDOIDToString(ZDOID zdoid)
        {
            if (zdoid.IsNone()) return string.Empty;
            // Use same format as current codebase expects (user:id)
            return $"{zdoid.UserID}:{zdoid.ID}";
        }

        // --- ZPackage helpers for teleport sync ---
        public static ZPackage CreateTeleportPackage(Vector3 pos, Quaternion rot)
        {
            var pkg = new ZPackage();
            pkg.Write(pos);
            pkg.Write(rot);
            return pkg;
        }

        public static bool TryReadTeleportPackage(ZPackage pkg, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            try
            {
                pos = pkg.ReadVector3();
                rot = pkg.ReadQuaternion();
                return true;
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"TryReadTeleportPackage failed: {ex}", DebugFeature.TeleportFollow);
                return false;
            }
        }
    }
}
