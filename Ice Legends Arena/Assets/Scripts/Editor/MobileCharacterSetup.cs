using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Imports a character produced by the Blender mobile pipeline (atlas + merged mesh) and wires it up
/// end to end: texture settings, Humanoid rig, the single atlas material, and a ready-to-play prefab.
///
/// The Blender side outputs two files per character into the same folder:
///   &lt;Name&gt;_Mobile.fbx   one merged skinned mesh, one material slot ("&lt;Name&gt;_Mobile")
///   &lt;Name&gt;_Atlas.png    every base-colour texture packed into one 2048 atlas
///
/// Select the FBX in the Project window and run the menu item. Safe to re-run.
///
/// This exists as a tool rather than a click-list because it has to run once per character (8 of them)
/// and because three of the steps are silent traps:
///   - Textures import as <b>Sprite</b> in this project, and a Sprite will not bind to Base Map. The
///     character comes out white and nothing warns you.
///   - The prefab must be a <b>Variant</b> of the model. A flat copy is stranded the next time the
///     mesh is re-exported, which this pipeline does constantly.
///   - The body is rigged barefoot (soles at z=0) with the skate boots hanging below it, so at
///     localPosition Y=0 the blades sink into the ice. See SkateLift.
/// </summary>
public static class MobileCharacterSetup
{
    private const string ControllerPath = "Assets/Animation/HockeyPlayerAnimator.controller";
    private const string PrefabFolder = "Assets/Prefabs/Player";

    /// <summary>
    /// Blade depth. The body is rigged barefoot with the soles at the origin and the boots hang
    /// beneath, so the character must be lifted or the blades cut into the ice. Measured on Blaze at
    /// 6.94cm. X and Z stay at 0 -- those are what gameplay measures for puck distance and check range.
    /// Blade depth varies per skate model, so re-measure per character rather than assuming 0.07.
    /// </summary>
    private const float SkateLift = 0.07f;

    private const int AtlasMaxSize = 2048;

    [MenuItem("Ice Legends/Setup Mobile Character")]
    private static void Setup()
    {
        string fbxPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(fbxPath) || !fbxPath.EndsWith(".fbx"))
        {
            EditorUtility.DisplayDialog("Setup Mobile Character",
                "Select the <Name>_Mobile.fbx in the Project window first.", "OK");
            return;
        }

        string folder = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
        string modelName = Path.GetFileNameWithoutExtension(fbxPath);      // e.g. "Blaze_Mobile"
        string character = modelName.Replace("_Mobile", "");               // e.g. "Blaze"
        string atlasPath = $"{folder}/{character}_Atlas.png";

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath) == null)
        {
            EditorUtility.DisplayDialog("Setup Mobile Character",
                $"Expected the atlas next to the model:\n{atlasPath}", "OK");
            return;
        }

        ConfigureAtlas(atlasPath);
        Material mat = CreateAtlasMaterial(folder, character, atlasPath);
        ConfigureModel(fbxPath, modelName, mat);
        GameObject prefab = BuildPrefab(fbxPath, character);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[MobileCharacterSetup] {character}: atlas + Humanoid rig + 1 material + prefab ready.\n" +
                  $"Drag {prefab.name} under a player, reset X/Z to 0, and play.");
    }

    /// <summary>Textures default to Sprite in this project, which will NOT bind to Base Map.</summary>
    private static void ConfigureAtlas(string atlasPath)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = true;
        ti.maxTextureSize = AtlasMaxSize;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.SaveAndReimport();
    }

    private static Material CreateAtlasMaterial(string folder, string character, string atlasPath)
    {
        string matFolder = $"{folder}/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder(folder, "Materials");

        string matPath = $"{matFolder}/{character}_Mobile.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        mat.SetTexture("_BaseMap", atlas);
        mat.SetFloat("_Smoothness", 0.1f);   // stylised character, not a wet plastic mannequin
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// <summary>
    /// Humanoid on the same 65-bone Mixamo skeleton the other skaters use, so the character drives
    /// HockeyPlayerAnimator with no retargeting work. The FBX's single material slot is remapped onto
    /// our atlas material rather than letting Unity auto-create an empty white one.
    /// </summary>
    private static void ConfigureModel(string fbxPath, string modelName, Material mat)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;                 // clips come from the shared animator, not the model
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.External;
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), modelName), mat);
        mi.SaveAndReimport();
    }

    /// <summary>
    /// A Prefab Variant of the model -- NOT a flat copy -- so re-exporting the FBX propagates into the
    /// prefab instead of stranding it.
    /// </summary>
    private static GameObject BuildPrefab(string fbxPath, string character)
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Player");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);

        var animator = instance.GetComponent<Animator>();
        if (animator != null)
        {
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            // Imported Humanoid rigs arrive with Apply Root Motion ON. The skaters are physics-driven:
            // the controller moves the Rigidbody, and the clips are authored to animate in place. Left
            // on, the Animator ALSO translates this transform, so the model slides away from the body
            // it belongs to and the character appears to rocket across the ice.
            //
            // The Test*Controllers do clear this in Start(), but only on the single Animator they hold a
            // reference to -- so a second model in the hierarchy (or a stale inspector reference) keeps
            // root motion and drifts. Bake it off here so the prefab is correct on its own.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        instance.transform.localPosition = new Vector3(0f, SkateLift, 0f);

        string prefabPath = $"{PrefabFolder}/{character}_Mobile.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        return prefab;
    }
}
