using System;
using BepInEx;
using BetterTames.ConfigSynchronization;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace BetterTames
{
    public enum DebugFeature
    {
        MakeCommandable,
        TeleportFollow,
        PetProtection,
        Initialization,
    }

    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class BetterTamesPlugin : BaseUnityPlugin
    {
        #region Constants
        public const string PluginId = "Koro.bettertames";
        public const string PluginName = "BetterTames";
        public const string PluginVersion = "0.0.8";

        public const string RPC_TELEPORT_SYNC = "BT_TeleportSync";

        public const string RPC_REQUEST_MERCY_KILL = "BT_RequestMercyKill";
        public const string RPC_NOTIFY_MERCY_KILL = "BetterTames_NotifyMercyKill";


        #endregion

        #region Properties
        public static BetterTamesPlugin Instance { get; private set; }
        public static ConfigSync ConfigInstance { get; private set; }
        public static ServerSync.ConfigSync _configSync;
        private readonly Harmony _harmony = new Harmony(PluginId);
        private static bool _corePatchesAppliedSession = false;

        // Coroutine handle so we can stop it cleanly
        private Coroutine _petMonitorCoroutine;
        #endregion

        #region Lifecycle Methods
        private void Awake()
        {
            Instance = this;

            // Initialize ServerSync and our custom config wrapper
            _configSync = new ServerSync.ConfigSync(PluginId)
            {
                DisplayName = PluginName,
                CurrentVersion = PluginVersion,
                MinimumRequiredVersion = PluginVersion,
                ModRequired = true
            };
            ConfigInstance = new ConfigSync(this);

            LogIfDebug("AWAKE: Config instances initialized.", DebugFeature.Initialization);


            ConfigInstance.Tames.PetProtectionExceptionPrefabs.SettingChanged += OnExceptionPrefabsSettingChanged;

            ApplyInitialPatches();
        }


        private void OnDestroy()
        {
            LogIfDebug("OnDestroy called. Unpatching Harmony...", DebugFeature.Initialization);
            _harmony?.UnpatchSelf();

            try
            {
                if (_petMonitorCoroutine != null && Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.StopCoroutine(_petMonitorCoroutine);
                    _petMonitorCoroutine = null;
                    LogIfDebug("Stopped PlayerPetMonitor coroutine on destroy.", DebugFeature.TeleportFollow);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed stopping PlayerPetMonitor coroutine: {ex}");
            }
        }
        #endregion

        #region Initialization
        public static void OnZNetReady()
        {
            BetterTames.Utils.RPCManager.RegisterRPCs();
        }

        public static void OnLocalPlayerReady()
        {
            LogIfDebug("Local player is ready.", DebugFeature.Initialization);

            PetProtection.PetProtectionPatch.Initialize();
            if (!_corePatchesAppliedSession)
            {
                ApplyCorePatches();
                _corePatchesAppliedSession = true;
            }
            new GameObject("BT_TeleportMonitor").AddComponent<BetterTames.DistanceTeleport.TeleportMonitorBehaviour>();

        }
        #endregion

        #region Harmony Patches
        private void ApplyInitialPatches()
        {
            try
            {
                LogIfDebug("Applying initial patches (Initialization & PetProtection)...", DebugFeature.Initialization);
                _harmony.PatchAll(typeof(PetProtection.PetProtectionPatch));
                _harmony.PatchAll(typeof(PetProtection.StunBehaviorPatches));
                _harmony.PatchAll(typeof(InitializationPatches));
                _harmony.PatchAll(typeof(PetProtection.EnemyHud_TestShow_Patch));
                LogIfDebug("Initial patches applied.", DebugFeature.Initialization);
            }
            catch (Exception ex)
            {
                Logger.LogError($"CRITICAL ERROR applying initial patches: {ex}");
            }
        }

        private static void ApplyCorePatches()
        {
            try
            {
                LogIfDebug("Applying core feature patches...", DebugFeature.Initialization);
                Instance._harmony.PatchAll(typeof(MakeCommandable.MakeCommandablePatch));
                Instance._harmony.PatchAll(typeof(MakeCommandable.Player_UpdateTeleport_Patch));
                Instance._harmony.PatchAll(typeof(PetProtection.ButcherKnifePatch));
                LogIfDebug("Core feature patches applied.", DebugFeature.Initialization);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"Exception during core patching: {ex}");
            }
        }
        #endregion

        #region Event Handlers
        private void OnExceptionPrefabsSettingChanged(object sender, EventArgs e)
        {
            PetProtection.PetProtectionPatch.UpdateExceptionPrefabs(ConfigInstance.Tames.PetProtectionExceptionPrefabs.Value);
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
    }
}