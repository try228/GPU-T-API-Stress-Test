using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Manages hardware performance queries, profiling locks, and silicon counter decoding via VK_KHR_performance_query.
/// Fully compliant with Vulkan specification requirements and NativeAOT.
/// </summary>
public sealed unsafe class VulkanPerfQueryManager : IDisposable
{
    private delegate* unmanaged[Cdecl]<PhysicalDevice, uint, uint*, PerformanceCounterKHR*, PerformanceCounterDescriptionKHR*, Result> _pfnEnumerate;
    private delegate* unmanaged[Cdecl]<PhysicalDevice, QueryPoolPerformanceCreateInfoKHR*, uint*, void> _pfnGetPasses;
    private delegate* unmanaged[Cdecl]<Device, AcquireProfilingLockInfoKHR*, Result> _pfnAcquireLock;
    private delegate* unmanaged[Cdecl]<Device, void> _pfnReleaseLock;

    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly uint _queueFamilyIndex;
    private readonly uint _querySlotsCount;

    private QueryPool _queryPool = default;
    private uint _selectedCounterIndex = uint.MaxValue;
    private PerformanceCounterStorageKHR _counterStorage;
    private PerformanceCounterUnitKHR _counterUnit;

    private uint _totalHardwareCounters = 0;
    private uint _requiredPasses = 1;
    private bool _lockAcquired = false;
    private double _smoothedValue = 0.0;
    private readonly object _stateLock = new();

    public bool IsSupported => _queryPool.Handle != 0;
    public bool IsLockAcquired => _lockAcquired;
    public string SelectedCounterName { get; private set; } = "None";
    public uint TotalHardwareCounters => _totalHardwareCounters;
    public string FormattedCounterValue { get; private set; } = "N/A (Pipeline Math Fallback)";
    public QueryPool QueryPool => _queryPool;

    public VulkanPerfQueryManager(
        Vk vk,
        Instance instance,
        Device device,
        PhysicalDevice physicalDevice,
        uint queueFamilyIndex,
        uint querySlotsCount = 1)
    {
        _vk = vk;
        _instance = instance;
        _device = device;
        _physicalDevice = physicalDevice;
        _queueFamilyIndex = queueFamilyIndex;
        _querySlotsCount = Math.Max(1, querySlotsCount);

        Initialize();
    }

    private void Initialize()
    {
        nint pEnum = GetProc("vkEnumeratePhysicalDeviceQueueFamilyPerformanceQueryCountersKHR");
        nint pPasses = GetProc("vkGetPhysicalDeviceQueueFamilyPerformanceQueryPassesKHR");
        nint pAcq = GetProc("vkAcquireProfilingLockKHR");
        nint pRel = GetProc("vkReleaseProfilingLockKHR");

        if (pEnum == nint.Zero || pAcq == nint.Zero || pRel == nint.Zero)
        {
            FormattedCounterValue = "N/A (VK_KHR_performance_query procs missing)";
            return;
        }

        _pfnEnumerate = (delegate* unmanaged[Cdecl]<PhysicalDevice, uint, uint*, PerformanceCounterKHR*, PerformanceCounterDescriptionKHR*, Result>)pEnum;
        if (pPasses != nint.Zero) _pfnGetPasses = (delegate* unmanaged[Cdecl]<PhysicalDevice, QueryPoolPerformanceCreateInfoKHR*, uint*, void>)pPasses;
        _pfnAcquireLock = (delegate* unmanaged[Cdecl]<Device, AcquireProfilingLockInfoKHR*, Result>)pAcq;
        _pfnReleaseLock = (delegate* unmanaged[Cdecl]<Device, void>)pRel;

        try
        {
            uint counterCount = 0;
            Result countRes = _pfnEnumerate(_physicalDevice, _queueFamilyIndex, &counterCount, null, null);
            if (countRes != Result.Success || counterCount == 0)
            {
                FormattedCounterValue = "N/A (0 hardware counters reported by driver)";
                return;
            }

            _totalHardwareCounters = counterCount;
            PerformanceCounterKHR* pCounters = stackalloc PerformanceCounterKHR[(int)counterCount];
            PerformanceCounterDescriptionKHR* pDescs = stackalloc PerformanceCounterDescriptionKHR[(int)counterCount];

            Unsafe.InitBlock(pCounters, 0, (uint)(sizeof(PerformanceCounterKHR) * counterCount));
            Unsafe.InitBlock(pDescs, 0, (uint)(sizeof(PerformanceCounterDescriptionKHR) * counterCount));

            for (int i = 0; i < (int)counterCount; i++)
            {
                pCounters[i].SType = VulkanConstants.StructureTypePerformanceCounterKHR;
                pCounters[i].PNext = null;
                pDescs[i].SType = VulkanConstants.StructureTypePerformanceCounterDescriptionKHR;
                pDescs[i].PNext = null;
            }

            _pfnEnumerate(_physicalDevice, _queueFamilyIndex, &counterCount, pCounters, pDescs);

            uint targetIdx = 0;
            for (uint i = 0; i < counterCount; i++)
            {
                string name = Marshal.PtrToStringAnsi((nint)pDescs[i].Name)?.ToLowerInvariant() ?? "";
                if (name.Contains("valu") || name.Contains("alu") || name.Contains("busy") || 
                    name.Contains("active") || name.Contains("utilization") || name.Contains("compute"))
                {
                    targetIdx = i;
                    break;
                }
            }

            _selectedCounterIndex = targetIdx;
            _counterStorage = pCounters[targetIdx].Storage;
            _counterUnit = pCounters[targetIdx].Unit;
            SelectedCounterName = Marshal.PtrToStringAnsi((nint)pDescs[targetIdx].Name) ?? "GPU Silicon Counter";

            uint cIdx = _selectedCounterIndex;
            QueryPoolPerformanceCreateInfoKHR perfPoolInfo = new()
            {
                SType = VulkanConstants.StructureTypeQueryPoolPerformanceCreateInfoKHR,
                PNext = null,
                QueueFamilyIndex = _queueFamilyIndex,
                CounterIndexCount = 1,
                PCounterIndices = &cIdx
            };

            if (_pfnGetPasses != null)
            {
                uint passes = 1;
                _pfnGetPasses(_physicalDevice, &perfPoolInfo, &passes);
                _requiredPasses = Math.Max(1, passes);
            }

            QueryPoolCreateInfo qpInfo = new()
            {
                SType = StructureType.QueryPoolCreateInfo,
                PNext = &perfPoolInfo,
                QueryType = QueryType.PerformanceQueryKhr,
                QueryCount = _querySlotsCount
            };

            QueryPool qp;
            Result qpRes = _vk.CreateQueryPool(_device, &qpInfo, null, &qp);
            if (qpRes == Result.Success)
            {
                _queryPool = qp;
                FormattedCounterValue = $"[{SelectedCounterName}] Ready (Standby Mode)";
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[PerfQuery] Bound silicon counter: [{SelectedCounterName}] (Passes: {_requiredPasses}, Total Counters: {_totalHardwareCounters})");
                Console.ResetColor();
            }
            else
            {
                FormattedCounterValue = "N/A (QueryPool creation rejected)";
            }
        }
        catch (Exception ex)
        {
            FormattedCounterValue = $"N/A ({ex.Message})";
        }
    }

    private nint GetProc(string name)
    {
        nint strPtr = SilkMarshal.StringToPtr(name);
        nint fnPtr = _vk.GetInstanceProcAddr(_instance, (byte*)strPtr);
        SilkMarshal.Free(strPtr);
        return fnPtr;
    }

    public bool AcquireLock()
    {
        lock (_stateLock)
        {
            if (_pfnAcquireLock == null || _lockAcquired) return _lockAcquired;
            try
            {
                AcquireProfilingLockInfoKHR lockInfo = new()
                {
                    SType = VulkanConstants.StructureTypeAcquireProfilingLockInfoKHR,
                    PNext = null,
                    Timeout = 1_000_000_000
                };
                Result res = _pfnAcquireLock(_device, &lockInfo);
                if (res == Result.Success)
                {
                    _lockAcquired = true;
                    _smoothedValue = 0.0;
                    return true;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[PerfQuery] Note: vkAcquireProfilingLockKHR returned {res}.");
                    Console.ResetColor();
                }
            }
            catch { }
            return false;
        }
    }

    public void ReleaseLock()
    {
        lock (_stateLock)
        {
            if (_pfnReleaseLock == null || !_lockAcquired) return;
            try
            {
                _pfnReleaseLock(_device);
                _lockAcquired = false;
                FormattedCounterValue = $"[{SelectedCounterName}] Ready (Standby Mode)";
            }
            catch { }
        }
    }

    public void FetchResults(uint slotIndex = 0)
    {
        if (_queryPool.Handle == 0 || !_lockAcquired) return;

        PerformanceCounterResultKHR result = default;
        Result res = _vk.GetQueryPoolResults(
            _device,
            _queryPool,
            slotIndex % _querySlotsCount,
            1,
            (nuint)sizeof(PerformanceCounterResultKHR),
            &result,
            (ulong)sizeof(PerformanceCounterResultKHR),
            QueryResultFlags.None);

        if (res == Result.Success)
        {
            double rawVal = _counterStorage switch
            {
                PerformanceCounterStorageKHR.Float32Khr => result.Float32,
                PerformanceCounterStorageKHR.Float64Khr => result.Float64,
                PerformanceCounterStorageKHR.Uint32Khr  => result.Uint32,
                PerformanceCounterStorageKHR.Uint64Khr  => result.Uint64,
                PerformanceCounterStorageKHR.Int32Khr   => result.Int32,
                PerformanceCounterStorageKHR.Int64Khr   => result.Int64,
                _ => 0.0
            };

            if (rawVal > 0.0001)
            {
                _smoothedValue = (_smoothedValue == 0.0) ? rawVal : (_smoothedValue * 0.85 + rawVal * 0.15);
            }

            string unitStr = _counterUnit switch
            {
                PerformanceCounterUnitKHR.PercentageKhr => "%",
                PerformanceCounterUnitKHR.HertzKhr => " Hz",
                PerformanceCounterUnitKHR.BytesPerSecondKhr => " B/s",
                PerformanceCounterUnitKHR.CyclesKhr => " cycles",
                PerformanceCounterUnitKHR.NanosecondsKhr => " ns",
                PerformanceCounterUnitKHR.WattsKhr => " W",
                _ => ""
            };

            FormattedCounterValue = $"{SelectedCounterName}: {_smoothedValue:F1}{unitStr} [Silicon Active]";
        }
    }

    public void Dispose()
    {
        ReleaseLock();
        if (_queryPool.Handle != 0)
        {
            _vk.DestroyQueryPool(_device, _queryPool, null);
            _queryPool = default;
        }
    }
}