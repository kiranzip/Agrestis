using UnityEngine;
using UnityEngine.UI;

namespace Agrestis.UI
{
    [DisallowMultipleComponent]
    public class HeartIcon : MonoBehaviour
    {
        [Tooltip("Fill image. Image Type must be Filled.")]
        [SerializeField] private Image _fill;
        [Tooltip("Background heart behind the fill.")]
        [SerializeField] private Image _background;

        [Header("Feedback")]
        [Tooltip("Scale bump when the value changes.")]
        [SerializeField] private float _punchScale = 0.18f;
        [SerializeField] private float _punchDecay = 8f;

        private float _target = 1f;
        private float _punch;

        private void Reset()
        {
            _background = GetComponent<Image>();
            if (transform.childCount > 0) _fill = transform.GetChild(0).GetComponent<Image>();
        }

        private void Awake()
        {
            if (_fill == null && transform.childCount > 0)
                _fill = transform.GetChild(0).GetComponent<Image>();

            if (_fill == null)
            {
                Debug.LogWarning($"HeartIcon on {name} has no Fill image.", this);
                return;
            }

            if (_fill.type != Image.Type.Filled)
            {
                Debug.LogWarning($"Fill image on {name} was not set to Filled.", this);
                _fill.type = Image.Type.Filled;
            }
        }

        public void SetFill(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (!Mathf.Approximately(amount, _target)) _punch = 1f;

            _target = amount;
            if (_fill != null) _fill.fillAmount = amount;
            if (_background != null) _background.enabled = true;
        }

        private void Update()
        {
            if (_punch <= 0.001f) return;

            _punch = Mathf.MoveTowards(_punch, 0f, _punchDecay * Time.unscaledDeltaTime);
            float scale = 1f + _punch * _punchScale;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
