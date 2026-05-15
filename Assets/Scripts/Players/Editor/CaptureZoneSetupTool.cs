using System.IO;
using KingOfTheHill.Gameplay;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KingOfTheHill.Editor
{
    public static class CaptureZoneSetupTool
    {
        private const string MaterialPath = "Assets/Materials/Neon/CaptureZone_Neon.mat";

        [MenuItem("KingOfTheHill/6 - Crear Zona de Captura", priority = 6)]
        public static void CreateCaptureZone()
        {
            if (Object.FindAnyObjectByType<CaptureZone>() != null)
            {
                Debug.LogWarning("[CaptureZoneSetupTool] Ya existe una zona de captura en la escena.");
                return;
            }

            GameObject root = new GameObject("CaptureZone");
            root.transform.position = new Vector3(0f, 0.05f, 0f);
            root.AddComponent<NetworkObject>();

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up;
            trigger.radius = 4f;

            CaptureZone captureZone = root.AddComponent<CaptureZone>();

            GameObject areaCenter = new GameObject("CaptureZoneAreaCenter");
            areaCenter.transform.position = Vector3.zero;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Visual_Disc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            disc.transform.localScale = new Vector3(8f, 0.02f, 8f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial();

            GameObject lightObject = new GameObject("CaptureZone_SpotLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 7f, 0f);
            lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Light spotLight = lightObject.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.color = new Color(0.1f, 0.85f, 1f);
            spotLight.intensity = 5f;
            spotLight.range = 11f;
            spotLight.spotAngle = 58f;
            spotLight.shadows = LightShadows.Soft;

            SerializedObject serializedZone = new SerializedObject(captureZone);
            serializedZone.FindProperty("radius").floatValue = 4f;
            serializedZone.FindProperty("pointsPerSecond").floatValue = 3f;
            serializedZone.FindProperty("relocationInterval").floatValue = 30f;
            serializedZone.FindProperty("areaCenter").objectReferenceValue = areaCenter.transform;
            serializedZone.FindProperty("areaSize").vector2Value = new Vector2(28f, 28f);
            serializedZone.FindProperty("visualRoot").objectReferenceValue = disc.transform;
            serializedZone.FindProperty("zoneLight").objectReferenceValue = spotLight;
            serializedZone.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);

            Debug.Log("[CaptureZoneSetupTool] Zona de captura creada. Ajusta areaSize o fixedSpawnPoints si tu mapa es mas grande.");
        }

        private static Material GetOrCreateMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
            material.name = "CaptureZone_Neon";
            material.color = new Color(0.08f, 0.9f, 1f, 0.55f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.08f, 0.9f, 1f) * 2.5f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
