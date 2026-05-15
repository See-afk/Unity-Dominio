using UnityEngine;
using UnityEngine.EventSystems;

namespace KingOfTheHill.UI
{
    /// <summary>
    /// Joystick virtual flotante para Android.
    /// Se activa solo en builds de Android (compilación condicional).
    /// Reporta valores normalizados igual que el InputSystem para compatibilidad
    /// con PlayerMovement sin modificar nada en él.
    /// Optimización: usa EventSystems (IPointerDown/Drag/Up) en lugar de Update
    /// con Input.touches para no hacer polling cada frame.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Visual")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float         handleRange = 0.5f;   // 0-1 fracción del radio

        // ─── Salida ───────────────────────────────────────────────────────────────
        /// <summary>Valor normalizado [-1,1] del joystick. Leer desde PlayerMovement.</summary>
        public Vector2 Value { get; private set; }

        // ─── Privados ─────────────────────────────────────────────────────────────
        private Canvas    _canvas;
        private Vector2   _startPos;
        private float     _radius;

        // ─────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            _canvas  = GetComponentInParent<Canvas>();
            _radius  = background.sizeDelta.x * 0.5f;
        }

        // ─── IPointerDownHandler ──────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            _startPos = ScreenToLocal(eventData.position);
            MoveHandle(Vector2.zero);
        }

        // ─── IDragHandler ─────────────────────────────────────────────────────────

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 pos   = ScreenToLocal(eventData.position) - _startPos;
            Vector2 clamp = Vector2.ClampMagnitude(pos, _radius * handleRange * 2f);

            MoveHandle(clamp);

            // Normalizar a [-1, 1]
            Value = clamp / (_radius * handleRange * 2f);
        }

        // ─── IPointerUpHandler ────────────────────────────────────────────────────

        public void OnPointerUp(PointerEventData eventData)
        {
            Value = Vector2.zero;
            MoveHandle(Vector2.zero);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private void MoveHandle(Vector2 offset)
        {
            handle.anchoredPosition = offset;
        }

        private Vector2 ScreenToLocal(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, screenPos, _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _canvas.worldCamera,
                out Vector2 local);
            return local;
        }
    }
}
