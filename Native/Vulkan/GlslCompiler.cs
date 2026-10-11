using System.Diagnostics;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Invokes standard Khronos GLSL compilers ('glslangValidator' or 'glslc') to compile GLSL compute shaders into aligned SPIR-V binary words.
/// </summary>
public static class GlslCompiler
{
    private const int DefaultCompilationTimeoutMs = 30000;

    /// <summary>
    /// Compiles raw GLSL compute shader source code targeting Vulkan into 32-bit aligned SPIR-V binary words.
    /// </summary>
    /// <param name="glslSource">Raw GLSL compute source text.</param>
    /// <param name="extraArgs">Optional arguments passed to glslangValidator.</param>
    /// <returns>An array of 32-bit unsigned integer words representing the SPIR-V module.</returns>
    /// <exception cref="InvalidOperationException">Thrown when compiler execution fails or output alignment is invalid.</exception>
    public static uint[] CompileToSpirv(string glslSource, string extraArgs = "")
    {
        string tempGlsl = Path.Combine(Path.GetTempPath(), $"gput_{Guid.NewGuid():N}.comp");
        string tempSpv = Path.Combine(Path.GetTempPath(), $"gput_{Guid.NewGuid():N}.spv");

        try
        {
            File.WriteAllText(tempGlsl, glslSource);

            ProcessStartInfo psi = new()
            {
                FileName = "glslangValidator",
                Arguments = $"-V -S comp \"{tempGlsl}\" {extraArgs} -o \"{tempSpv}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to launch 'glslangValidator'. Ensure Vulkan SDK / glslang is installed and available in PATH.");

            string stderr = process.StandardError.ReadToEnd();
            string stdout = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(DefaultCompilationTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new InvalidOperationException($"GLSL compilation timed out after {DefaultCompilationTimeoutMs / 1000} seconds.");
            }

            if (process.ExitCode != 0 || !File.Exists(tempSpv))
            {
                string errorDetails = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
                throw new InvalidOperationException($"GLSL compilation failed (Exit code {process.ExitCode}):\n{errorDetails.Trim()}");
            }

            byte[] bytes = File.ReadAllBytes(tempSpv);
            if (bytes.Length == 0 || bytes.Length % 4 != 0)
            {
                throw new InvalidOperationException($"Invalid SPIR-V binary alignment produced by glslangValidator (Size: {bytes.Length} bytes).");
            }

            uint[] words = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);
            return words;
        }
        finally
        {
            if (File.Exists(tempGlsl)) { try { File.Delete(tempGlsl); } catch { } }
            if (File.Exists(tempSpv)) { try { File.Delete(tempSpv); } catch { } }
        }
    }
}