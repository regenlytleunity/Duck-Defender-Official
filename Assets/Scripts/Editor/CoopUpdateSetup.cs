using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class CoopUpdateSetup
{
    [MenuItem("Duck Defender/Co-op/Apply world and menu setup")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save your open scene changes before applying setup.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var camera = Object.FindFirstObjectByType<Camera>();
            var world = camera.GetComponent<WorldCamera>();
            if (world == null)
            {
                var borders = Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);
                var left = borders.Single(b => b.name == "Map Border Left");
                var right = borders.Single(b => b.name == "Map Border Right");
                float oldLeft = left.bounds.max.x, oldRight = right.bounds.min.x;
                float width = oldRight - oldLeft;
                if (width <= 0) throw new System.InvalidOperationException("Invalid authored map boundaries.");
                foreach (var tiles in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                {
                    tiles.CompressBounds(); var bounds = tiles.cellBounds;
                    var positions = bounds.allPositionsWithin;
                    // Repeat the existing 38-cell terrain block on either side; preserve all original cells.
                    foreach (var p in positions)
                    {
                        var tile = tiles.GetTile(p); if (tile == null) continue;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            var target = p + Vector3Int.right * (38 * side);
                            tiles.SetTile(target, tile); tiles.SetTileFlags(target, TileFlags.None);
                            tiles.SetTransformMatrix(target, tiles.GetTransformMatrix(p)); tiles.SetColor(target, tiles.GetColor(p));
                            tiles.SetTileFlags(target, tiles.GetTileFlags(p));
                        }
                    }
                    tiles.CompressBounds(); EditorUtility.SetDirty(tiles);
                }
                foreach (var border in borders)
                {
                    if (!border.name.Contains("Border") && !border.name.StartsWith("AntiFalloff")) continue;
                    var scale = border.transform.localScale;
                    if (Mathf.Abs(border.transform.position.x) > 10)
                        border.transform.position += Vector3.right * (Mathf.Sign(border.transform.position.x) * width);
                    else { scale.x += 2 * width; border.transform.localScale = scale; }
                }
                var backgrounds = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(s => s.name.StartsWith("GameBackground")).ToArray();
                foreach (var background in backgrounds) for (int side = -1; side <= 1; side += 2)
                {
                    var copy = Object.Instantiate(background.gameObject, background.transform.parent);
                    copy.name = background.name + (side < 0 ? " West" : " East");
                    copy.transform.position += Vector3.right * (38.4f * side);
                }
                world = camera.gameObject.AddComponent<WorldCamera>(); world.Left = oldLeft - width; world.Right = oldRight + width;
                Debug.Log("[Coop setup] Playable width " + width + " -> " + (world.Right - world.Left) + "; ground 38 -> 114 cells.");
            }
            var waves = Object.FindFirstObjectByType<WaveManager>();
            if (waves.GetComponent<LocalCoopSession>() == null) waves.gameObject.AddComponent<LocalCoopSession>();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var menu = Object.FindFirstObjectByType<MainMenuUI>();
            var host = menu.GetComponent<HostMenuUI>() ?? menu.gameObject.AddComponent<HostMenuUI>();
            host.HostButton = menu.MenuPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "HostButton");
            host.HostButton.interactable = true; host.HostButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            var settings = Object.FindFirstObjectByType<SettingsMenuUI>(FindObjectsInactive.Include);
            if (settings.CameraZoomSlider == null)
            {
                var zoom = Object.Instantiate(settings.MusicSlider, settings.transform);
                zoom.name = "Camera Zoom"; zoom.onValueChanged = new UnityEngine.UI.Slider.SliderEvent();
                foreach (var label in zoom.GetComponentsInChildren<TMPro.TMP_Text>(true)) label.gameObject.SetActive(false);
                settings.CameraZoomSlider = zoom; zoom.minValue = 0; zoom.maxValue = 1; zoom.wholeNumbers = false;
                settings.CameraZoomText = (TMPro.TextMeshProUGUI)CoopUIElements.Text(settings.transform, "Solo camera zoom", new Vector2(.57f, .31f), new Vector2(.95f, .34f), 24);
            }
            CoopUIElements.Stretch(settings.CameraZoomSlider.GetComponent<RectTransform>(), new Vector2(.57f, .265f), new Vector2(.95f, .298f));
            CoopUIElements.Stretch(settings.CameraZoomText.rectTransform, new Vector2(.57f, .31f), new Vector2(.95f, .34f));
            var zoomLabelBacking = settings.transform.Find("Camera Zoom Label Backing");
            if (zoomLabelBacking == null)
                zoomLabelBacking = CoopUIElements.Panel(settings.transform, "Camera Zoom Label Backing", new Vector2(.64f, .307f), new Vector2(.88f, .343f), new Color(.1f, .15f, .2f, .9f));
            zoomLabelBacking.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            zoomLabelBacking.SetSiblingIndex(settings.CameraZoomText.transform.GetSiblingIndex());
            settings.CameraZoomText.transform.SetSiblingIndex(zoomLabelBacking.GetSiblingIndex() + 1);
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
}
