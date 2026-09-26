using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

// Explicit, targeted authoring repairs; never run automatically on import.
public static class LateGameFixSetup
{
    [MenuItem("Duck Defender/Late Game Fixes/Update Dash and Explosion Assets")]
    public static void ApplyAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply repairs outside Play Mode.");
        var card = AssetDatabase.FindAssets("t:CardDefinition", new[] { "Assets/Cards/Upgrades" })
            .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .First(c => c.ID == "mob_dash");
        Undo.RecordObject(card, "Use distance based Dash");
        int index = card.Modifiers.FindIndex(m => m.StatType == StatType.DashDuration || m.StatType == StatType.DashDistance);
        if (index < 0) throw new InvalidOperationException("Dash modifier missing.");
        var modifier = card.Modifiers[index];
        modifier.StatType = StatType.DashDistance; modifier.BaseAmount = 3; modifier.AmountPerShopLevel = 1;
        card.Modifiers[index] = modifier;
        card.Description = "Dash up to {0} units in your movement direction, stopping at terrain. Cooldown: {1} seconds.";
        EditorUtility.SetDirty(card); AssetDatabase.SaveAssetIfDirty(card);

        RepairParticlePrefab("Assets/Prefabs/Effects/Explosion Effect.prefab", "Assets/Prefabs/Effects/ExplosionParticles.mat");
        RepairParticlePrefab("Assets/Prefabs/Effects/Volcano fire.prefab", "Assets/Prefabs/Effects/VolcanoParticles.mat");
        Debug.Log("[Late Game Fixes] Updated Dash distance and explosion/Volcano particle materials; existing artwork retained.");
    }

    static void RepairParticlePrefab(string prefabPath, string materialPath)
    {
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var particle = prefab.GetComponentInChildren<ParticleSystem>(true);
            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) throw new InvalidOperationException("URP particle shader missing.");
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(materialPath) };
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                var sheet = particle.textureSheetAnimation;
                if (sheet.enabled && sheet.mode == ParticleSystemAnimationMode.Sprites && sheet.spriteCount > 0 && sheet.GetSprite(0) != null)
                    material.SetTexture("_BaseMap", sheet.GetSprite(0).texture);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            particle.gameObject.SetActive(true);
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 100;
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    [MenuItem("Duck Defender/Late Game Fixes/Merge Active Scene Ground Colliders")]
    public static void RepairActiveGround() { RepairGround(SceneManager.GetActiveScene()); }
    public static int RepairGround(Scene scene)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Repair ground outside Play Mode.");
        int changed = 0;
        foreach (var tilemap in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TilemapCollider2D>(true)))
        {
            if (tilemap.gameObject.layer != LayerMask.NameToLayer("Ground")) continue;
            var composite = tilemap.GetComponent<CompositeCollider2D>();
            if (composite == null) continue;
            Undo.RecordObjects(new UnityEngine.Object[] { tilemap, composite }, "Merge ground tile collisions");
            // Unity's generated composite uses tile geometry without the source
            // collider offset. Transfer it once so the repair preserves terrain height.
            if (tilemap.offset != Vector2.zero)
            { composite.offset += tilemap.offset; tilemap.offset = Vector2.zero; }
            tilemap.compositeOperation = Collider2D.CompositeOperation.Merge;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            tilemap.ProcessTilemapChanges(); composite.GenerateGeometry();
            EditorUtility.SetDirty(tilemap); EditorUtility.SetDirty(composite); changed++;
        }
        if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Late Game Fixes] Merged " + changed + " Ground tilemap(s). Save this scene after reviewing the collision outline.");
        return changed;
    }
}
