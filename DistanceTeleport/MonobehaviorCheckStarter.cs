using UnityEngine;

namespace BetterTames.DistanceTeleport
{
    public class TeleportMonitorBehaviour : MonoBehaviour
    {
        private Coroutine _monitorCoroutine;
        private bool _fallbackRunning;

        private void Awake()
        {
            // Singleton / einmalig
            if (FindObjectsByType<TeleportMonitorBehaviour>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);

            StartMonitor();
        }

        public void StartMonitor()
        {
            if (_monitorCoroutine != null) return;

            BetterTamesPlugin.LogIfDebug("TeleportMonitorBehaviour: Starting MonitorRoutine...", DebugFeature.TeleportFollow);
            _monitorCoroutine = StartCoroutine(PlayerPetMonitor.MonitorRoutine());

            if (!_fallbackRunning)
            {
                // Prüft periodisch, ob die Coroutine aus irgendeinem Grund null ist, und startet neu.
                InvokeRepeating(nameof(EnsureCoroutineRunning), DistanceTeleportLogic.TELEPORT_CHECK_INTERVAL * 2f, DistanceTeleportLogic.TELEPORT_CHECK_INTERVAL * 2f);
                _fallbackRunning = true;
            }
        }

        private void EnsureCoroutineRunning()
        {
            if (_monitorCoroutine == null)
            {
                BetterTamesPlugin.LogIfDebug("TeleportMonitorBehaviour: Coroutine not running, restarting.", DebugFeature.TeleportFollow);
                _monitorCoroutine = StartCoroutine(PlayerPetMonitor.MonitorRoutine());
            }
        }

        private void OnDestroy()
        {
            if (_monitorCoroutine != null)
            {
                StopCoroutine(_monitorCoroutine);
                _monitorCoroutine = null;
            }
            CancelInvoke();
        }
    }
}