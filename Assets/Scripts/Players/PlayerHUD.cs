using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingOfTheHill.Gameplay;
using KingOfTheHill.Managers;

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
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI respawnText;

        // ─── Inspector ─ Billboard sobre la cabeza (visible para todos) ──────────
        [Header("Billboard (sobre cabeza)")]
        [SerializeField] private GameObject         billboardRoot;
        [SerializeField] private TextMeshProUGUI    playerNameText;
        [SerializeField] private Slider             billboardHealthBar;
        [SerializeField] private Image              teamColorIndicator;
        [SerializeField] private bool               showScoreInBillboard = true;

        // ─── Inspector ─ Colores de equipo ────────────────────────────────────────
        [Header("Equipos")]
        [SerializeField] private Color[] teamColors = { Color.blue, Color.red, Color.green, Color.yellow };

        // ─── Referencias ──────────────────────────────────────────────────────────
        private PlayerStats _stats;
        private Camera      _mainCamera;

        // ─── Cache para evitar allocs ──────────────────────────────────────────────
        private float _lastHealth     = -1f;
        private int   _lastTeam       = -1;
        private int   _lastScore      = -1;
        private bool  _lastAlive      = true;
        private bool  _lastCapturing;
        private string _lastPlayerName = "Player";
        private string _respawnMessage = "";

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();

            if (GetComponent<CaptureZoneDirectionArrow>() == null)
                gameObject.AddComponent<CaptureZoneDirectionArrow>();
        }

        // Usando IsLocalPlayer heredado de NetworkBehaviour
        public override void OnNetworkSpawn()
        {
            _mainCamera = Camera.main;

            // Solo el Owner (que sea un Player, no un Bot) ve el HUD propio
            if (ownerHUDRoot != null)
                ownerHUDRoot.SetActive(IsLocalPlayer);

            // Suscribirse a cambios
            _stats.Health.OnValueChanged    += OnHealthChanged;
            _stats.TeamIndex.OnValueChanged += OnTeamChanged;
            _stats.IsAlive.OnValueChanged   += OnAliveChanged;
            _stats.PlayerName.OnValueChanged+= OnNameChanged;
            _stats.Score.OnValueChanged     += OnScoreChanged;
            _stats.IsInCaptureZone.OnValueChanged += OnCaptureZoneChanged;

            // Inicializar valores actuales
            RefreshHealthUI(_stats.Health.Value);
            RefreshTeamUI(_stats.TeamIndex.Value);
            RefreshNameUI(_stats.PlayerName.Value.ToString());
            RefreshAliveUI(_stats.IsAlive.Value);
            RefreshScoreUI(_stats.Score.Value);
            RefreshCaptureZoneUI(_stats.IsInCaptureZone.Value);
        }

        public override void OnNetworkDespawn()
        {
            _stats.Health.OnValueChanged    -= OnHealthChanged;
            _stats.TeamIndex.OnValueChanged -= OnTeamChanged;
            _stats.IsAlive.OnValueChanged   -= OnAliveChanged;
            _stats.PlayerName.OnValueChanged-= OnNameChanged;
            _stats.Score.OnValueChanged     -= OnScoreChanged;
            _stats.IsInCaptureZone.OnValueChanged -= OnCaptureZoneChanged;
        }

        // ─── Callbacks de NetworkVariable ─────────────────────────────────────────

        private void OnHealthChanged(float old, float newVal) => RefreshHealthUI(newVal);
        private void OnTeamChanged(int old, int newVal)       => RefreshTeamUI(newVal);
        private void OnAliveChanged(bool old, bool newVal)    => RefreshAliveUI(newVal);
        private void OnNameChanged(FixedString32Bytes old, FixedString32Bytes newVal)
            => RefreshNameUI(newVal.ToString());
        private void OnScoreChanged(int old, int newVal)      => RefreshScoreUI(newVal);
        private void OnCaptureZoneChanged(bool old, bool newVal) => RefreshCaptureZoneUI(newVal);

        // ─── Actualización de UI ──────────────────────────────────────────────────

        private void RefreshHealthUI(float hp)
        {
            // Guard: solo actualiza si cambió
            if (Mathf.Approximately(hp, _lastHealth)) return;
            _lastHealth = hp;

            float normalized = hp / PlayerStats.MaxHealth;

            // HUD propio
            if (IsLocalPlayer)
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
            _lastPlayerName = playerName;
            RefreshBillboardName();
        }

        private void RefreshScoreUI(int score)
        {
            if (score == _lastScore) return;
            _lastScore = score;

            if (IsLocalPlayer && scoreText != null)
                scoreText.SetText("{0} pts", score);

            RefreshBillboardName();
        }

        private void RefreshCaptureZoneUI(bool isCapturing)
        {
            _lastCapturing = isCapturing;
        }

        private void RefreshBillboardName()
        {
            if (playerNameText != null)
            {
                if (showScoreInBillboard)
                    playerNameText.SetText($"{_lastPlayerName} | {Mathf.Max(0, _lastScore)} pts");
                else
                    playerNameText.SetText(_lastPlayerName);
            }
        }

        private void RefreshAliveUI(bool alive)
        {
            _lastAlive = alive;

            // Billboard solo visible si está vivo
            if (billboardRoot != null)
                billboardRoot.SetActive(alive);

            // Texto de respawn (solo Owner)
            if (IsLocalPlayer && respawnText != null)
            {
                respawnText.gameObject.SetActive(!alive);
                
                Transform overlay = null;
                if (respawnText.transform.parent != null && respawnText.transform.parent.name == "RespawnOverlay")
                {
                    overlay = respawnText.transform.parent;
                    overlay.gameObject.SetActive(!alive);
                }
                
                // Ocultar vida, puntaje y controles (todo excepto RespawnOverlay)
                if (ownerHUDRoot != null)
                {
                    foreach (Transform child in ownerHUDRoot.transform)
                    {
                        if (child == overlay || child == respawnText.transform) continue;
                        child.gameObject.SetActive(alive);
                    }
                }
            }
        }

        public void SetRespawnText(string text)
        {
            _respawnMessage = text;
            
            if (IsLocalPlayer && respawnText != null)
                respawnText.SetText(text);
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

    /// <summary>
    /// Flecha local sobre cada jugador que apunta hacia la zona de captura.
    /// Se oculta cuando la zona ya esta dentro del campo de vision de la camara local.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class CaptureZoneDirectionArrow : MonoBehaviour
    {
        [Header("Flecha")]
        [SerializeField] private float heightOffset = 2.45f;
        [SerializeField] private float arrowScale = 0.85f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseAmount = 0.12f;
        [SerializeField] private Color arrowColor = new Color(0.1f, 0.9f, 1f, 1f);

        private PlayerStats _stats;
        private Camera _camera;
        private Transform _arrowRoot;
        private MeshRenderer _arrowRenderer;
        private Material _arrowMaterial;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            CreateArrow();
        }

        private void OnDisable()
        {
            SetArrowVisible(false);
        }

        private void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;

            CaptureZone zone = CaptureZone.ActiveZone;
            if (_camera == null || zone == null || _stats == null || !_stats.IsAlive.Value || !ShouldShowDuringPhase())
            {
                SetArrowVisible(false);
                return;
            }

            if (IsZoneVisible(zone))
            {
                SetArrowVisible(false);
                return;
            }

            Vector3 direction = zone.Center - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.04f)
            {
                SetArrowVisible(false);
                return;
            }

            SetArrowVisible(true);

            _arrowRoot.position = transform.position + Vector3.up * heightOffset;
            _arrowRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            _arrowRoot.localScale = Vector3.one * (arrowScale * pulse);
        }

        private bool ShouldShowDuringPhase()
        {
            if (GamePhaseManager.Singleton == null) return true;
            return GamePhaseManager.Singleton.CurrentPhase.Value == GamePhase.Playing;
        }

        private bool IsZoneVisible(CaptureZone zone)
        {
            Vector3 center = zone.Center + Vector3.up * 0.4f;
            float radius = Mathf.Max(0.5f, zone.Radius);

            return IsPointVisible(center)
                || IsPointVisible(center + Vector3.forward * radius)
                || IsPointVisible(center - Vector3.forward * radius)
                || IsPointVisible(center + Vector3.right * radius)
                || IsPointVisible(center - Vector3.right * radius);
        }

        private bool IsPointVisible(Vector3 worldPoint)
        {
            Vector3 viewport = _camera.WorldToViewportPoint(worldPoint);
            return viewport.z > 0f
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
        }

        private void CreateArrow()
        {
            if (_arrowRoot != null) return;

            GameObject arrowObject = new GameObject("CaptureZoneDirectionArrow");
            arrowObject.transform.SetParent(transform, false);
            arrowObject.transform.localPosition = Vector3.up * heightOffset;

            MeshFilter filter = arrowObject.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateArrowMesh();

            _arrowRenderer = arrowObject.AddComponent<MeshRenderer>();
            _arrowMaterial = CreateArrowMaterial();
            _arrowRenderer.sharedMaterial = _arrowMaterial;

            _arrowRoot = arrowObject.transform;
            SetArrowVisible(false);
        }

        private Mesh CreateArrowMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "CaptureZoneDirectionArrowMesh";

            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0.75f),
                new Vector3(-0.38f, 0f, -0.12f),
                new Vector3(-0.14f, 0f, -0.12f),
                new Vector3(-0.14f, 0f, -0.7f),
                new Vector3(0.14f, 0f, -0.7f),
                new Vector3(0.14f, 0f, -0.12f),
                new Vector3(0.38f, 0f, -0.12f)
            };

            mesh.triangles = new[]
            {
                0, 2, 1,
                0, 5, 2,
                0, 6, 5,
                2, 4, 3,
                2, 5, 4
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateArrowMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            material.name = "CaptureZoneDirectionArrow_Runtime";
            material.color = arrowColor;
            return material;
        }

        private void SetArrowVisible(bool visible)
        {
            if (_arrowRenderer != null && _arrowRenderer.enabled != visible)
                _arrowRenderer.enabled = visible;
        }
    }
}
