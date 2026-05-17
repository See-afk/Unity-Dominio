using UnityEngine;
using Unity.Netcode;
#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace KingOfTheHill.Gameplay
{
    /// <summary>
    /// Se adjunta al Prefab del Jugador.
    /// Crea el CameraTarget (intermediario) al spawnear y configura 
    /// la cámara Cinemachine Top-Down para que lo siga.
    /// </summary>
    public class TopDownCameraSetup : NetworkBehaviour
    {
        [Header("TopDown Camera Config")]
        [SerializeField] private float damping = 3f;
        [SerializeField] private float forwardLookAhead = 3f;
        [SerializeField] private float deadZoneRadius = 1f;

        private GameObject _cameraTargetGO;

        public override void OnNetworkSpawn()
        {
            // Solo configuramos la cámara para el jugador local, ignoramos bots/remotos
            if (!IsLocalPlayer) return;

            // Inyectar CinemachineBrain a la cámara nativa si no lo tiene
            Camera playerCam = GetComponentInChildren<Camera>(true);
            if (playerCam != null)
            {
#if UNITY_6000_0_OR_NEWER
                if (playerCam.GetComponent<Unity.Cinemachine.CinemachineBrain>() == null)
                    playerCam.gameObject.AddComponent<Unity.Cinemachine.CinemachineBrain>();
#else
                if (playerCam.GetComponent<CinemachineBrain>() == null)
                    playerCam.gameObject.AddComponent<CinemachineBrain>();
#endif
            }

            SetupTopDownCamera();
        }

        private void SetupTopDownCamera()
        {
            // 1. Instanciamos el objeto intermediario independiente del jugador
            _cameraTargetGO = new GameObject($"CameraTarget_{gameObject.name}");
            _cameraTargetGO.transform.position = transform.position;
            
            // Le agregamos la lógica de suavizado y deadzone
            var targetScript = _cameraTargetGO.AddComponent<TopDownCameraTarget>();
            targetScript.playerTransform = transform;
            targetScript.damping = damping;
            targetScript.forwardLookAhead = forwardLookAhead;
            targetScript.deadZoneRadius = deadZoneRadius;

            // 2. Buscamos la cámara Cinemachine de la escena
#if UNITY_6000_0_OR_NEWER
            var cinemachineCam = Object.FindAnyObjectByType<CinemachineCamera>();
#else
            var cinemachineCam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
#endif

            if (cinemachineCam != null)
            {
                // 3. Asignamos nuestro intermediario como el objetivo a seguir
#if UNITY_6000_0_OR_NEWER
                var targets = cinemachineCam.Target;
                targets.TrackingTarget = _cameraTargetGO.transform;
                // En modo TopDown, la cámara no debe rotar (LookAt = null), 
                // mantiene su ángulo fijo de ~45 grados
                targets.LookAtTarget = null; 
                cinemachineCam.Target = targets;

                cinemachineCam.enabled = false;
                cinemachineCam.enabled = true; // Reiniciar pipeline
#else
                cinemachineCam.Follow = _cameraTargetGO.transform;
                cinemachineCam.LookAt = null;
#endif
                Debug.Log("[TopDownCameraSetup] Cámara TopDown asignada al intermediario.");
            }
        }

        public override void OnNetworkDespawn()
        {
            // Destruir el intermediario al desconectarnos para no dejar basura
            if (IsLocalPlayer && _cameraTargetGO != null)
            {
                Destroy(_cameraTargetGO);
            }
        }
    }
}
