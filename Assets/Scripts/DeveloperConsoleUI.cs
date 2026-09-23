using UnityEngine;
using TMPro;

public class DeveloperConsoleUI : MonoBehaviour
{
    public TMP_InputField CodeInput;
    public UnityEngine.UI.Button SubmitButton;
    public TMP_Text ResultText;
    [Tooltip("Enable for private playtest builds. The console is available in the Editor and Development builds regardless.")]
    public bool AllowInReleaseBuilds;
    void Start()
    {
        if (!Application.isEditor && !Debug.isDebugBuild && !AllowInReleaseBuilds) { gameObject.SetActive(false); return; }
        if (CodeInput != null)
        {
            CodeInput.characterLimit = 4;
            CodeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            CodeInput.onSubmit.AddListener(_ => Submit());
        }
        if (SubmitButton != null) SubmitButton.onClick.AddListener(Submit);
    }
    public void Submit()
    {
        string code = CodeInput != null ? CodeInput.text.Trim() : "";
        bool success = code.Length == 4 && ShopManager.Instance != null && ShopManager.Instance.ExecuteDeveloperCode(code);
        if (ResultText != null) ResultText.text = success ? "Code applied. Start a new run to use the updated collection." : "Unknown code.";
        if (success && CodeInput != null) CodeInput.text = "";
    }
}
