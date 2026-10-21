using System;
using RoR2;
using UnityEngine;

namespace DuelistMod
{
    /// <summary>
    /// Fixed tabletop view of the duel board, handed to the player's CameraRigController as an
    /// override camera. Hides the normal HUD while active. Offsets come from config so they can be
    /// tuned live with F8.
    /// </summary>
    public class DuelCamera : ICameraStateProvider
    {
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public float Fov = 60f;

        private CameraRigController _rig;

        public Camera SceneCamera => _rig && _rig.sceneCam ? _rig.sceneCam : Camera.main;

        public void Aim(Transform board)
        {
            Vector3 eye = board.TransformPoint(new Vector3(0f, DuelConfig.CameraHeight.Value, -DuelConfig.CameraBack.Value));
            Vector3 look = board.TransformPoint(new Vector3(0f, 0f, DuelConfig.CameraLookAt.Value));
            Position = eye;
            Rotation = Quaternion.LookRotation(look - eye, Vector3.up);
            Fov = DuelConfig.CameraFov.Value;
        }

        public void Attach(CharacterBody body)
        {
            _rig = null;
            foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
            {
                if (rig && rig.targetBody == body) { _rig = rig; break; }
            }
            if (!_rig) { DuelistPlugin.Log.LogWarning("No camera rig found for the Duelist; keeping the normal camera."); return; }
            if (!DuelConfig.TabletopCamera.Value) return;
            try { _rig.SetOverrideCam(this, 0.6f); }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("SetOverrideCam failed: " + e.Message); }
        }

        public void Detach()
        {
            if (!_rig) return;
            try { if (DuelConfig.TabletopCamera.Value) _rig.SetOverrideCam(null, 0.6f); }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("Clearing override cam failed: " + e.Message); }
            _rig = null;
        }

        // ICameraStateProvider
        public void GetCameraState(CameraRigController cameraRigController, ref CameraState cameraState)
        {
            cameraState.position = Position;
            cameraState.rotation = Rotation;
            cameraState.fov = Fov;
        }

        public bool IsUserLookAllowed(CameraRigController cameraRigController) => false;
        public bool IsUserControlAllowed(CameraRigController cameraRigController) => false;
        public bool IsHudAllowed(CameraRigController cameraRigController) => false;
    }
}
