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

        private readonly List<PlayerStats> _scoreboardPlayers = new List<PlayerStats>();
        private GUIStyle _timerStyle;
        private GUIStyle _resultTitleStyle;
        private GUIStyle _resultSubtitleStyle;
        private GUIStyle _scoreboardStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _panelStyle;
        private bool _isLeavingToMenu;
        private bool _isRestartingMatch;

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
                Destroy(gameObject);
            else
                Singleton = this;
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
            PlayerStats[] players = FindObjectsByType<PlayerStats>();
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

            if (phase == GamePhase.Finished)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnValidate()
        {
            matchDurationSeconds = Mathf.Max(10, matchDurationSeconds);
        }

        private void OnGUI()
        {
            if (!IsSpawned) return;

            EnsureStyles();

            if (CurrentPhase.Value == GamePhase.Playing)
                DrawMatchTimer();
            else if (CurrentPhase.Value == GamePhase.Finished)
                DrawResultScreen();
        }

        private void DrawMatchTimer()
        {
            Rect rect = new Rect((Screen.width - 240f) * 0.5f, 16f, 240f, 44f);
            GUI.Label(rect, FormatTime(MatchTimer.Value), _timerStyle);
        }

        private void DrawResultScreen()
        {
            float panelWidth = Mathf.Min(560f, Screen.width - 32f);
            float panelHeight = Mathf.Min(520f, Screen.height - 32f);
            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUI.Box(panel, GUIContent.none, _panelStyle);

            bool localWon = NetworkManager.Singleton != null
                && WinnerClientId.Value == NetworkManager.Singleton.LocalClientId;

            Rect titleRect = new Rect(panel.x + 24f, panel.y + 24f, panel.width - 48f, 54f);
            GUI.Label(titleRect, localWon ? "VICTORIA" : "DERROTA", _resultTitleStyle);

            Rect subtitleRect = new Rect(panel.x + 24f, panel.y + 82f, panel.width - 48f, 34f);
            GUI.Label(subtitleRect, GetWinnerText(), _resultSubtitleStyle);

            Rect tableRect = new Rect(panel.x + 34f, panel.y + 138f, panel.width - 68f, panel.height - 228f);
            DrawScoreboard(tableRect);

            DrawResultButtons(panel);
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
            PlayerStats[] players = FindObjectsByType<PlayerStats>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsSpawned && players[i].OwnerClientId == clientId)
                    return players[i];
            }

            return null;
        }

        private void DrawScoreboard(Rect rect)
        {
            RefreshScoreboardPlayers();

            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), "Tabla de puntuacion", _resultSubtitleStyle);

            float y = rect.y + 42f;
            for (int i = 0; i < _scoreboardPlayers.Count; i++)
            {
                PlayerStats stats = _scoreboardPlayers[i];
                if (stats == null) continue;

                string marker = stats.OwnerClientId == WinnerClientId.Value ? "1." : $"{i + 1}.";
                string line = $"{marker} {stats.PlayerName.Value}    {stats.Score.Value} pts";
                GUI.Label(new Rect(rect.x, y, rect.width, 28f), line, _scoreboardStyle);
                y += 32f;
            }
        }

        private void RefreshScoreboardPlayers()
        {
            _scoreboardPlayers.Clear();

            PlayerStats[] players = FindObjectsByType<PlayerStats>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsSpawned)
                    _scoreboardPlayers.Add(players[i]);
            }

            _scoreboardPlayers.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));
        }

        private void DrawResultButtons(Rect panel)
        {
            float buttonWidth = Mathf.Min(220f, (panel.width - 72f) * 0.5f);
            float buttonHeight = 46f;
            float y = panel.yMax - 68f;
            float leftX = panel.x + (panel.width - (buttonWidth * 2f + 24f)) * 0.5f;

            Rect menuRect = new Rect(leftX, y, buttonWidth, buttonHeight);
            Rect restartRect = new Rect(leftX + buttonWidth + 24f, y, buttonWidth, buttonHeight);

            GUI.enabled = !_isLeavingToMenu;
            if (GUI.Button(menuRect, "Salir al menu", _buttonStyle))
                LeaveToMenu();

            GUI.enabled = !_isRestartingMatch && !_isLeavingToMenu;
            if (GUI.Button(restartRect, "Nueva partida", _buttonStyle))
                RequestRestartMatch();

            GUI.enabled = true;
        }

        private void RequestRestartMatch()
        {
            if (_isRestartingMatch) return;
            _isRestartingMatch = true;

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

        private void LeaveToMenu()
        {
            if (_isLeavingToMenu) return;
            _isLeavingToMenu = true;

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

        private void EnsureStyles()
        {
            if (_timerStyle != null) return;

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _resultTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _resultSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _scoreboardStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            Texture2D panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.82f));
            panelTexture.Apply();

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = panelTexture;
        }
    }
}
