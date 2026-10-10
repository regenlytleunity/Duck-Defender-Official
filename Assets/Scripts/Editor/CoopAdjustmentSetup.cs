using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class CoopAdjustmentSetup
{
    [MenuItem("Duck Defender/Co-op/Apply update 3 adjustments")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save open scene changes first.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DuckDefenderTestFontv2.asset");
            if (font == null) throw new System.InvalidOperationException("DuckDefenderTestFontv2 font asset is missing.");
            CoopUIElements.SetFont(font);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var duck = Object.FindFirstObjectByType<PlayerController>().GetComponent<SpriteRenderer>().sprite;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var host = Object.FindFirstObjectByType<HostMenuUI>(); host.DuckPreviewSprite = duck; EditorUtility.SetDirty(host);
            var settings = Object.FindFirstObjectByType<SettingsMenuUI>(FindObjectsInactive.Include);
            // Four vertically stacked controls in the existing volume column.
            var sliders = new[] { settings.MasterSlider, settings.SfxSlider, settings.MusicSlider };
            for (int i = 0; i < sliders.Length; i++)
                CoopUIElements.Stretch((RectTransform)sliders[i].transform.parent, new Vector2(.13f, .67f - .135f * i), new Vector2(.54f, .80f - .135f * i));
            CoopUIElements.Stretch((RectTransform)settings.CameraZoomSlider.transform, new Vector2(.142f, .285f), new Vector2(.47f, .325f));
            CoopUIElements.Stretch(settings.CameraZoomText.rectTransform, new Vector2(.13f, .325f), new Vector2(.54f, .385f));
            var backing = settings.transform.Find("Camera Zoom Label Backing");
            if (backing != null) CoopUIElements.Stretch((RectTransform)backing, new Vector2(.13f, .325f), new Vector2(.54f, .385f));
            var menu = Object.FindFirstObjectByType<MainMenuUI>();
            menu.UIFont = font; EditorUtility.SetDirty(menu);
            var cancel = menu.ConfirmPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b != menu.YesButton && b != menu.BuyThreeButton);
            cancel.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(cancel.onClick, menu.CancelPurchase);
            // Existing new UI must use the same custom face as future runtime labels.
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { text.font = font; text.fontSharedMaterial = font.material; text.color = Color.white; EditorUtility.SetDirty(text); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var ui = Object.FindFirstObjectByType<GameUI>(); ui.UIFont = font; EditorUtility.SetDirty(ui);
            var world = Object.FindFirstObjectByType<WorldCamera>();
            if (world.SpawnPadding < 30)
            {
                foreach (var tiles in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                {
                    tiles.CompressBounds(); var bounds = tiles.cellBounds;
                    foreach (var p in bounds.allPositionsWithin)
                    {
                        var tile = tiles.GetTile(p); if (tile == null) continue;
                        int side = p.x < bounds.xMin + 38 ? -1 : p.x >= bounds.xMax - 38 ? 1 : 0;
                        if (side == 0) continue;
                        var target = p + Vector3Int.right * (38 * side);
                        tiles.SetTile(target, tile); tiles.SetTileFlags(target, TileFlags.None);
                        tiles.SetTransformMatrix(target, tiles.GetTransformMatrix(p)); tiles.SetColor(target, tiles.GetColor(p)); tiles.SetTileFlags(target, tiles.GetTileFlags(p));
                    }
                    tiles.CompressBounds(); EditorUtility.SetDirty(tiles);
                }
                foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("GameBackground") && (r.name.EndsWith(" West") || r.name.EndsWith(" East"))).ToArray())
                {
                    var copy = Object.Instantiate(renderer.gameObject, renderer.transform.parent); copy.name = renderer.name + " Spawn reserve";
                    copy.transform.position += Vector3.right * (renderer.name.EndsWith(" West") ? -38.4f : 38.4f);
                }
                world.SpawnPadding = 30;
            }
            // Both the visible walls and the anti-falloff blockers must admit enemies.
            world.PlayerWalls = Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)
                .Where(b => (b.name.Contains("Border") || b.name.StartsWith("AntiFalloff")) && Mathf.Abs(b.transform.position.x) > 40).Cast<Collider2D>().ToArray();
            foreach (var collider in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None))
            {
                collider.GetComponent<Tilemap>().RefreshAllTiles();
                collider.ProcessTilemapChanges();
                // Rebuilding also invalidates serialized composite geometry from before expansion.
                collider.enabled = false; collider.enabled = true;
                var composite = collider.GetComponent<CompositeCollider2D>();
                if (composite != null) { composite.GenerateGeometry(); EditorUtility.SetDirty(composite); }
                EditorUtility.SetDirty(collider);
            }
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { text.font = font; text.fontSharedMaterial = font.material; text.color = Color.white; EditorUtility.SetDirty(text); }
            EditorUtility.SetDirty(world); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

}
