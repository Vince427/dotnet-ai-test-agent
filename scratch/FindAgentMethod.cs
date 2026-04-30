using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        var paths = new[]
        {
            @"C:\Users\vince\.nuget\packages\microsoft.agents.ai\1.3.0\lib\netstandard2.0\Microsoft.Agents.AI.dll",
            @"C:\Users\vince\.nuget\packages\microsoft.agents.ai.openai\1.3.0\lib\netstandard2.0\Microsoft.Agents.AI.OpenAI.dll",
            @"C:\Users\vince\.nuget\packages\microsoft.extensions.ai\9.0.0-preview.9.24556.5\lib\netstandard2.0\Microsoft.Extensions.AI.dll"
        };
        
        foreach (var path in paths)
        {
            try
            {
                var asm = Assembly.LoadFrom(path);
                Console.WriteLine($"Loaded {asm.GetName().Name}");
                foreach (var type in asm.GetTypes().Where(t => t.IsSealed && !t.IsGenericType && !t.IsNested))
                {
                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    {
                        if (method.Name.Contains("AsAIAgent"))
                        {
                            Console.WriteLine($"Found AsAIAgent in {type.FullName}!");
                            Console.WriteLine($"Signature: {method}");
                            foreach(var param in method.GetParameters())
                            {
                                Console.WriteLine($" - {param.ParameterType} {param.Name}");
                            }
                        }
                    }
                }
            }
            catch(Exception e) { Console.WriteLine("Failed to load " + path + ": " + e.Message); }
        }
    }
}
