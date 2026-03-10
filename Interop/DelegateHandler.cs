using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MelInEx.Interop;

public class DelegateHandler
{
    public static Delegate CreateForwarder(Type forwarderType, Type delegateType, string handlerName)
    {
        // Get delegate signature
        MethodInfo invoke = delegateType.GetMethod("Invoke");

        Type returnType = invoke.ReturnType;
        Type[] parameters = invoke.GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        // Create dynamic method matching the signature
        DynamicMethod dm = new DynamicMethod(
            "Forwarder_" + handlerName,
            returnType,
            parameters,
            forwarderType.GetType().Module,
            true);

        ILGenerator il = dm.GetILGenerator();

        // Load all arguments onto the stack
        for (short i = 0; i < parameters.Length; i++)
            il.Emit(OpCodes.Ldarg, i);

        // Call our managed handler

        MethodInfo target = AccessTools.Method(forwarderType, handlerName);

        // Emit that shit and forward the poor word-ing of that rhyme
        il.Emit(OpCodes.Call, target);
        il.Emit(OpCodes.Ret);

        // Create delegate instance
        return dm.CreateDelegate(delegateType);
    }
}