using UDND.Tools;
using UnityEngine;

namespace CodeUtils
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        protected static T _instance = null;
        public static bool IsInstanceExist => _instance != null;
        private static bool _isSelfCreating = false;
        public static T Instance => _instance;
        public static T AutoCreateInstance {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>();
                    if (_instance == null)
                    {
                        _isSelfCreating = true;
                        var go = new GameObject(typeof(T).Name);
                        _instance = go.AddComponent<T>();
                        _isSelfCreating = false;
                    }

                    _instance.Init();
                }
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        protected virtual void Init()
        {
            Extensions.DragAndDropLog($"<color=green>Initializing {typeof(T).Name}</color>");
        }

        protected virtual void DeInit()
        {
            Extensions.DragAndDropLog($"<color=red>Deinitializing {typeof(T).Name}</color>");
        }

        public virtual void Awake()
        {
            if (!_isSelfCreating)
            {
                if (_instance == null)
                {
                    _instance = (T) this;
                    Init();
                }
                else if (_instance != this)
                    Destroy(gameObject);
            }
        }
        public virtual void OnDestroy()
        {
            if (_instance == this)
            {
                DeInit();
                _instance = null;
            }
        }
    }
}