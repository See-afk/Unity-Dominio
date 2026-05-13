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

        private void Start()
        {
            if (target != null)
            {
                // Posición inicial alejada
                transform.position = target.position + new Vector3(distance, heightOffset, -distance);
                transform.LookAt(target);
            }
        }

        private void Update()
        {
            if (target != null)
            {
                transform.RotateAround(target.position, Vector3.up, speed * Time.deltaTime);
                transform.LookAt(target);
            }
        }
    }
}
