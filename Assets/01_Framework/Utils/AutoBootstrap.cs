using StarPixelSurge.Effect;
using StarPixelSurge.GameCore;
using StarPixelSurge.Map;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 原型自动启动器。进入 Play 时若场景中不存在 GameBootstrap，则自动创建相机与启动对象，
    /// 使任意空场景都能直接运行 V1.0 单机原型。正式版本可在场景中手动放置 GameBootstrap 后关闭本开关。
    /// </summary>
    public static class AutoBootstrap
    {
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Enabled)
            {
                return;
            }

            if (Object.FindObjectOfType<GameBootstrap>() != null)
            {
                EnsureCamera();
                EnsureEffectSystems();
                return;
            }

            EnsureCamera();
            EnsureEffectSystems();

            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                if (Camera.main.GetComponent<CameraFollow>() == null)
                {
                    Camera.main.gameObject.AddComponent<CameraFollow>();
                }

                if (Camera.main.GetComponent<CameraFeedback>() == null)
                {
                    Camera.main.gameObject.AddComponent<CameraFeedback>();
                }

                return;
            }

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";

            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.03f, 0.08f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<CameraFollow>();
            cameraGo.AddComponent<CameraFeedback>();
        }

        /// <summary>装配特效反馈系统：分级控制器与打击反馈调度器。</summary>
        private static void EnsureEffectSystems()
        {
            if (!GraphicsTierController.HasInstance)
            {
                var go = new GameObject("EffectSystems");
                go.AddComponent<GraphicsTierController>();
                go.AddComponent<HitFeedback>();
            }
        }
    }
}
