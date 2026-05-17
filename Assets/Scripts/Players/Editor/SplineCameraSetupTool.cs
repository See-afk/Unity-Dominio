#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
// Cinemachine 3 y Splines
using Unity.Cinemachine;
using UnityEngine.Splines;
#endif

namespace Dominio.Editor
{
    public static class SplineCameraSetupTool
    {
        [MenuItem("Tools/Dominio/5 - Setup Spline Camera en Escena")]
        public static void SetupSplineCamera()
        {
#if !UNITY_6000_0_OR_NEWER
            Debug.LogError("Esta herramienta está diseñada para Unity 6 y Cinemachine 3.");
            return;
#else
            // 1. Crear el Spline Container
            GameObject splineGO = new GameObject("RutaCamara_Spline");
            SplineContainer splineContainer = splineGO.AddComponent<SplineContainer>();
            
            // Añadir algunos nudos básicos al spline para que no esté vacío
            Spline spline = splineContainer.Spline;
            spline.Add(new BezierKnot(new Unity.Mathematics.float3(0, 5, -10)));
            spline.Add(new BezierKnot(new Unity.Mathematics.float3(10, 5, 0)));
            spline.Add(new BezierKnot(new Unity.Mathematics.float3(0, 5, 10)));
            spline.Add(new BezierKnot(new Unity.Mathematics.float3(-10, 5, 0)));
            spline.SetTangentMode(TangentMode.AutoSmooth);
            spline.Closed = true;

            // 2. Crear la Cámara Cinemachine
            GameObject camGO = new GameObject("CM_SplineCamera");
            CinemachineCamera cmCamera = camGO.AddComponent<CinemachineCamera>();

            // 3. Configurar Position Control -> Spline Dolly
            var splineDolly = camGO.AddComponent<CinemachineSplineDolly>();
            splineDolly.Spline = splineContainer;
            splineDolly.CameraPosition = 0;
            
            // Configurar Auto Dolly para que persiga automáticamente al Target
            var autoDolly = splineDolly.AutomaticDolly;
            autoDolly.Enabled = true;
            splineDolly.AutomaticDolly = autoDolly;

            // 4. Configurar Rotation Control -> Look At
            var lookAt = camGO.AddComponent<CinemachineHardLookAt>(); // Podemos usar HardLookAt o RotationComposer, Composer es mejor
            Object.DestroyImmediate(lookAt); // Mejor usemos Rotation Composer para que sea suave
            var rotationComposer = camGO.AddComponent<CinemachineRotationComposer>();
            rotationComposer.Damping = new Vector2(0.5f, 0.5f);

            // Seleccionar en el editor para que el usuario pueda empezar a editar
            Selection.activeGameObject = splineGO;
            EditorGUIUtility.PingObject(splineGO);

            Debug.Log("[SplineCameraSetup] Ruta Spline y Cámara Cinemachine creadas con éxito.");
            EditorUtility.DisplayDialog("Éxito", "Spline y Cinemachine Camera creados en la escena actual.\n\nAbre tu Prefab del Jugador y añádele el script 'SplineCameraAssigner' para que la cámara lo empiece a seguir al iniciar la partida.", "OK");
#endif
        }
    }
}
#endif
