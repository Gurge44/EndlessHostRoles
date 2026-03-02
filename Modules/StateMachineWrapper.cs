// https://github.com/XtraCube/Reactor/blob/main/Reactor/Utilities/StateMachineWrapper.cs

using System;
using System.Reflection;
using HarmonyLib;
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

namespace EHR.Modules;

/// <summary>
/// A wrapper for state machine objects to access their parent instance and state.
/// </summary>
/// <typeparam name="T">The type of the parent class that owns the state machine.</typeparam>
public class StateMachineWrapper<T> : CompilerGeneratedObjectWrapper
{
    // normally it is fields, but IL2CPP turns them into properties
    private readonly PropertyInfo _thisProperty;
    private readonly PropertyInfo _stateProperty;

    private T? _parentInstance;

    /// <summary>
    /// Gets the instance of the parent class that owns the state machine.
    /// </summary>
    public T Instance => _parentInstance ??= (T) _thisProperty.GetValue(GeneratedObject)!;

    /// <summary>
    /// Gets or sets the current state of the state machine.
    /// </summary>
    /// <returns>The current state as an integer.</returns>
    public int State
    {
        get => (int) _stateProperty.GetValue(GeneratedObject)!;
        set => _stateProperty.SetValue(GeneratedObject, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineWrapper{T}"/> class.
    /// </summary>
    /// <param name="stateMachine">The state machine instance to wrap.</param>
    public StateMachineWrapper(object stateMachine) : base(stateMachine)
    {
        _thisProperty = AccessTools.Property(GeneratedType, "__4__this");
        _stateProperty = AccessTools.Property(GeneratedType, "__1__state");

        if (_thisProperty == null || _stateProperty == null)
        {
            throw new MissingMemberException($"Could not find required properties in type '{GeneratedType}'.");
        }
    }

    /// <summary>
    /// Gets a parameter from the state machine by its name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter to retrieve.</param>
    /// <typeparam name="TField">The type of the parameter to retrieve.</typeparam>
    /// <returns>>The value of the specified parameter.</returns>
    /// <exception cref="MissingFieldException">Thrown if the specified parameter does not exist.</exception>
    public TField GetParameter<TField>(string parameterName)
    {
        return GetField<TField>(parameterName);
    }

    /// <summary>
    /// Sets a parameter in the state machine by its name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter to set.</param>
    /// <param name="value">The value to set for the parameter.</param>
    /// <typeparam name="TField">The type of the parameter to set.</typeparam>
    /// <exception cref="MissingFieldException">Thrown if the specified parameter does not exist.</exception>
    public void SetParameter<TField>(string parameterName, TField value)
    {
        SetField(parameterName, value);
    }
}