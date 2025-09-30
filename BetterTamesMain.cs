using BepInEx;
using BetterTames.ConfigSynchronization;
using HarmonyLib;
using System;
using System.Collections.Generic;

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

        public const string RPC_REQUEST_PET_PROTECTION = "BT_RequestPetProtection";
        public const string RPC_PET_PROTECTION_SYNC = "BT_PetProtectionSync";
        public const string RPC_TELEPORT_SYNC = "BT_TeleportSync";
        public const string RPC_PREPARE_PETS_FOR_TELEPORT = "BT_PreparePetsForTeleport";
        public const string RPC_RECREATE_PETS_AT_DESTINATION = "BT_RecreatePetsAtDest";
        #endregion

        #region Properties
        public static BetterTamesPlugin Instance { get; private set; }
        public static ConfigSync ConfigInstance { get; private set; }
        public static ServerSync.ConfigSync _configSync;
        private readonly Harmony _harmony = new Harmony(PluginId);
        private static bool _corePatchesAppliedSession = false;
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

            // Subscribe to config changes
            ConfigInstance.Tames.PetProtectionExceptionPrefabs.SettingChanged += OnExceptionPrefabsSettingChanged;

            // Apply essential patches that need to run early
            ApplyInitialPatches();
        }

        private void OnDestroy()
        {
            LogIfDebug("OnDestroy called. Unpatching Harmony...", DebugFeature.Initialization);
            _harmony?.UnpatchSelf();
        }
        #endregion

        #region Initialization
        public static void OnZNetReady()
        {
            LogIfDebug("ZNet is ready. Registering RPCs...", DebugFeature.Initialization);
            BetterTames.Utils.RPCManager.RegisterRPCs();
        }

        public static void OnLocalPlayerReady()
        {
            LogIfDebug("Local player is ready.", DebugFeature.Initialization);

            // NEU: Initialisiere den PetProtectionPatch (lädt das Wisp-Prefab)
            PetProtection.PetProtectionPatch.Initialize();

            // (Der Rest der Methode bleibt gleich)
            if (!_corePatchesAppliedSession)
            {
                ApplyCorePatches();
                _corePatchesAppliedSession = true;
            }
        }
        #endregion

        #region Harmony Patches
        private void ApplyInitialPatches()
        {
            try
            {
                LogIfDebug("Applying initial patches (Initialization & PetProtection)...", DebugFeature.Initialization);
                _harmony.PatchAll(typeof(PetProtection.PetProtectionPatch));
                _harmony.PatchAll(typeof(InitializationPatches));
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
                Instance._harmony.PatchAll(typeof(DistanceTeleport.DistanceTeleportPatch));
                Instance._harmony.PatchAll(typeof(PetProtection.StunBehaviorPatches));
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
            // Update the exception list when the config is changed
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
                    shouldLog = true; // Always log initialization steps for now
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