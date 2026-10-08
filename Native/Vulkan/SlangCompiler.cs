using System.Diagnostics;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Invokes the external Slang compiler CLI tool ('slangc') to compile source code into aligned SPIR-V bytecode.
/// </summary>
public static class SlangCompiler
{
    private const int DefaultCompilationTimeoutMs = 30000;

    /// <summary>
    /// Compiles raw Slang/HLSL source text into 32-bit aligned SPIR-V binary words.
    /// </summary>
    /// <param name="slangSource">The complete shader source code.</param>
    /// <param name="entryPoint">Compute shader entry point name (defaults to 'main').</param>
    /// <param name="profile">Target SPIR-V profile version (e.g., 'spirv_1_0', 'spirv_1_3').</param>
    /// <param name="extraArgs">Optional compiler flags passed directly to slangc.</param>
    /// <returns>An array of 32-bit unsigned integer words representing the SPIR-V module.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if slangc fails to launch, execution times out, compilation fails, or output binary alignment is invalid.
    /// </exception>
    public static uint[] CompileToSpirv(
        string slangSource,
        string entryPoint = "main",
        string profile = "spirv_1_0",
        string extraArgs = "")
    {
        string tempSlang = Path.Combine(Path.GetTempPath(), $"gput_{Guid.NewGuid():N}.slang");
        string tempSpv = Path.Combine(Path.GetTempPath(), $"gput_{Guid.NewGuid():N}.spv");

        try
        {
            File.WriteAllText(tempSlang, slangSource);

            ProcessStartInfo psi = new()
            {
                FileName = "slangc",
                Arguments = $"\"{tempSlang}\" -target spirv -entry {entryPoint} -stage compute -profile {profile} {extraArgs} -o \"{tempSpv}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to launch 'slangc'. Ensure Slang is installed and available in the system PATH.");

            string stderr = process.StandardError.ReadToEnd();
            string stdout = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(DefaultCompilationTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new InvalidOperationException($"Slang compilation timed out after {DefaultCompilationTimeoutMs / 1000} seconds.");
            }

            if (process.ExitCode != 0 || !File.Exists(tempSpv))
            {
                string errorDetails = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
                throw new InvalidOperationException($"Slang compilation failed (Exit code {process.ExitCode}):\n{errorDetails.Trim()}");
            }

            byte[] bytes = File.ReadAllBytes(tempSpv);
            if (bytes.Length == 0 || bytes.Length % 4 != 0)
            {
                throw new InvalidOperationException($"Invalid SPIR-V binary alignment produced by Slang (Size: {bytes.Length} bytes).");
            }

            uint[] words = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);
            return words;
        }
        finally
        {
            if (File.Exists(tempSlang))
            {
                try { File.Delete(tempSlang); } catch { }
            }
            if (File.Exists(tempSpv))
            {
                try { File.Delete(tempSpv); } catch { }
            }
        }
    }
}