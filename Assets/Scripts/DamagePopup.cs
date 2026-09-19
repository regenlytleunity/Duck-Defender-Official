using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshProUGUI _textMesh;
    private float _disappearTimer;
    private Color _textColor;
    private Vector3 _moveVector;

    private const float DISAPPEAR_TIMER_MAX = 0.8f;

    void Awake()
    {
        _textMesh = GetComponent<TextMeshProUGUI>();
    }

    public void Setup(int damageAmount, bool isCrit)
    {
        _textMesh.text = damageAmount.ToString();
        
        if (isCrit)
        {
            _textMesh.fontSize = 36; // UI fonts use larger point sizes
            _textMesh.color = Color.red; 
            _textMesh.text += "!";
        }
        else
        {
            _textMesh.fontSize = 24;
            _textMesh.color = Color.yellow; 
        }

        _textColor = _textMesh.color;
        _disappearTimer = DISAPPEAR_TIMER_MAX;

        // Move Upwards (Pixels per second)
        _moveVector = new Vector3(Random.Range(-10f, 10f), 50f); 
    }

    void Update()
    {
        // Move relative to screen
        transform.position += _moveVector * Time.deltaTime;
        _moveVector -= _moveVector * 2f * Time.deltaTime; 

        _disappearTimer -= Time.deltaTime;
        if (_disappearTimer < 0)
        {
            float disappearSpeed = 3f;
            _textColor.a -= disappearSpeed * Time.deltaTime;
            _textMesh.color = _textColor;

            if (_textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}