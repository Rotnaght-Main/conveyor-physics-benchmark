using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LoopSortTest.Editor
{
    public static class LoopSortSceneSetup
    {
        [MenuItem("LoopSort/Setup Scene", false, 0)]
        public static void SetupScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cameraGo.transform.position = new Vector3(0f, 12f, -8f);
            cameraGo.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            // URP Camera
            var urpCamType = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (urpCamType != null)
                cameraGo.AddComponent(urpCamType);

            // Directional Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.5f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // URP Light
            var urpLightType = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalLightData");
            if (urpLightType != null)
                lightGo.AddComponent(urpLightType);

            // SceneContext (Zenject)
            var sceneContextType = FindType("Zenject.SceneContext");
            if (sceneContextType == null)
            {
                EditorUtility.DisplayDialog("LoopSort",
                    "Zenject.SceneContext bulunamadi!\n\n" +
                    "Zenject paketinin yuklu ve compile olmus oldugundan emin olun.",
                    "OK");
                return;
            }

            var sceneContextGo = new GameObject("SceneContext");
            var sceneContext = sceneContextGo.AddComponent(sceneContextType);

            // Installer
            var installerType = FindType("LoopSortTest.Installers.ConveyorSystemInstaller");
            if (installerType == null)
            {
                EditorUtility.DisplayDialog("LoopSort",
                    "ConveyorSystemInstaller bulunamadi!\n\n" +
                    "Scriptlerin compile olmasini bekleyin ve tekrar deneyin.",
                    "OK");
                return;
            }

            var installer = sceneContextGo.AddComponent(installerType);

            // ConveyorConfig
            var config = FindOrCreateConfig();
            if (config != null)
            {
                var configField = installerType.GetField("_config",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (configField != null)
                    configField.SetValue(installer, config);
            }

            // Installer'i SceneContext._monoInstallers listesine ekle
            var monoInstallerBaseType = FindType("Zenject.MonoInstaller");
            if (monoInstallerBaseType != null)
            {
                // SceneContext uzerindeki _monoInstallers field'ini bul
                var monoInstField = FindFieldInHierarchy(sceneContextType, "_monoInstallers");
                if (monoInstField != null)
                {
                    var listType = typeof(List<>).MakeGenericType(monoInstallerBaseType);
                    var list = Activator.CreateInstance(listType);
                    listType.GetMethod("Add").Invoke(list, new object[] { installer });
                    monoInstField.SetValue(sceneContext, list);
                }
                else
                {
                    Debug.LogWarning("[LoopSort] _monoInstallers field bulunamadi. " +
                        "Inspector'dan SceneContext > Mono Installers listesine installer'i elle ekleyin.");
                }
            }

            // UI GameObject
            var uiGo = new GameObject("ConveyorUI");

            var uiType = FindType("LoopSortTest.UI.AlgorithmSwitcherUI");
            if (uiType != null) uiGo.AddComponent(uiType);

            var perfType = FindType("LoopSortTest.UI.PerformanceStatsUI");
            if (perfType != null) uiGo.AddComponent(perfType);

            var gizmoType = FindType("LoopSortTest.UI.ConveyorGizmoDrawer");
            if (gizmoType != null) uiGo.AddComponent(gizmoType);

            // Save scene
            string scenePath = "Assets/Scenes/LoopSortConveyor.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("[LoopSort] Scene basariyla olusturuldu: " + scenePath);
            EditorUtility.DisplayDialog("LoopSort",
                "Scene basariyla olusturuldu!\n\n" +
                "Konum: " + scenePath + "\n\n" +
                "Play tusuna basarak test edebilirsiniz.\n" +
                "1-5 tuslari ile algoritma degistirin.",
                "Tamam");
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string fieldName)
        {
            var current = type;
            while (current != null)
            {
                var field = current.GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field != null) return field;
                current = current.BaseType;
            }
            return null;
        }

        private static ScriptableObject FindOrCreateConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:ConveyorConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            }

            var configType = FindType("LoopSortTest.Config.ConveyorConfig");
            if (configType == null)
            {
                Debug.LogError("[LoopSort] ConveyorConfig tipi bulunamadi!");
                return null;
            }

            string dir = "Assets/Scripts/LoopSortTest/Config";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var config = ScriptableObject.CreateInstance(configType);
            string assetPath = dir + "/ConveyorConfig.asset";
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LoopSort] ConveyorConfig olusturuldu: " + assetPath);
            return config;
        }
    }
}
