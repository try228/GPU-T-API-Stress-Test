using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace GpuT.Agent.Native.Vulkan;

public sealed unsafe class VulkanPerfQueryManager : IDisposable
{
    public delegate Result PfnEnumerateCounters(
        PhysicalDevice physicalDevice,
        uint queueFamilyIndex,
        uint* pCounterCount,
        PerformanceCounterKHR* pCounters,
        PerformanceCounterDescriptionKHR* pCounterDescriptions);

    public delegate Result PfnAcquireProfilingLock(
        Device device,
        AcquireProfilingLockInfoKHR* pInfo);

    public delegate void PfnReleaseProfilingLock(
        Device device);

    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly uint _queueFamilyIndex;
    private readonly uint _querySlotsCount;
    private readonly bool _deviceExtensionActive;

    private PfnEnumerateCounters? _pfnEnumerate;
    private PfnAcquireProfilingLock? _pfnAcquireLock;
    private PfnReleaseProfilingLock? _pfnReleaseLock;

    private bool _lockAcquired;
    private QueryPool _queryPool = default;
    private uint _selectedCounterIndex = uint.MaxValue;
    private PerformanceCounterStorageKHR _counterStorage;
    private PerformanceCounterUnitKHR _counterUnit;

    private double _smoothedValue = 0.0;

    public bool IsSupported => _deviceExtensionActive && _pfnEnumerate != null;
    public bool IsQueryPoolReady => _queryPool.Handle != 0;
    public QueryPool QueryPoolHandle => _queryPool;
    public string SelectedCounterName { get; private set; } = "None";
    public string FormattedCounterValue { get; private set; } = "N/A (Pipeline Math Fallback)";

    public VulkanPerfQueryManager(Vk vk, Instance instance, Device device, PhysicalDevice physicalDevice, uint queueFamilyIndex, uint querySlotsCount, bool deviceExtensionActive)
    {
        _vk = vk;
        _instance = instance;
        _device = device;
        _physicalDevice = physicalDevice;
        _queueFamilyIndex = queueFamilyIndex;
        _querySlotsCount = Math.Max(1, querySlotsCount);
        _deviceExtensionActive = deviceExtensionActive;

        Initialize();
    }

    private void Initialize()
    {
        // Если устройство не поддерживает расширение (iGPU), сразу выходим в безопасный fallback
        if (!_deviceExtensionActive)
        {
            FormattedCounterValue = "N/A (Pipeline Math Fallback)";
            return;
        }

        nint pEnum = GetProc("vkEnumeratePhysicalDeviceQueueFamilyPerformanceQueryCountersKHR");
        nint pAcq = GetProc("vkAcquireProfilingLockKHR");
        nint pRel = GetProc("vkReleaseProfilingLockKHR");

        if (pEnum == nint.Zero)
        {
            FormattedCounterValue = "N/A (Pipeline Math Fallback)";
            return;
        }

        _pfnEnumerate = Marshal.GetDelegateForFunctionPointer<PfnEnumerateCounters>(pEnum);
        if (pAcq != nint.Zero) _pfnAcquireLock = Marshal.GetDelegateForFunctionPointer<PfnAcquireProfilingLock>(pAcq);
        if (pRel != nint.Zero) _pfnReleaseLock = Marshal.GetDelegateForFunctionPointer<PfnReleaseProfilingLock>(pRel);

        try
        {
            uint counterCount = 0;
            Result countRes = _pfnEnumerate(_physicalDevice, _queueFamilyIndex, &counterCount, null, null);
            if (countRes != Result.Success || counterCount == 0)
            {
                FormattedCounterValue = "N/A (0 Counters on GPU)";
                return;
            }

            PerformanceCounterKHR* pCounters = stackalloc PerformanceCounterKHR[(int)counterCount];
            PerformanceCounterDescriptionKHR* pDescs = stackalloc PerformanceCounterDescriptionKHR[(int)counterCount];

            _pfnEnumerate(_physicalDevice, _queueFamilyIndex, &counterCount, pCounters, pDescs);

            uint targetIdx = 0;
            for (uint i = 0; i < counterCount; i++)
            {
                string name = Marshal.PtrToStringAnsi((nint)pDescs[i].Name)?.ToLowerInvariant() ?? "";
                if (name.Contains("valu") || name.Contains("alu") || name.Contains("busy") || name.Contains("utilization") || name.Contains("active"))
                {
                    targetIdx = i;
                    break;
                }
            }

            _selectedCounterIndex = targetIdx;
            _counterStorage = pCounters[targetIdx].Storage;
            _counterUnit = pCounters[targetIdx].Unit;
            SelectedCounterName = Marshal.PtrToStringAnsi((nint)pDescs[targetIdx].Name) ?? "HW Counter";

            uint cIdx = _selectedCounterIndex;
            QueryPoolPerformanceCreateInfoKHR perfPoolInfo = new()
            {
                SType = StructureType.QueryPoolPerformanceCreateInfoKhr,
                QueueFamilyIndex = _queueFamilyIndex,
                CounterIndexCount = 1,
                PCounterIndices = &cIdx
            };

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
                FormattedCounterValue = $"{SelectedCounterName}: Ready";
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[PerfQuery] Multi-slot QueryPool created for [{SelectedCounterName}]");
                Console.ResetColor();
            }
            else
            {
                FormattedCounterValue = "N/A (Pipeline Math Fallback)";
            }
        }
        catch (Exception ex)
        {
            FormattedCounterValue = "N/A (Pipeline Math Fallback)";
            Console.WriteLine($"[PerfQuery] iGPU Notice: {ex.Message}");
        }
    }

    private nint GetProc(string name)
    {
        nint strPtr = SilkMarshal.StringToPtr(name);
        nint fnPtr = _vk.GetInstanceProcAddr(_instance, (byte*)strPtr);
        SilkMarshal.Free(strPtr);
        return fnPtr;
    }

    public void AcquireLock()
    {
        // Защита: не вызываем блокировку, если QueryPool не активен (iGPU)
        if (!IsQueryPoolReady || _pfnAcquireLock == null || _lockAcquired) return;
        try
        {
            AcquireProfilingLockInfoKHR lockInfo = new()
            {
                SType = StructureType.AcquireProfilingLockInfoKhr,
                Timeout = 2_000_000_000
            };
            if (_pfnAcquireLock(_device, &lockInfo) == Result.Success)
            {
                _lockAcquired = true;
                _smoothedValue = 0.0;
            }
        }
        catch { }
    }

    public void ReleaseLock()
    {
        if (!IsQueryPoolReady || _pfnReleaseLock == null || !_lockAcquired) return;
        try
        {
            _pfnReleaseLock(_device);
            _lockAcquired = false;
            _smoothedValue = 0.0;
        }
        catch { }
    }

    public void FetchResults(uint slotIndex)
    {
        if (!IsQueryPoolReady) return;

        PerformanceCounterResultKHR result = default;
        Result res = _vk.GetQueryPoolResults(
            _device,
            _queryPool,
            slotIndex,
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

            if (rawVal > 0.01)
            {
                _smoothedValue = (_smoothedValue == 0.0) ? rawVal : (_smoothedValue * 0.8 + rawVal * 0.2);
            }

            string unitStr = _counterUnit switch
            {
                PerformanceCounterUnitKHR.PercentageKhr => "%",
                PerformanceCounterUnitKHR.HertzKhr => " Hz",
                PerformanceCounterUnitKHR.BytesPerSecondKhr => " B/s",
                PerformanceCounterUnitKHR.CyclesKhr => " cycles",
                _ => ""
            };

            FormattedCounterValue = $"{SelectedCounterName} = {_smoothedValue:F1}{unitStr} (Direct Silicon)";
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