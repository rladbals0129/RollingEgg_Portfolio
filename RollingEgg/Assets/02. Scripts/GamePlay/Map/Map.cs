using RollingEgg.Core;
using RollingEgg.Util;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RollingEgg
{
    public class Map : MonoBehaviour
    {
        [SerializeField] private SplinePath _splinePath;
        [SerializeField] private EndPoint _endPoint;

        private SortedDictionary<EPathType, EColorType> _pathColorMaps = new();

        public SplinePath SplinePath => _splinePath;
        public EndPoint EndPoint => _endPoint;
        public SortedDictionary<EPathType, EColorType> PathColorMaps => _pathColorMaps;

        public void Initialize(ColorKeyMapping colorKeyMapping)
        {
            SetupSafeZoneColors(colorKeyMapping);
        }

        public Vector3 GetEndPointPosition()
        {
            if (_endPoint == null)
            {
                Debug.LogWarning($"[Map] EndPoint가 할당 되지 않았습니다.");
                return Vector3.zero;
            }

            return _endPoint.transform.position;
        }

        private void SetupSafeZoneColors(ColorKeyMapping colorKeyMapping)
        {
            SafeZone[] safeZones = GetComponentsInChildren<SafeZone>();

            if (safeZones == null || safeZones.Length == 0)
            {
                Debug.Log("[Map] SafeZone을 찾을 수 없습니다.");
                return;
            }

            if (colorKeyMapping == null || colorKeyMapping.IsEmpty)
            {
                Debug.LogWarning("[Map] ColorKeyMapping이 null이거나 비어있습니다.");
                return;
            }

            foreach (SafeZone safeZone in safeZones)
            {
                if (safeZone == null)
                    continue;

                // SafeZone의 각 ColorKeyType에 맞는 색상 할당
                safeZone.UpdateVisualization(colorKeyMapping);
            }

            Debug.Log($"[Map] {safeZones.Length}개의 SafeZone에 색상 할당 완료");
        }
    }
}
