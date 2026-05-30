using UnityEngine;

namespace KingOfTheHill.Gameplay
{
    /// <summary>
    /// Rota una cámara alrededor de un objetivo específico (para la fase de cuenta regresiva).
    /// </summary>
    public class CinematicCameraRotator : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float speed = 15f;
        [SerializeField] private float heightOffset = 10f;
        [SerializeField] private float distance = 15f;
        
        [Tooltip("Posición a mirar si no hay un target asignado.")]
        [SerializeField] private Vector3 defaultTargetPosition = Vector3.zero;

        private void Start()
        {
            Vector3 centerPos = target != null ? target.position : defaultTargetPosition;
            
            // Posición inicial alejada
            transform.position = centerPos + new Vector3(distance, heightOffset, -distance);
            transform.LookAt(centerPos);
        }

        private void Update()
        {
            Vector3 centerPos = target != null ? target.position : defaultTargetPosition;
            
            transform.RotateAround(centerPos, Vector3.up, speed * Time.deltaTime);
            transform.LookAt(centerPos);
        }
    }
}
