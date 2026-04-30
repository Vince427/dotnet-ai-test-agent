// See https://aka.ms/new-console-template for more information
using System;
using System.Reflection;
using System.Linq;

try
{
    var path = @"C:\Users\vince\.nuget\packages\microsoft.agents.ai.openai\1.3.0\lib\netstandard2.0\Microsoft.Agents.AI.OpenAI.dll";
    var asm = Assembly.LoadFrom(path);
    Console.WriteLine("Loaded: " + asm.FullName);
    foreach (var type in asm.GetTypes().Where(t => t.IsSealed && t.IsAbstract && !t.IsGenericType))
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name.Contains("Agent"))
            {
                Console.WriteLine($"Found {method.Name} in {type.FullName}");
                foreach(var p in method.GetParameters()) Console.WriteLine("  Param: " + p.ParameterType.FullName + " " + p.Name);
            }
        }
    }
} catch(Exception e) { Console.WriteLine("ERR: " + e); }
