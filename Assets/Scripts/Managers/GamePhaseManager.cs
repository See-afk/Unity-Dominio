using Unity.Netcode;
using UnityEngine;
using TMPro;
using System;
using System.Collections;

namespace KingOfTheHill.Managers
{
    public enum GamePhase { Waiting, Countdown, Playing, Finished }

    /// <summary>
    /// Maneja el estado global del juego (Cuenta regresiva, jugando, terminado).
    /// Habilita la cámara cinemática durante la espera y la UI táctil al jugar.
    /// </summary>
    public class GamePhaseManager : NetworkBehaviour
    {
        public static GamePhaseManager Singleton { get; private set; }

        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Waiting);
        public NetworkVariable<int> CountdownTimer = new NetworkVariable<int>(0);

        public Action<GamePhase> OnPhaseChanged;

        [Header("Referencias de la Escena")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private GameObject cinematicCamera;
        [SerializeField] private GameObject mobileUIRoot; // Canvas o Root de controles táctiles

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
                Destroy(gameObject);
            else
                Singleton = this;
        }

        public override void OnNetworkSpawn()
        {
            CurrentPhase.OnValueChanged += (oldPhase, newPhase) => 
            {
                OnPhaseChanged?.Invoke(newPhase);
                UpdatePhaseUI(newPhase);
            };

            CountdownTimer.OnValueChanged += (oldVal, newVal) => 
            {
                if (countdownText != null && CurrentPhase.Value == GamePhase.Countdown)
                {
                    if (newVal > 0)
                        countdownText.text = newVal.ToString();
                    else
                        countdownText.text = "¡GO!";
                }
            };

            if (IsServer)
            {
                StartCoroutine(ServerPhaseFlow());
            }

            // Forzar actualización inicial para clientes y host
            UpdatePhaseUI(CurrentPhase.Value);
        }

        private IEnumerator ServerPhaseFlow()
        {
            CurrentPhase.Value = GamePhase.Waiting;
            
            // Esperamos 2 segundos en Waiting para que carguen los clientes
            yield return new WaitForSeconds(2f); 

            CurrentPhase.Value = GamePhase.Countdown;
            CountdownTimer.Value = 3;

            // Cuenta regresiva: 3, 2, 1
            while (CountdownTimer.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                CountdownTimer.Value--;
            }

            // Un segundo extra para mostrar el "¡GO!"
            yield return new WaitForSeconds(1f); 
            CurrentPhase.Value = GamePhase.Playing;

            ClearTextClientRpc();
        }

        [ClientRpc]
        private void ClearTextClientRpc()
        {
            if (countdownText != null) countdownText.text = "";
        }

        private void UpdatePhaseUI(GamePhase phase)
        {
            bool isPlaying = (phase == GamePhase.Playing);
            
            // La cámara cinemática funciona mientras NO se esté jugando (Waiting, Countdown, Finished)
            if (cinematicCamera != null)
                cinematicCamera.SetActive(!isPlaying);

            // Los controles táctiles se muestran SOLO cuando se está jugando
            if (mobileUIRoot != null)
            {
#if UNITY_EDITOR || UNITY_ANDROID
                mobileUIRoot.SetActive(isPlaying);
#else
                mobileUIRoot.SetActive(false);
#endif
            }

            if (countdownText != null && isPlaying)
                countdownText.text = "";
        }
    }
}
