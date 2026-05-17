using UnityEngine;
using Unity.Netcode;

namespace KingOfTheHill.Players
{
    /// <summary>
    /// Genera automáticamente un indicador visual en forma de ">"
    /// frente al jugador local para saber a dónde atacará.
    /// </summary>
    public class PlayerAimIndicator : NetworkBehaviour
    {
        private LineRenderer _lineRenderer;

        public override void OnNetworkSpawn()
        {
            // Solo creamos el indicador para el jugador local, no queremos ver a dónde miran los bots
            if (!IsLocalPlayer) return;

            // Creamos un LineRenderer en el jugador
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = false; // Se moverá y rotará junto con el jugador
            _lineRenderer.positionCount = 3;     // Tres puntos para hacer una "V" invertida
            
            // Ancho de la línea
            _lineRenderer.startWidth = 0.15f;
            _lineRenderer.endWidth = 0.15f;

            // Dibujar la forma del ">" en el suelo, delante del personaje
            _lineRenderer.SetPosition(0, new Vector3(-0.7f, 0.1f, 1.5f)); // Izquierda
            _lineRenderer.SetPosition(1, new Vector3(0f,    0.1f, 2.5f)); // Punta (Centro adelante)
            _lineRenderer.SetPosition(2, new Vector3(0.7f,  0.1f, 1.5f)); // Derecha

            // Material básico de Unity para que brille un poco
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.startColor = new Color(1f, 0.2f, 0.2f, 0.8f); // Rojo semi-transparente
            _lineRenderer.endColor   = new Color(1f, 0.2f, 0.2f, 0.8f);

            // Quitamos sombras
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
        }
    }
}
