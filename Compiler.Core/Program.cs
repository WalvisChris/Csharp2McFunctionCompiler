using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;

namespace Compiler.Core
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Minecraft C# Compiler ===");

            // 1. Point to the user script file
            // Note: This relative path looks up out of Compiler.Core's bin folder to find UserScripts
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\..\\UserScripts\\Program.cs");

            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"Error: Could not find user script at {Path.GetFullPath(scriptPath)}");
                return;
            }

            // 2. Read the C# file as plain text
            string csharpCode = File.ReadAllText(scriptPath);
            Console.WriteLine("Reading User Script...");

            // 3. Hand the text over to Roslyn to build the Abstract Syntax Tree (AST)
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);
            SyntaxNode rootNode = syntaxTree.GetRoot();

            // 4 & 5. Initialize our compiler and trigger the compilation passes
            var compiler = new McFunctionCompiler();
            var compiledFiles = compiler.Compile((CSharpSyntaxNode)rootNode);

            // 6. Print the results to the console (and save them!)
            Console.WriteLine("\n--- Compilation Result ---");

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string solutionRootDir = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\..\\"));
            string outputDir = Path.Combine(solutionRootDir, "Output", "functions");

            // Wipe old output folder so deleted C# functions don't hang around forever
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            foreach (var fileEntry in compiledFiles)
            {
                string functionName = fileEntry.Key;
                List<string> commands = fileEntry.Value;

                // Skip writing files that ended up completely empty
                if (commands.Count == 0) continue;

                string outputPath = Path.Combine(outputDir, $"{functionName}.mcfunction");
                File.WriteAllLines(outputPath, commands);

                Console.WriteLine($"Generated: {functionName}.mcfunction ({commands.Count} commands)");
            }

            Console.WriteLine($"\nSuccess! Datapack files updated inside Output directory.");
            Console.ReadLine();
        }
    }
}