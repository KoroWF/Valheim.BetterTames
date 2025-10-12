using HarmonyLib;
using System;

namespace BetterTames
{
    [HarmonyPatch]
    internal static class InitializationPatches
    {
        private static bool _zNetReadyCalled = false;
        private static bool _localPlayerReadyCalled = false;

        /// <summary>
        /// Setzt die Initialisierungs-Flags zurück, nützlich für einen sauberen Neustart im selben Spiel-Client (z.B. Logout/Login).
        /// </summary>
        public static void ResetInitializationFlags()
        {
            BetterTamesPlugin.LogIfDebug("Resetting initialization flags.", DebugFeature.Initialization);
            _zNetReadyCalled = false;
            _localPlayerReadyCalled = false;
        }

        /// <summary>
        /// Patch, der nach dem Initialisieren des Netzwerks ausgeführt wird.
        /// Ruft OnZNetReady auf, um RPCs zu registrieren.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), "Awake")]
        [HarmonyPostfix]
        private static void ZNet_Awake_Postfix(ZNet __instance)
        {
            if (_zNetReadyCalled) return;
            if (ZNet.instance != __instance) return;

            BetterTamesPlugin.LogIfDebug($"ZNet.Awake postfix triggered. Calling OnZNetReady...", DebugFeature.Initialization);
            try
            {
                BetterTamesPlugin.OnZNetReady();
                _zNetReadyCalled = true;
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Error in OnZNetReady from ZNet_Awake_Postfix: {ex}", DebugFeature.Initialization);
            }
        }

        /// <summary>
        /// Patch, der ausgeführt wird, sobald der lokale Spieler im Spiel gesetzt ist.
        /// Ruft OnLocalPlayerReady auf, um Patches anzuwenden, die vom Spieler abhängen.
        /// </summary>
        [HarmonyPatch(typeof(Player), "SetLocalPlayer")]
        [HarmonyPostfix]
        private static void Player_SetLocalPlayer_Postfix()
        {
            if (_localPlayerReadyCalled) return;
            if (Player.m_localPlayer == null) return;

            var harmony = new Harmony("BetterTames");

            BetterTamesPlugin.LogIfDebug($"Player.SetLocalPlayer postfix triggered for {Player.m_localPlayer.GetPlayerName()}. Calling OnLocalPlayerReady...", DebugFeature.Initialization);
            try
            {
                BetterTamesPlugin.OnLocalPlayerReady();
                _localPlayerReadyCalled = true;
            }
            catch (Exception ex)
            {
                BetterTamesPlugin.LogIfDebug($"Error in OnLocalPlayerReady from Player_SetLocalPlayer_Postfix: {ex}", DebugFeature.Initialization);
            }
        }
    }
}