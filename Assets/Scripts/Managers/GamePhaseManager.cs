using System;
using System.Collections;
using System.Collections.Generic;
using KingOfTheHill.Players;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingOfTheHill.Managers
{
    public enum GamePhase { Waiting, Countdown, Playing, Finished }

    /// <summary>
    /// Maneja el estado global del juego: espera, cuenta regresiva, partida y resultados.
    /// </summary>
    public class GamePhaseManager : NetworkBehaviour
    {
        public static GamePhaseManager Singleton { get; private set; }

        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Waiting);
        public NetworkVariable<int> CountdownTimer = new NetworkVariable<int>(0);
        public NetworkVariable<int> MatchTimer = new NetworkVariable<int>(0);
        public NetworkVariable<ulong> WinnerClientId = new NetworkVariable<ulong>(ulong.MaxValue);
        public NetworkVariable<int> WinningScore = new NetworkVariable<int>(0);

        public Action<GamePhase> OnPhaseChanged;

        [Header("Partida")]
        [SerializeField] private int matchDurationSeconds = 180;
        [SerializeField] private string mainMenuSceneName = "MainMenu_Scene";

        [Header("Referencias de la Escena")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private GameObject cinematicCamera;
        [SerializeField] private GameObject mobileUIRoot;

        [Header("UI del Scoreboard (Canvas)")]
        [SerializeField] private TextMeshProUGUI matchTimerText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultSubtitleText;
        [SerializeField] private TextMeshProUGUI scoreboardText;
        [SerializeField] private UnityEngine.UI.Button menuButton;
        [SerializeField] private UnityEngine.UI.Button restartButton;

        private bool _isLeavingToMenu;
        private bool _isRestartingMatch;

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
                Destroy(gameObject);
            else
                Singleton = this;

            if (menuButton != null)
                menuButton.onClick.AddListener(LeaveToMenu);
            if (restartButton != null)
                restartButton.onClick.AddListener(RequestRestartMatch);
        }

        public override void OnNetworkSpawn()
        {
            CurrentPhase.OnValueChanged += HandlePhaseChanged;
            CountdownTimer.OnValueChanged += HandleCountdownChanged;

            if (IsServer)
                StartCoroutine(ServerPhaseFlow());

            UpdatePhaseUI(CurrentPhase.Value);
        }

        public override void OnNetworkDespawn()
        {
            CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            CountdownTimer.OnValueChanged -= HandleCountdownChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (CurrentPhase.Value == GamePhase.Playing)
            {
                if (matchTimerText != null)
                    matchTimerText.text = FormatTime(MatchTimer.Value);
            }
        }

        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            OnPhaseChanged?.Invoke(newPhase);
            UpdatePhaseUI(newPhase);
        }

        private void HandleCountdownChanged(int oldVal, int newVal)
        {
            if (countdownText == null || CurrentPhase.Value != GamePhase.Countdown) return;

            countdownText.text = newVal > 0 ? newVal.ToString() : "GO!";
        }

        private IEnumerator ServerPhaseFlow()
        {
            CurrentPhase.Value = GamePhase.Waiting;
            yield return new WaitForSeconds(2f);

            CurrentPhase.Value = GamePhase.Countdown;
            CountdownTimer.Value = 3;

            while (CountdownTimer.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                CountdownTimer.Value--;
            }

            yield return new WaitForSeconds(1f);

            WinnerClientId.Value = ulong.MaxValue;
            WinningScore.Value = 0;
            MatchTimer.Value = Mathf.Max(1, matchDurationSeconds);
            CurrentPhase.Value = GamePhase.Playing;

            ClearTextClientRpc();

            while (MatchTimer.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                MatchTimer.Value--;
            }

            FinishMatch();
        }

        [ClientRpc]
        private void ClearTextClientRpc()
        {
            if (countdownText != null)
                countdownText.text = "";
        }

        private void FinishMatch()
        {
            if (!IsServer) return;

            PlayerStats winner = FindWinner();
            WinnerClientId.Value = winner != null ? winner.OwnerClientId : ulong.MaxValue;
            WinningScore.Value = winner != null ? winner.Score.Value : 0;
            CurrentPhase.Value = GamePhase.Finished;
        }

        private PlayerStats FindWinner()
        {
            PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);
            PlayerStats winner = null;

            for (int i = 0; i < players.Length; i++)
            {
                PlayerStats current = players[i];
                if (current == null || !current.IsSpawned) continue;

                if (winner == null || current.Score.Value > winner.Score.Value)
                    winner = current;
            }

            return winner;
        }

        private void UpdatePhaseUI(GamePhase phase)
        {
            bool isPlaying = phase == GamePhase.Playing;

            if (cinematicCamera != null)
                cinematicCamera.SetActive(!isPlaying);

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

            if (matchTimerText != null)
                matchTimerText.gameObject.SetActive(isPlaying);

            if (resultPanel != null)
                resultPanel.SetActive(phase == GamePhase.Finished);

            if (phase == GamePhase.Finished)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                RefreshResultScreen();
            }
        }

        private void RefreshResultScreen()
        {
            bool localWon = NetworkManager.Singleton != null
                && WinnerClientId.Value == NetworkManager.Singleton.LocalClientId;

            if (resultTitleText != null)
                resultTitleText.text = localWon ? "VICTORIA" : "DERROTA";

            if (resultSubtitleText != null)
                resultSubtitleText.text = GetWinnerText();

            if (scoreboardText != null)
            {
                var sb = new System.Text.StringBuilder();
                List<PlayerStats> players = GetSortedPlayers();
                
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerStats stats = players[i];
                    string marker = stats.OwnerClientId == WinnerClientId.Value ? "1." : $"{i + 1}.";
                    sb.AppendLine($"{marker} {stats.PlayerName.Value}    {stats.Score.Value} pts");
                }
                scoreboardText.text = sb.ToString();
            }
        }

        private List<PlayerStats> GetSortedPlayers()
        {
            List<PlayerStats> playersList = new List<PlayerStats>();
            PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsSpawned)
                    playersList.Add(players[i]);
            }
            playersList.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));
            return playersList;
        }

        private string GetWinnerText()
        {
            PlayerStats winner = FindPlayerByClientId(WinnerClientId.Value);
            if (winner == null)
                return "Sin ganador";

            return $"Gano {winner.PlayerName.Value} con {WinningScore.Value} pts";
        }

        private PlayerStats FindPlayerByClientId(ulong clientId)
        {
            PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsSpawned && players[i].OwnerClientId == clientId)
                    return players[i];
            }
            return null;
        }

        public void RequestRestartMatch()
        {
            if (_isRestartingMatch) return;
            _isRestartingMatch = true;

            if (menuButton != null) menuButton.interactable = false;
            if (restartButton != null) restartButton.interactable = false;

            if (IsServer)
                RestartMatch();
            else
                RequestRestartMatchServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRestartMatchServerRpc()
        {
            RestartMatch();
        }

        private void RestartMatch()
        {
            if (!IsServer || NetworkManager.Singleton == null) return;

            string sceneName = SceneManager.GetActiveScene().name;
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public void LeaveToMenu()
        {
            if (_isLeavingToMenu) return;
            _isLeavingToMenu = true;

            if (menuButton != null) menuButton.interactable = false;
            if (restartButton != null) restartButton.interactable = false;

            StartCoroutine(LeaveToMenuRoutine());
        }

        private IEnumerator LeaveToMenuRoutine()
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient))
            {
                NetworkManager.Singleton.Shutdown();
                while (NetworkManager.Singleton.ShutdownInProgress)
                    yield return null;
                
                yield return new WaitForSeconds(0.1f);
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private string FormatTime(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private void OnValidate()
        {
            matchDurationSeconds = Mathf.Max(10, matchDurationSeconds);
        }
    }
}
