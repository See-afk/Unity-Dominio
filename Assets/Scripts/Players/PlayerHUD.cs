using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingOfTheHill.Players
{
    /// <summary>
    /// HUD local del jugador: barra de vida, nombre y estado.
    /// Optimización: actualiza UI SOLO cuando cambia el valor (evita SetText cada frame).
    /// </summary>
    public class PlayerHUD : NetworkBehaviour
    {
        // ─── Inspector ─ HUD de ESTE jugador (solo Owner lo ve) ──────────────────
        [Header("HUD Propio (solo Owner)")]
        [SerializeField] private GameObject    ownerHUDRoot;
        [SerializeField] private Slider        healthSlider;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI respawnText;

        // ─── Inspector ─ Billboard sobre la cabeza (visible para todos) ──────────
        [Header("Billboard (sobre cabeza)")]
        [SerializeField] private GameObject         billboardRoot;
        [SerializeField] private TextMeshProUGUI    playerNameText;
        [SerializeField] private Slider             billboardHealthBar;
        [SerializeField] private Image              teamColorIndicator;

        // ─── Inspector ─ Colores de equipo ────────────────────────────────────────
        [Header("Equipos")]
        [SerializeField] private Color[] teamColors = { Color.blue, Color.red, Color.green, Color.yellow };

        // ─── Referencias ──────────────────────────────────────────────────────────
        private PlayerStats _stats;
        private Camera      _mainCamera;

        // ─── Cache para evitar allocs ──────────────────────────────────────────────
        private float _lastHealth     = -1f;
        private int   _lastTeam       = -1;
        private bool  _lastAlive      = true;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        public override void OnNetworkSpawn()
        {
            _mainCamera = Camera.main;

            // Solo el Owner ve el HUD propio
            if (ownerHUDRoot != null)
                ownerHUDRoot.SetActive(IsOwner);

            // Suscribirse a cambios
            _stats.Health.OnValueChanged    += OnHealthChanged;
            _stats.TeamIndex.OnValueChanged += OnTeamChanged;
            _stats.IsAlive.OnValueChanged   += OnAliveChanged;
            _stats.PlayerName.OnValueChanged+= OnNameChanged;

            // Inicializar valores actuales
            RefreshHealthUI(_stats.Health.Value);
            RefreshTeamUI(_stats.TeamIndex.Value);
            RefreshNameUI(_stats.PlayerName.Value.ToString());
            RefreshAliveUI(_stats.IsAlive.Value);
        }

        public override void OnNetworkDespawn()
        {
            _stats.Health.OnValueChanged    -= OnHealthChanged;
            _stats.TeamIndex.OnValueChanged -= OnTeamChanged;
            _stats.IsAlive.OnValueChanged   -= OnAliveChanged;
            _stats.PlayerName.OnValueChanged-= OnNameChanged;
        }

        // ─── Callbacks de NetworkVariable ─────────────────────────────────────────

        private void OnHealthChanged(float old, float newVal) => RefreshHealthUI(newVal);
        private void OnTeamChanged(int old, int newVal)       => RefreshTeamUI(newVal);
        private void OnAliveChanged(bool old, bool newVal)    => RefreshAliveUI(newVal);
        private void OnNameChanged(FixedString32Bytes old, FixedString32Bytes newVal)
            => RefreshNameUI(newVal.ToString());

        // ─── Actualización de UI ──────────────────────────────────────────────────

        private void RefreshHealthUI(float hp)
        {
            // Guard: solo actualiza si cambió
            if (Mathf.Approximately(hp, _lastHealth)) return;
            _lastHealth = hp;

            float normalized = hp / PlayerStats.MaxHealth;

            // HUD propio
            if (IsOwner)
            {
                if (healthSlider != null) healthSlider.value = normalized;
                if (healthText   != null) healthText.SetText("{0:0}", hp);
            }

            // Billboard
            if (billboardHealthBar != null) billboardHealthBar.value = normalized;
        }

        private void RefreshTeamUI(int teamIndex)
        {
            if (teamIndex == _lastTeam) return;
            _lastTeam = teamIndex;

            if (teamColorIndicator != null && teamIndex < teamColors.Length)
                teamColorIndicator.color = teamColors[teamIndex];
        }

        private void RefreshNameUI(string playerName)
        {
            if (playerNameText != null)
                playerNameText.SetText(playerName);
        }

        private void RefreshAliveUI(bool alive)
        {
            _lastAlive = alive;

            // Billboard solo visible si está vivo
            if (billboardRoot != null)
                billboardRoot.SetActive(alive);

            // Texto de respawn (solo Owner)
            if (IsOwner && respawnText != null)
                respawnText.gameObject.SetActive(!alive);
        }

        // ─── Billboard siempre mira a cámara ─────────────────────────────────────

        private void LateUpdate()
        {
            if (billboardRoot == null || !billboardRoot.activeSelf) return;
            if (_mainCamera == null) return;

            // Rotar billboard hacia la cámara sin pitch
            billboardRoot.transform.LookAt(
                billboardRoot.transform.position + _mainCamera.transform.rotation * Vector3.forward,
                _mainCamera.transform.rotation * Vector3.up);
        }
    }
}
