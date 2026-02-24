using System;
using System.Collections.Generic;
using UnityEngine;

namespace RollingEgg.Core
{
    public interface IEventBus
    {
        void Initialize();
        void Subscribe<T>(Action<T> callback) where T : struct;
        void Unsubscribe<T>(Action<T> callback) where T : struct;
        void Publish<T>(T eventData) where T : struct;
    }

    public class EventBus : IEventBus
    {
        private Dictionary<Type, List<Delegate>> _eventDictionary = new Dictionary<Type, List<Delegate>>();

        public void Initialize()
        {
            _eventDictionary.Clear();
            Debug.Log("EventBus Initialized.");
        }

        /// <summary>
        /// 특정 타입의 이벤트를 구독
        /// </summary>
        /// <typeparam name="T">구독할 이벤트의 데이터 타입</typeparam>
        /// <param name="callback">이벤트 발생 시 호출될 콜백 함수</param>
        public void Subscribe<T>(Action<T> callback) where T : struct
        {
            if (callback == null) 
                return;

            Type eventType = typeof(T);

            if (!_eventDictionary.ContainsKey(eventType))
            {
                _eventDictionary[eventType] = new List<Delegate>();
            }

            _eventDictionary[eventType].Add(callback);
        }

        /// <summary>
        /// 구독했던 이벤트를 해제
        /// </summary>
        public void Unsubscribe<T>(Action<T> callback) where T : struct
        {
            if (callback == null)
                return;

            Type eventType = typeof(T);

            if (_eventDictionary.ContainsKey(eventType))
            {
                _eventDictionary[eventType].Remove(callback);
            }
        }

        /// <summary>
        /// 이벤트를 발행하여, 해당 이벤트를 구독하는 모든 대상에게 알림
        /// </summary>
        /// <typeparam name="T">발행할 이벤트의 데이터 타입</typeparam>
        /// <param name="eventData">전달할 이벤트 데이터</param>
        public void Publish<T>(T eventData) where T : struct
        {
            Type eventType = typeof(T);

            if (_eventDictionary.TryGetValue(eventType, out var delegates))
            {
                if (delegates == null || delegates.Count == 0)
                    return;

                var callbacksToInvoke = new List<Delegate>(delegates);

                foreach (var callback in callbacksToInvoke)
                {
                    ((Action<T>)callback)?.Invoke(eventData);
                }
            }
        }
    }
}
