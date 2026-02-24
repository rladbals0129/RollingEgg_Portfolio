using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RollingEgg.UI
{
    public interface IUIEntry<TEnum> where TEnum : Enum
    {
        TEnum Type { get; }
        AssetReferenceGameObject PrefabReference { get; }
    }

    [CreateAssetMenu(fileName = "UIRegistry", menuName = "Scriptable Objects/UI/UIRegistry")]
    public class UIRegistry : ScriptableObject
    {
        [System.Serializable]
        public class SceneEntry : IUIEntry<ESceneUIType>
        {
            [SerializeField] private ESceneUIType _type;
            [SerializeField] private AssetReferenceGameObject _prefabReference;

            ESceneUIType IUIEntry<ESceneUIType>.Type => _type;
            AssetReferenceGameObject IUIEntry<ESceneUIType>.PrefabReference => _prefabReference;
        }

        [System.Serializable]
        public class PopupEntry : IUIEntry<EPopupUIType>
        {
            [SerializeField] private EPopupUIType _type;
            [SerializeField] private AssetReferenceGameObject _prefabReference;

            EPopupUIType IUIEntry<EPopupUIType>.Type => _type;
            AssetReferenceGameObject IUIEntry<EPopupUIType>.PrefabReference => _prefabReference;
        }

        public List<SceneEntry> Scenes;
        public List<PopupEntry> Popups;
    }
}
