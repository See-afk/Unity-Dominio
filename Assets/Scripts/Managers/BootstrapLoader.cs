using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dominio.Managers
{
    /// <summary>
    /// Bootstrap: primera escena en ejecutarse (índice 0 en Build Settings).
    /// No contiene gráficos. Inicializa sistemas globales y carga MainMenu.
    /// </summary>
    public class BootstrapLoader : MonoBehaviour
    {
        [Header("Configuración")]
        [Tooltip("Nombre exacto de la escena del menú principal.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu_Scene";

        [Tooltip("Tiempo mínimo en pantalla de carga antes de pasar al menú (segundos).")]
        [SerializeField] private float minimumLoadTime = 1.5f;

        private void Start()
        {
            StartCoroutine(InitializeAndLoad());
        }

        private IEnumerator InitializeAndLoad()
        {
            // 1. Inicializar datos globales
            GameData.Reset();

            // 2. Aquí puedes inicializar otros sistemas futuros
            //    (analytics, SDKs, etc.) de forma no bloqueante.

            // 3. Esperar tiempo mínimo (útil para mostrar un splash screen)
            yield return new WaitForSeconds(minimumLoadTime);

            // 4. Cargar la escena del menú principal
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
