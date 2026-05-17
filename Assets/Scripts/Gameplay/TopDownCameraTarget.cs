using UnityEngine;

namespace KingOfTheHill.Gameplay
{
    /// <summary>
    /// Objeto intermediario que persigue al jugador de manera suavizada.
    /// Se calcula un desplazamiento hacia adelante (LookAhead) y se respeta una Zona Muerta (Dead Zone)
    /// para evitar que la cámara tiemble con micropasos del jugador.
    /// </summary>
    public class TopDownCameraTarget : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("El Transform del jugador local al que seguiremos.")]
        public Transform playerTransform;
        
        [Header("Configuración de Suavizado (Soft Zone)")]
        [Tooltip("Qué tan rápido el objetivo alcanza al jugador (menor = más suave).")]
        public float damping = 5f;
        
        [Header("Predicción (Look Ahead)")]
        [Tooltip("Distancia extra hacia adelante basada en la dirección hacia donde mira el jugador.")]
        public float forwardLookAhead = 3f;
        
        [Header("Zona Muerta (Dead Zone)")]
        [Tooltip("El jugador debe alejarse esta distancia antes de que la cámara empiece a seguirlo.")]
        public float deadZoneRadius = 1f;

        // Variables internas para el cálculo matemático
        private Vector3 _targetPosition;
        private Vector3 _currentVelocity;

        private void Start()
        {
            if (playerTransform != null)
            {
                transform.position = playerTransform.position;
                _targetPosition = playerTransform.position;
            }
        }

        private void LateUpdate()
        {
            if (playerTransform == null) return;

            // 1. Calculamos la posición ideal (Jugador + offset basado en movimiento real)
            // Usamos la velocidad del CharacterController en lugar de hacia dónde mira el jugador.
            // Esto garantiza que girar el personaje con el ratón no afecte a la cámara.
            Vector3 flatVelocity = Vector3.zero;
            if (playerTransform.TryGetComponent<CharacterController>(out var cc))
            {
                flatVelocity = new Vector3(cc.velocity.x, 0, cc.velocity.z);
            }

            // El offset crece gradualmente según qué tan rápido te muevas
            Vector3 lookAheadOffset = flatVelocity * (forwardLookAhead * 0.2f);
            if (lookAheadOffset.magnitude > forwardLookAhead)
            {
                lookAheadOffset = lookAheadOffset.normalized * forwardLookAhead;
            }

            Vector3 idealPosition = playerTransform.position + lookAheadOffset;

            // 2. Aplicamos la Zona Muerta (Dead Zone)
            // Si el jugador se ha movido más allá del radio, actualizamos el destino de la cámara
            float distanceToPlayer = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                                      new Vector3(playerTransform.position.x, 0, playerTransform.position.z));
                                                      
            if (distanceToPlayer > deadZoneRadius || _targetPosition == Vector3.zero)
            {
                _targetPosition = idealPosition;
            }

            // 3. Interpolación y Suavizado (Soft Zone)
            // Movemos este intermediario suavemente hacia la posición calculada
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                _targetPosition, 
                ref _currentVelocity, 
                1f / damping
            );
        }
    }
}
