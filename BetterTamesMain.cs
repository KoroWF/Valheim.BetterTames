using BepInEx;
using BetterTames.ConfigSynchronization;
using BetterTames.PetProtection;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BetterTames
{
    public enum DebugFeature
    {
        MakeCommandable,
        TeleportFollow,
        PetProtection,
        Initialization
    }

    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    public class BetterTamesPlugin : BaseUnityPlugin
    {
        #region Constants
        public const string PluginId = "Koro.bettertames";
        public const string PluginName = "BetterTames";
        public const string PluginVersion = "0.0.4";

        // Fix: Nur Server-Only RPCs
        public const string RPC_PREPARE_PETS_FOR_TELEPORT = "BT_PreparePetsForTeleport";
        public const string RPC_RECREATE_PETS_AT_DESTINATION = "BT_RecreatePetsAtDest";
        public const string RPC_REQUEST_PET_STUN = "BT_RequestPetStun";

        private static readonly Dictionary<long, int> playerFollowerCounts = new Dictionary<long, int>();
        #endregion

        #region Properties
        private static int CachedMaxPets;
        private static bool CachedPetProtectionEnabled;
        public static BetterTamesPlugin Instance { get; private set; }
        public static ConfigSync ConfigInstance { get; private set; }
        public static ServerSync.ConfigSync _configSync;
        private readonly Harmony _harmony = new Harmony(PluginId);
        #endregion

        #region Lifecycle Methods
        private void Awake()
        {
            Instance = this;

            _configSync = new ServerSync.ConfigSync(PluginId)
            {
                DisplayName = PluginName,
                CurrentVersion = PluginVersion,
                MinimumRequiredVersion = PluginVersion,
                ModRequired = true
            };
            ConfigInstance = new ConfigSync(this);

            LogIfDebug("AWAKE: Config instances initialized.", DebugFeature.Initialization);

            CachedMaxPets = ConfigInstance.Tames.MaxFollowingPets.Value;
            CachedPetProtectionEnabled = ConfigInstance.Tames.PetProtectionEnabled.Value;

            _harmony.PatchAll();  // Globale Patches
        }


        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
        #endregion

        #region Initialization Hooks
        public static void OnZNetReady()
        {
            LogIfDebug("OnZNetReady called.", DebugFeature.Initialization);
            Utils.RPCManager.RegisterRPCs();

            PetProtectionPatch.UpdateExceptionPrefabs(ConfigInstance.Tames.PetProtectionExceptionPrefabs.Value);
        }

        public static void OnLocalPlayerReady()
        {
            LogIfDebug("OnLocalPlayerReady called for local player.", DebugFeature.Initialization);

            PetProtectionPatch.Initialize(); 
            LogIfDebug("PetProtection initialized.", DebugFeature.PetProtection);
            // Füge unsere performante Checker-Komponente zum Spieler hinzu.
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetComponent<PetDistanceChecker>() == null)
            {
                Player.m_localPlayer.gameObject.AddComponent<PetDistanceChecker>();
                LogIfDebug("PetDistanceChecker component added to local player.", DebugFeature.TeleportFollow);
            }

        }

        #endregion

        #region Logging
        public static void LogIfDebug(string message, DebugFeature feature = DebugFeature.Initialization)
        {
            if (ConfigInstance == null) return;

            bool shouldLog;
            switch (feature)
            {
                case DebugFeature.MakeCommandable:
                    shouldLog = ConfigInstance.Tames.DebugMakeCommandable.Value;
                    break;
                case DebugFeature.TeleportFollow:
                    shouldLog = ConfigInstance.Tames.DebugTeleportFollow.Value;
                    break;
                case DebugFeature.PetProtection:
                    shouldLog = ConfigInstance.Tames.DebugPetProtection.Value;
                    break;
                case DebugFeature.Initialization:
                    shouldLog = true;
                    break;
                default:
                    shouldLog = false;
                    break;
            }

            if (shouldLog)
            {
                Instance.Logger.LogInfo($"[{feature}] {message}");
            }
        }
        #endregion

        #region MakeCommandable Helper
        public static void UpdateFollowerCount(Player player, bool isFollowing)
        {
            long playerId = player.GetPlayerID();
            if (!playerFollowerCounts.ContainsKey(playerId))
                playerFollowerCounts[playerId] = 0;

            int current = playerFollowerCounts[playerId];
            int max = ConfigInstance.Tames.MaxFollowingPets.Value;
            if (isFollowing && current < max)
                playerFollowerCounts[playerId]++;
            else if (!isFollowing && current > 0)
                playerFollowerCounts[playerId]--;

            LogIfDebug($"Updated follower count for {player.GetPlayerName()}: {playerFollowerCounts[playerId]} (isFollowing: {isFollowing})", DebugFeature.MakeCommandable);
        }

        public static int GetFollowerCount(Player player)
        {
            long playerId = player.GetPlayerID();
            return playerFollowerCounts.ContainsKey(playerId) ? playerFollowerCounts[playerId] : 0;
        }
        #endregion
    }
}