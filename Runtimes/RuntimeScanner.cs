using System.Diagnostics;

namespace GpuT.Agent.Runtimes;

public enum RuntimeType
{
    Wine,
    Proton
}

public sealed record RuntimeEnvironment(
    string Name,
    RuntimeType Type,
    string ExecutablePath,
    string? PrefixPath = null
);

public static class RuntimeScanner
{
    public static List<RuntimeEnvironment> DiscoverAll()
    {
        var runtimes = new List<RuntimeEnvironment>();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // 1. Поиск системного Wine и Wine-Staging
        ProbeSystemWine(runtimes);

        // 2. Поиск Proton в Steam (Native)
        ProbeSteamProton(Path.Combine(home, ".local/share/Steam"), runtimes);
        ProbeSteamProton(Path.Combine(home, ".steam/root"), runtimes);
        ProbeSteamProton(Path.Combine(home, ".steam/steam"), runtimes);

        // 3. Поиск Proton в Steam (Flatpak)
        ProbeSteamProton(Path.Combine(home, ".var/app/com.valvesoftware.Steam/data/Steam"), runtimes);

        // 4. Поиск в Lutris Wine/Proton runners
        ProbeLutrisRunners(Path.Combine(home, ".local/share/lutris/runners"), runtimes);

        // 5. CachyOS / Системные директории совместимости
        ProbeDirectoryProton("/usr/share/steam/compatibilitytools.d", runtimes);

        return runtimes.DistinctBy(r => r.ExecutablePath).ToList();
    }

    private static void ProbeSystemWine(List<RuntimeEnvironment> list)
    {
        string[] candidates = ["/usr/bin/wine", "/usr/bin/wine-staging", "/usr/local/bin/wine"];
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                list.Add(new RuntimeEnvironment(
                    Name: Path.GetFileName(path),
                                                Type: RuntimeType.Wine,
                                                ExecutablePath: path
                ));
            }
        }
    }

    private static void ProbeSteamProton(string steamRoot, List<RuntimeEnvironment> list)
    {
        if (!Directory.Exists(steamRoot)) return;

        // Custom tools (Proton-GE, Proton-CachyOS)
        string customTools = Path.Combine(steamRoot, "compatibilitytools.d");
        ProbeDirectoryProton(customTools, list);

        // Official Steamapps Proton (Proton 8, 9, Experimental)
        string commonDir = Path.Combine(steamRoot, "steamapps/common");
        if (Directory.Exists(commonDir))
        {
            foreach (var dir in Directory.GetDirectories(commonDir, "Proton*"))
            {
                string protonBin = Path.Combine(dir, "proton");
                if (File.Exists(protonBin))
                {
                    list.Add(new RuntimeEnvironment(
                        Name: Path.GetFileName(dir),
                                                    Type: RuntimeType.Proton,
                                                    ExecutablePath: protonBin
                    ));
                }
            }
        }
    }

    private static void ProbeDirectoryProton(string dir, List<RuntimeEnvironment> list)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var sub in Directory.GetDirectories(dir))
        {
            string protonBin = Path.Combine(sub, "proton");
            if (File.Exists(protonBin))
            {
                list.Add(new RuntimeEnvironment(
                    Name: Path.GetFileName(sub),
                                                Type: RuntimeType.Proton,
                                                ExecutablePath: protonBin
                ));
            }
        }
    }

    private static void ProbeLutrisRunners(string runnersDir, List<RuntimeEnvironment> list)
    {
        string wineRunners = Path.Combine(runnersDir, "wine");
        if (Directory.Exists(wineRunners))
        {
            foreach (var dir in Directory.GetDirectories(wineRunners))
            {
                string bin = Path.Combine(dir, "bin/wine");
                if (File.Exists(bin))
                {
                    list.Add(new RuntimeEnvironment(
                        Name: $"Lutris-{Path.GetFileName(dir)}",
                                                    Type: RuntimeType.Wine,
                                                    ExecutablePath: bin
                    ));
                }
            }
        }
    }
}
