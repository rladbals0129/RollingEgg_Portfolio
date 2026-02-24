using System;
using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Core
{
    public class ServiceLocator
    {
        private static Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// 제네릭을 사용하여 타입 안전성을 보장하는 방식으로 서비스를 등록
        /// </summary>
        /// <typeparam name="TInterface">서비스 인터페이스 타입</typeparam>
        /// <typeparam name="TImplementation">서비스의 실제 인스턴스</typeparam>
        /// <param name="service"></param>
        public static void Register<TInterface, TImplementation>(TImplementation service) where TImplementation : class, TInterface
        {
            Type interfaceType = typeof(TInterface);
            if (_services.ContainsKey(interfaceType))
            {
                Debug.LogWarning($"Service {interfaceType.Name} already registered. Overwriting...");
                _services[interfaceType] = service;
            }
            else
            {
                _services.Add(interfaceType, service);
                Debug.Log($"Service {interfaceType.Name} registered.");
            }
        }

        /// <summary>
        /// 리플렉션 등 동적으로 타입을 다뤄야 할 때 사용하는, 보다 유연한 방식의 서비스 등록
        /// </summary>
        /// <param name="interfaceType">서비스 인터페이스 타입</param>
        /// <param name="serviceInstance">서비스의 실제 인스턴스</param>
        public static void Register(Type interfaceType, object serviceInstance)
        {
            if (_services.ContainsKey(interfaceType))
            {
                Debug.LogWarning($"Service {interfaceType.Name} already registered. Overwriting...");
                _services[interfaceType] = serviceInstance;
            }
            else
            {
                _services.Add(interfaceType, serviceInstance);
                Debug.Log($"Service {interfaceType.Name} registered.");
            }
        }

        /// <summary>
        /// 서비스 제거
        /// </summary>
        public static bool Unregister<TInterface>()
        {
            Type interfaceType = typeof(TInterface);

            if (_services.ContainsKey(interfaceType))
            {
                _services.Remove(interfaceType);
                Debug.Log($"Service {interfaceType.Name} unregistered.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 서비스 가져오기
        /// </summary>
        public static TInterface Get<TInterface>()
        {
            Type interfaceType = typeof(TInterface);

            if (_services.TryGetValue(interfaceType, out object serviceObject))
            {
                return (TInterface)serviceObject;
            }

            Debug.LogError($"Service {interfaceType.Name} not found!");
            return default;
        }

        /// <summary>
        /// 서비스 존재 여부 확인
        /// </summary>
        public static bool HasService<TInterface>()
        {
            return _services.ContainsKey(typeof(TInterface));
        }
    }
}
