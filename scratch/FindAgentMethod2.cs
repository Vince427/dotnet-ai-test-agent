using System;
using System.IO;
using System.Reflection;

class Program
{
    static void Main()
    {
        try
        {
            var path = @"C:\Users\vince\.nuget\packages\microsoft.agents.ai.openai\1.3.0\lib\netstandard2.0\Microsoft.Agents.AI.OpenAI.dll";
            var asmBytes = File.ReadAllBytes(path);
            var asm = Assembly.Load(asmBytes);
            foreach (var type in asm.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name.Contains("AsAIAgent") || method.Name.Contains("Agent"))
                    {
                        Console.WriteLine($"Found {method.Name} in {type.FullName}");
                        foreach(var p in method.GetParameters()) Console.WriteLine("  Param: " + p.ParameterType.FullName);
                    }
                }
            }
        } catch(Exception e) { Console.WriteLine("ERR: " + e); }
    }
}
