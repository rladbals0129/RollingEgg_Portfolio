using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg
{
    [System.Serializable]
    public class ParallaxLayer
    {
        public Transform layer;

        [Range(0f, 1f)]
        public float parallaxSpeed = 0.5f;

        private Transform _cameraTransform;
        private float _startPosX;
        private float _tileWidth;


        public void SetupParallaxLayer(Transform cameraTransform)
        {
            if (layer == null)
                return;

            _cameraTransform = cameraTransform;
            _startPosX = layer.position.x;

            SpriteRenderer spriteRenderer = layer.GetComponent<SpriteRenderer>();
            _tileWidth = spriteRenderer != null ? spriteRenderer.bounds.size.x : 0f;
        }

        public void ParallaxMove()
        {
            if (_cameraTransform == null)
                return;

            Vector3 cameraPos = _cameraTransform.position;
            float tempX = cameraPos.x * (1 - parallaxSpeed);
            float distX = cameraPos.x * parallaxSpeed;
            float newPosX = _startPosX + distX;

            layer.position = new Vector3(newPosX, layer.position.y, layer.position.z);

            // 루핑 처리
            if (_tileWidth > 0f)
            {
                if (tempX > _startPosX + _tileWidth)
                {
                    _startPosX += _tileWidth;
                }
                else if (tempX < _startPosX - _tileWidth)
                {
                    _startPosX -= _tileWidth;
                }
            }
        }
    }

    public class ParallaxBackground : MonoBehaviour
    {
        [Header("Parallax Settings")]
        [SerializeField] private List<ParallaxLayer> _parallaxLayers = new();

        [Header("Background Movement")]
        [Range(0.9f, 1f)]
        [SerializeField] private float _parallaxSpeedY = 0.9f;

        private Transform _cameraTransform;
        private Vector3 _startPosition;

        private void LateUpdate()
        {
            if (_cameraTransform == null)
                return;

            Vector3 cameraPos = _cameraTransform.position;
            float newPosY = _startPosition.y + (cameraPos.y - _startPosition.y) * _parallaxSpeedY;
            transform.position = new Vector3(cameraPos.x, newPosY, transform.position.z);

            if (_parallaxLayers == null || _parallaxLayers.Count == 0)
                return;

            foreach (var layer in _parallaxLayers)
            {
                layer.ParallaxMove();
            }
        }

        public void SetupParallax(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
            _startPosition = transform.position;

            if (_parallaxLayers.Count == 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    _parallaxLayers.Add(new ParallaxLayer
                    {
                        layer = transform.GetChild(i),
                        parallaxSpeed = 0.5f
                    });
                }
            }

            foreach (var layer in _parallaxLayers)
            {
                layer.SetupParallaxLayer(cameraTransform);
            }
        }
    }
}