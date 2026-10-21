using UnityEngine;

// A slow chromatic sheen on card backgrounds. Glyph faces retain their authored white.
public class AscendedCardTint : MonoBehaviour
{
    UnityEngine.UI.Image _image;
    Color _baseColor;
    public static void Apply(UnityEngine.UI.Image image, bool ascended)
    {
        if (image == null) return;
        var tint = image.GetComponent<AscendedCardTint>();
        if (tint == null && ascended) tint = image.gameObject.AddComponent<AscendedCardTint>();
        if (tint == null) return;
        tint._image = image; tint._baseColor = image.color; tint.enabled = ascended;
    }
    void Update()
    {
        if (_image == null) return;
        Color sheen = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime / 12, 1), .38f, .92f);
        Color color = Color.Lerp(_baseColor, sheen, .55f); color.a = _baseColor.a;
        _image.color = color;
    }
}
