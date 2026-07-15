using UnityEngine;

namespace NoREroMod.Patches.Enemy.Six_hand
{
    internal class MutudeVideoPositionTracker : MonoBehaviour
    {
        private RectTransform videoRect;
        private Vector2 offset;
        private bool tracking;
        private bool followPlayer;
        private Camera cachedCamera;

        internal void Initialize(RectTransform rect)
        {
            videoRect = rect;
            cachedCamera = Camera.main;
            tracking = false;
        }

        public void SetOffsets(Vector2 newOffset)
        {
            offset = newOffset;
        }

        public void SetTrackingTarget(bool usePlayer)
        {
            followPlayer = usePlayer;
        }

        public void EnableTracking()
        {
            tracking = true;
        }

        public void DisableTracking()
        {
            tracking = false;
        }

        private void LateUpdate()
        {
            if (!tracking || videoRect == null)
            {
                return;
            }

            Transform target = MutudeEffects.GetTrackingTarget(followPlayer);
            if (target == null)
            {
                return;
            }

            var cam = Camera.main ?? cachedCamera;
            if (cam == null)
            {
                return;
            }

            cachedCamera = cam;

            Vector3 screenPos = cam.WorldToScreenPoint(target.position);
            if (screenPos.z < 0f)
            {
                return;
            }

            RectTransform parentRect = videoRect.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, null, out localPoint);
            videoRect.anchoredPosition = localPoint + offset;
        }
    }
}

