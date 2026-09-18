using System.Diagnostics;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Runtime Slang-to-SPIR-V compilation engine.
/// Compiles Slang/HLSL compute kernels strictly into Vulkan 1.0 (SPIR-V 1.0) binaries.
/// </summary>
public static class SlangCompiler
{
    /// <summary>
    /// Compiles a Slang source string to SPIR-V 1.0 bytecode words.
    /// </summary>
    /// <param name="slangSource">The Slang/HLSL source code to compile.</param>
    /// <param name="entryPoint">The name of the compute entry point function.</param>
    /// <param name="profile">The target SPIR-V version profile (defaults to spirv_1_0).</param>
    /// <returns>Array of 32-bit SPIR-V instruction words.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the compiler executable is missing or compilation fails.
    /// </exception>
    public static uint[] CompileToSpirv(string slangSource, string entryPoint = "main", string profile = "spirv_1_0")
    {
        string tempSlang = Path.Combine(Path.GetTempPath(), $"gput_{Guid.NewGuid():N}.slang");
        string tempSpv = Path.Combine(Path.GetTempPath(), $"gput_{Guid.NewGuid():N}.spv");

        try
        {
            File.WriteAllText(tempSlang, slangSource);

            ProcessStartInfo psi = new()
            {
                FileName = "slangc",
                Arguments = $"\"{tempSlang}\" -target spirv -entry {entryPoint} -stage compute -profile {profile} -o \"{tempSpv}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) 
                ?? throw new InvalidOperationException("Failed to launch 'slangc'. Make sure Slang is installed and available in PATH.");

            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(tempSpv))
            {
                throw new InvalidOperationException($"Slang compilation failed:\n{stderr}");
            }

            byte[] bytes = File.ReadAllBytes(tempSpv);
            if (bytes.Length % 4 != 0)
            {
                throw new InvalidOperationException("Invalid SPIR-V binary alignment produced by Slang.");
            }

            uint[] words = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);
            return words;
        }
        finally
        {
            if (File.Exists(tempSlang)) File.Delete(tempSlang);
            if (File.Exists(tempSpv)) File.Delete(tempSpv);
        }
    }
}