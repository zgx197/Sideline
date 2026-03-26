// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

namespace Lattice.Tools
{
    /// <summary>
    /// 定点数查找表生成器入口
    /// 用法: dotnet run [-- <output-directory>]
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            string outputPath = args.Length > 0 
                ? args[0] 
                : Path.Combine(GetProjectRoot(), "godot", "scripts", "lattice", "Math", "Generated");

            try
            {
                LutGeneratorCore.GenerateAll(outputPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        static string GetProjectRoot()
        {
            // 从执行路径向上查找包含 .git 或 godot 目录的根目录
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, ".git")) || 
                    Directory.Exists(Path.Combine(current, "godot")))
                {
                    return current;
                }
                current = Directory.GetParent(current)?.FullName;
            }
            return Directory.GetCurrentDirectory();
        }
    }
}
