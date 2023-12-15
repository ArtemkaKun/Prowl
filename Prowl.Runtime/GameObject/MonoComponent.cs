﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Prowl.Runtime;

public abstract class MonoBehaviour : EngineObject
{
    public static bool PauseLogic = false;

    [SerializeField]
    private bool _enabled = true;
    [SerializeField]
    private bool _enabledInHierarchy = true;

    private bool _hasBeenEnabled = false;

    [HideInInspector]
    public HideFlags hideFlags;

    private Dictionary<string, Coroutine> _coroutines = new();
    private Dictionary<string, Coroutine> _endOfFrameCoroutines = new();

    private MethodInfo onEnable;
    private MethodInfo onDisable;
    private MethodInfo awake;
    private MethodInfo start;
    private MethodInfo fixedUpdate;
    private MethodInfo update;
    private MethodInfo lateUpdate;
    private MethodInfo onSceneLoaded;
    private MethodInfo onDestroy;
    private MethodInfo onRenderObject;
    private MethodInfo onRenderObjectDepth;
    private MethodInfo drawGizmos;
    private MethodInfo drawGizmosSelected;

    public bool HasStarted { get; set; } = false;

    public enum RenderingOrder { None, Opaque, Lighting }
    public virtual RenderingOrder RenderOrder => RenderingOrder.None;


    private GameObject _go;

    public GameObject GameObject => _go;

    private bool executeAlways = false;
    private bool hasCached = false;

    public string Tag => _go.tag;

    public bool Enabled
    {
        get { return _enabled; }
        set
        {
            if (value != _enabled)
            {
                _enabled = value;
                HierarchyStateChanged();
            }
        }
    }

    public bool EnabledInHierarchy => _enabledInHierarchy;
    public bool HasBeenEnabled => _hasBeenEnabled;

    public MonoBehaviour() : base() { }

    public bool CompareTag(string otherTag) => _go.CompareTag(otherTag);

    #region Component API
    // This is used to make the Component API more similar to Unity's, Its generally recommended to use the GameObject instead
    public T AddComponent<T>() where T : MonoBehaviour, new() => (T)AddComponent(typeof(T));
    public MonoBehaviour AddComponent(Type type) => GameObject.AddComponent(type);
    public void RemoveAll<T>() where T : MonoBehaviour => GameObject.RemoveAll<T>();
    public void RemoveComponent<T>(T component) where T : MonoBehaviour => GameObject.RemoveComponent(component);
    public void RemoveComponent(MonoBehaviour component) => GameObject.RemoveComponent(component);
    public void RemoveSelf() => GameObject.RemoveComponent(this);
    public T GetComponent<T>() where T : MonoBehaviour => GameObject.GetComponent<T>();
    public MonoBehaviour GetComponent(Type type) => GameObject.GetComponent(type);
    public bool TryGetComponent<T>(out T component) where T : MonoBehaviour => (component = GetComponent<T>()) != null;
    public IEnumerable<T> GetComponents<T>() where T : MonoBehaviour => GameObject.GetComponents<T>();
    public T GetComponentInParent<T>() where T : MonoBehaviour => GameObject.GetComponentInParent<T>();
    public MonoBehaviour GetComponentInParent(Type componentType) => GameObject.GetComponentInParent(componentType);
    public IEnumerable<T> GetComponentsInParent<T>() where T : MonoBehaviour => GameObject.GetComponentsInParent<T>();
    public T GetComponentInChildren<T>() where T : MonoBehaviour => GameObject.GetComponentInChildren<T>();
    public MonoBehaviour GetComponentInChildren(Type componentType) => GameObject.GetComponentInChildren(componentType);
    public IEnumerable<T> GetComponentsInChildren<T>() where T : MonoBehaviour => GameObject.GetComponentsInChildren<T>();
    #endregion

    internal void AttachToGameObject(GameObject go)
    {
        _go = go;
        HierarchyStateChanged();
    }

    internal void HierarchyStateChanged()
    {
        bool newState = _enabled && _go.EnabledInHierarchy;
        _hasBeenEnabled |= newState;
        if (newState != _enabledInHierarchy)
        {
            _enabledInHierarchy = newState;
            if (newState)
                Internal_OnEnabled();
            else
                Internal_OnDisabled();
        }
    }

    internal bool CanDestroy()
    {
#warning "Need to apply this to Component Deletion in Inspector, to make sure not to delete dependant Components"
        if (_go.IsComponentRequired(this, out Type dependentType))
        {
            Debug.LogError("Can't remove " + this.GetType().Name + " because " + dependentType.Name + " depends on it");
            return false;
        }
        return true;
    }

    #region Behaviour

    internal bool Internal_Awake()
    {
        if (!hasCached)
        {
            hasCached = true;
            List<string> methodNames = new List<string>()
            {
                "OnEnable",
                "OnDisable",
                "Awake",
                "Start",
                "FixedUpdate",
                "Update",
                "LateUpdate",
                "OnDestroy",
                "OnRenderObject",
                "OnRenderObjectDepth",
                "DrawGizmos",
                "DrawGizmosSelected",
                "OnSceneLoaded",
            };

            MethodInfo[] retMethods = new MethodInfo[methodNames.Count];
            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                var ind = methodNames.IndexOf(method.Name);
                if (ind != -1) retMethods[ind] = method;
            }

            onEnable = retMethods[0];
            onDisable = retMethods[1];
            awake = retMethods[2];
            start = retMethods[3];
            fixedUpdate = retMethods[4];
            update = retMethods[5];
            lateUpdate = retMethods[6];
            onDestroy = retMethods[7];
            onRenderObject = retMethods[8];
            onRenderObjectDepth = retMethods[9];
            drawGizmos = retMethods[10];
            drawGizmosSelected = retMethods[11];
            onSceneLoaded = retMethods[12];

            executeAlways = this.GetType().GetCustomAttribute<ExecuteAlwaysAttribute>() != null;
        }

        if (!PauseLogic || executeAlways)
        {
            awake?.Invoke(this, []);
            onEnable?.Invoke(this, []);
            return true;
        }
        return false;
    }

    internal void Internal_OnEnabled()
    {
        if (!PauseLogic || executeAlways) onEnable?.Invoke(this, []);
    }
    internal void Internal_OnDisabled()
    {
        if (!PauseLogic || executeAlways) onDisable?.Invoke(this, []);
    }
    internal void Internal_Start()
    {
        if (!PauseLogic || executeAlways) start?.Invoke(this, []);
    }
    internal void Internal_FixedUpdate()
    {
        if (!PauseLogic || executeAlways) fixedUpdate?.Invoke(this, []);
    }
    internal void Internal_Update()
    {
        if (!PauseLogic || executeAlways) update?.Invoke(this, []);
    }
    internal void Internal_LateUpdate()
    {
        if (!PauseLogic || executeAlways) lateUpdate?.Invoke(this, []);
    }
    internal void Internal_OnSceneLoaded()
    {
        if (!PauseLogic || executeAlways) onSceneLoaded?.Invoke(this, []);
    }
    internal void Internal_OnDestroy()
    {
        if (!PauseLogic || executeAlways) onDestroy?.Invoke(this, []);
    }
    internal void Internal_DrawGizmos(System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection)
    {
        drawGizmos?.Invoke(this, [ view, projection ]);
    }
    internal void Internal_DrawGizmosSelected(System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection)
    {
        drawGizmosSelected?.Invoke(this, [ view, projection ]);
    }

    public Coroutine StartCoroutine(string methodName)
    {
        methodName = methodName.Trim();
        var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (method == null)
        {
            Debug.LogError("Coroutine '" + methodName + "' couldn't be started, the method doesn't exist!");
            return null;
        }

        var coroutine = new Coroutine(method.Invoke(this, null) as IEnumerator);

        if (coroutine.Enumerator.Current is WaitForEndOfFrame)
            _endOfFrameCoroutines.Add(methodName, coroutine);
        else
            _coroutines.Add(methodName, coroutine);

        return coroutine;
    }

    public void StopAllCoroutines()
    {
        _coroutines.Clear();
        _endOfFrameCoroutines.Clear();
    }

    public void StopCoroutine(string methodName)
    {
        methodName = methodName.Trim();
        _coroutines.Remove(methodName);
        _endOfFrameCoroutines.Remove(methodName);
    }

    public class YieldInstruction
    {
    }

    public class WaitForSeconds : YieldInstruction
    {
        public double Duration { get; private set; }
        public WaitForSeconds(float seconds)
        {
            Duration = Time.time + seconds;
        }
    }

    public class WaitForEndOfFrame : YieldInstruction
    {
    }

    public sealed class Coroutine : YieldInstruction
    {
        internal bool isDone { get; private set; }
        internal IEnumerator Enumerator { get; private set; }
        internal Coroutine(IEnumerator routine)
        {
            Enumerator = routine;
        }

        internal bool CanRun
        {
            get
            {
                object current = Enumerator.Current;

                if (current is Coroutine)
                {
                    Coroutine dep = current as Coroutine;
                    return dep.isDone;
                }
                else if (current is WaitForSeconds)
                {
                    WaitForSeconds wait = current as WaitForSeconds;
                    return wait.Duration <= Time.time;
                }
                else
                {
                    return true;
                }
            }
        }

        internal void Run()
        {
            if (CanRun)
            {
                isDone = !Enumerator.MoveNext();
            }
        }
    }

    internal void UpdateCoroutines()
    {
        _coroutines ??= new Dictionary<string, Coroutine>();
        var tempList = new Dictionary<string, Coroutine>(_coroutines);
        _coroutines.Clear();
        foreach (var coroutine in tempList)
        {
            coroutine.Value.Run();
            if (coroutine.Value.isDone)
            {
                if (coroutine.Value.Enumerator.Current is WaitForEndOfFrame)
                    _endOfFrameCoroutines.Add(coroutine.Key, coroutine.Value);
                else
                    _coroutines.Add(coroutine.Key, coroutine.Value);
            }
        }
    }

    internal void UpdateEndOfFrameCoroutines()
    {
        _endOfFrameCoroutines ??= new Dictionary<string, Coroutine>();
        var tempList = new Dictionary<string, Coroutine>(_endOfFrameCoroutines);
        _endOfFrameCoroutines.Clear();
        foreach (var coroutine in tempList)
        {
            coroutine.Value.Run();
            if (coroutine.Value.isDone)
            {
                if (coroutine.Value.Enumerator.Current is WaitForEndOfFrame)
                    _endOfFrameCoroutines.Add(coroutine.Key, coroutine.Value);
                else
                    _coroutines.Add(coroutine.Key, coroutine.Value);
            }
        }
    }

    // Rendering always occurs
    internal void Internal_OnRenderObject() => onRenderObject?.Invoke(this, []);

    // Rendering always occurs
    internal void Internal_OnRenderObjectDepth() => onRenderObjectDepth?.Invoke(this, []);

    /// <summary> Calls the method named methodName on every MonoBehaviour in this game object or any of its children. </summary>
    public void BroadcastMessage(string methodName, params object[] objs) => GameObject.BroadcastMessage(methodName, objs);

    /// <summary> Calls the method named methodName on every MonoBehaviour in this game object. </summary>
    public void SendMessage(string methodName, params object[] objs) => GameObject.SendMessage(methodName, objs);


    #endregion
}
