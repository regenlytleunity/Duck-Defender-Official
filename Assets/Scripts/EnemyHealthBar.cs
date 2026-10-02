using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyHealthBar : MonoBehaviour
{
    public Slider HealthSlider;
    public Vector3 Offset = new Vector3(0, 1.2f, 0); // Height above enemy head
    public TextMeshProUGUI HealthText;
    public Slider ShieldSlider;
    public TextMeshProUGUI ShieldText;
    public Vector2 BarSizeMultiplier = new Vector2(1.35f, 1.8f);

    private Transform _target;

    public void Initialize(Transform target, float maxHealth)
    {
        _target = target;
        if (HealthSlider == null) return;
        var rect = HealthSlider.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.Scale(rect.sizeDelta, BarSizeMultiplier);
        HealthSlider.interactable = false;
        if (HealthText == null) HealthText = CreateLabel(HealthSlider);
        HealthSlider.maxValue = maxHealth;
        UpdateHealth(maxHealth);
    }

    public void UpdateHealth(float currentHealth)
    {
        if (HealthSlider == null) return;
        HealthSlider.value = currentHealth;
        if (HealthText != null) HealthText.text = Format(currentHealth) + "/" + Format(HealthSlider.maxValue);
    }

    public void UpdateShield(float current, float max)
    {
        if (ShieldSlider == null && current > 0 && HealthSlider != null)
        {
            ShieldSlider = Instantiate(HealthSlider, HealthSlider.transform.parent);
            ShieldSlider.name = "Shield";
            var rect = ShieldSlider.GetComponent<RectTransform>();
            rect.anchoredPosition += Vector2.up * (Mathf.Abs(rect.rect.height) + 3) * Mathf.Abs(rect.localScale.y);
            if (ShieldSlider.fillRect != null)
            {
                var fill = ShieldSlider.fillRect.GetComponent<UnityEngine.UI.Image>();
                if (fill != null) fill.color = new Color(.2f, .65f, 1);
            }
            ShieldText = ShieldSlider.GetComponentInChildren<TextMeshProUGUI>();
            if (ShieldText == null) ShieldText = CreateLabel(ShieldSlider);
        }
        if (ShieldSlider == null) return;
        ShieldSlider.gameObject.SetActive(current > 0);
        ShieldSlider.maxValue = Mathf.Max(1, max); ShieldSlider.value = current;
        if (ShieldText != null) ShieldText.text = Format(current) + "/" + Format(max);
    }

    static string Format(float value) => Mathf.Max(0, value).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    static TextMeshProUGUI CreateLabel(Slider slider)
    {
        var go = new GameObject("Health Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(slider.transform, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = Mathf.Max(1, slider.GetComponent<RectTransform>().rect.height * .45f);
        text.enableAutoSizing = true; text.fontSizeMax = text.fontSize; text.fontSizeMin = text.fontSize * .5f;
        text.color = Color.white; text.fontStyle = FontStyles.Bold; text.raycastTarget = false;
        return text;
    }

    void LateUpdate()
    {
        if (_target == null)
        {
            Destroy(gameObject); // If enemy dies, destroy bar
            return;
        }

        // 1. Follow Position
        transform.position = _target.position + Offset;

        // 2. Prevent Rotation (Billboarding)
        // Even if the enemy flips left (Scale X -1), we want the bar to stay normal
        transform.rotation = Quaternion.identity;
    }
}
