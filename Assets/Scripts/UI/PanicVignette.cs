using UnityEngine;
using UnityEngine.UI;

namespace TiffanyGame
{
    [RequireComponent(typeof(Image))]
    public sealed class PanicVignette : MonoBehaviour
    {
        public PanicController panic;
        Image overlay;
        RectTransform rectangle;
        PanicVignetteModel model;

        void Awake()
        {
            overlay = GetComponent<Image>();
            rectangle = overlay.rectTransform;
        }

        void LateUpdate()
        {
            model.Advance(panic != null && panic.isActiveAndEnabled ? panic.Level : 0f, Time.deltaTime);
            overlay.color = new Color(0f, 0f, 0f, model.Alpha);
            rectangle.localScale = Vector3.one * model.Scale;
        }
    }
}
