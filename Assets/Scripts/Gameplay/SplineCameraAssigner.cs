using UnityEngine;
using Unity.Netcode;
#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine; // Cinemachine 3 (Unity 6)
#else
using Cinemachine; // Cinemachine 2
#endif

namespace KingOfTheHill.Gameplay
{
    /// <summary>
    /// Se encarga de buscar a la cámara Cinemachine en la escena y asignarle 
    /// el jugador local como objetivo de LookAt y de seguimiento (Tracking) 
    /// para que funcione con el Spline Dolly.
    /// </summary>
    public class SplineCameraAssigner : NetworkBehaviour
    {
#if UNITY_6000_0_OR_NEWER
        private CinemachineCamera _cinemachineCamera;
#else
        private CinemachineVirtualCamera _cinemachineCamera;
#endif

        [SerializeField] private Transform cameraTargetPoint; // Opcional: Un punto específico en el jugador a donde mirar (ej. la cabeza o el pecho)

        public override void OnNetworkSpawn()
        {
            // Solo queremos que la cámara siga al jugador local real (ignorar bots aunque seamos el Host)
            if (!IsLocalPlayer) return;

            // Para que Cinemachine funcione, la cámara real (Unity Camera) necesita un CinemachineBrain.
            // Buscamos la cámara del jugador y se lo añadimos si no lo tiene.
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
                Debug.Log("[SplineCameraAssigner] CinemachineBrain inyectado en la cámara del jugador.");
            }

            AssignCameraTarget();
        }

        private void AssignCameraTarget()
        {
#if UNITY_6000_0_OR_NEWER
            _cinemachineCamera = Object.FindAnyObjectByType<CinemachineCamera>();
#else
            _cinemachineCamera = Object.FindFirstObjectByType<CinemachineVirtualCamera>();
#endif

            if (_cinemachineCamera != null)
            {
                Transform targetToFollow = cameraTargetPoint != null ? cameraTargetPoint : transform;

#if UNITY_6000_0_OR_NEWER
                var targets = _cinemachineCamera.Target;
                targets.TrackingTarget = targetToFollow;
                targets.LookAtTarget = targetToFollow;
                _cinemachineCamera.Target = targets;

                // Apagamos y encendemos la cámara para forzar que Cinemachine actualice
                _cinemachineCamera.enabled = false;
                _cinemachineCamera.enabled = true;

                // Desactivamos el AutoDolly nativo, usaremos nuestro propio algoritmo de "Tren"
                if (_cinemachineCamera.TryGetComponent<Unity.Cinemachine.CinemachineSplineDolly>(out var dolly))
                {
                    var autoDolly = dolly.AutomaticDolly;
                    autoDolly.Enabled = false;
                    dolly.AutomaticDolly = autoDolly;
                }
#else
                _cinemachineCamera.Follow = targetToFollow;
                _cinemachineCamera.LookAt = targetToFollow;
#endif
                Debug.Log($"[SplineCameraAssigner] Cámara asignada al jugador local: {gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[SplineCameraAssigner] No se encontró ninguna cámara Cinemachine en la escena.");
            }
        }

        private float _currentPathPosition = 0f;

        private void Update()
        {
            if (!IsLocalPlayer || _cinemachineCamera == null) return;

#if UNITY_6000_0_OR_NEWER
            if (_cinemachineCamera.TryGetComponent<Unity.Cinemachine.CinemachineSplineDolly>(out var dolly))
            {
                if (dolly.Spline != null && dolly.Spline.Spline != null)
                {
                    Transform target = cameraTargetPoint != null ? cameraTargetPoint : transform;
                    
                    // Convertir la posición del jugador al espacio del Spline
                    Unity.Mathematics.float3 localTargetPos = dolly.Spline.transform.InverseTransformPoint(target.position);
                    
                    float lookAhead = 0.05f; // Qué tan adelante/atrás mira el tren
                    float speed = 0.5f;      // Velocidad del tren por los raíles
                    
                    // Evaluamos 3 puntos en el rail: actual, adelante y atrás
                    Unity.Mathematics.float3 posCurrent = UnityEngine.Splines.SplineUtility.EvaluatePosition(dolly.Spline.Spline, _currentPathPosition);
                    Unity.Mathematics.float3 posForward = UnityEngine.Splines.SplineUtility.EvaluatePosition(dolly.Spline.Spline, (_currentPathPosition + lookAhead) % 1f);
                    
                    float backPath = _currentPathPosition - lookAhead;
                    if (backPath < 0) backPath += 1f;
                    Unity.Mathematics.float3 posBackward = UnityEngine.Splines.SplineUtility.EvaluatePosition(dolly.Spline.Spline, backPath);

                    // Calculamos cuál de los 3 puntos está más cerca del jugador
                    float distCurrent  = Unity.Mathematics.math.distancesq(posCurrent, localTargetPos);
                    float distForward  = Unity.Mathematics.math.distancesq(posForward, localTargetPos);
                    float distBackward = Unity.Mathematics.math.distancesq(posBackward, localTargetPos);

                    // El tren decide moverse hacia el punto que lo acerque más al jugador
                    if (distForward < distCurrent && distForward < distBackward)
                    {
                        _currentPathPosition += speed * Time.deltaTime;
                    }
                    else if (distBackward < distCurrent && distBackward < distForward)
                    {
                        _currentPathPosition -= speed * Time.deltaTime;
                    }

                    // Aseguramos que la posición se mantenga en el rango 0 a 1 (Spline cerrado)
                    if (_currentPathPosition > 1f) _currentPathPosition -= 1f;
                    if (_currentPathPosition < 0f) _currentPathPosition += 1f;

                    // Asignar al Spline Dolly de Cinemachine
                    dolly.CameraPosition = _currentPathPosition;
                }
            }
#endif
        }
    }
}
