using System;
using System.Collections.Generic;
using UnityEngine;

public class ServiceContainer : MonoBehaviour
{
    public static ServiceContainer Instance { get; private set; }

    private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
    private readonly Dictionary<Type, Func<object>> factories = new Dictionary<Type, Func<object>>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            RegisterDefaultServices();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void RegisterDefaultServices()
    {
        // Register CacheService
/*        var tokenManager = FindObjectOfType<TokenManager>();
        if (tokenManager == null)
        {
            var tokenManagerGO = new GameObject("TokenManager");
            tokenManager = tokenManagerGO.AddComponent<TokenManager>();
        }
        Register<TokenManager>(tokenManager);

        var authAPI = FindObjectOfType<AuthenticationAPI>();
        if (authAPI == null)
        {
            var authAPIGO = new GameObject("AuthenticationAPI");
            authAPI = authAPIGO.AddComponent<AuthenticationAPI>();
        }
        Register<AuthenticationAPI>(authAPI);

        var authService = FindObjectOfType<AuthenticationService>();
        if (authService == null)
        {
            var authServiceGO = new GameObject("AuthenticationService");
            authService = authServiceGO.AddComponent<AuthenticationService>();
        }
        Register<IAuthenticationService>(authService);

        Debug.Log("✅ Authentication services registered");
*/
        
    }

    public void Register<T>(T service)
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        services[typeof(T)] = service;
        Debug.Log($"ServiceContainer: Registered {typeof(T).Name}");
    }

    public void RegisterFactory<T>(Func<object> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        factories[typeof(T)] = factory;
        Debug.Log($"ServiceContainer: Registered factory for {typeof(T).Name}");
    }

    public T Get<T>()
    {
        Type type = typeof(T);

        // Try to get existing service
        if (services.TryGetValue(type, out object service))
        {
            return (T)service;
        }

        // Try to create from factory
        if (factories.TryGetValue(type, out Func<object> factory))
        {
            object newService = factory();
            services[type] = newService;
            return (T)newService;
        }

        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
    }

    public bool IsRegistered<T>()
    {
        return services.ContainsKey(typeof(T)) || factories.ContainsKey(typeof(T));
    }

    public void Unregister<T>()
    {
        Type type = typeof(T);
        services.Remove(type);
        factories.Remove(type);
        Debug.Log($"ServiceContainer: Unregistered {type.Name}");
    }

    private void OnDestroy()
    {
        services.Clear();
        factories.Clear();
    }
}
