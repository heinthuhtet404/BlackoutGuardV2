using BlackoutGuard.Domain.Entities;
using System.Collections.Generic;

namespace BlackoutGuard.Application.Services;

public class PowerManagementService
{
    /// <summary>
    /// Power State နှင့် Loads များပေါ်မူတည်၍ မည်သည့် Load များ Power ပေးမည် (IsActive = true)၊ 
    /// မည်သည့် Load များကို Shed/ပိတ်မည် (IsActive = false) ဆိုသည်ကို တွက်ချက်ပေးသည်။
    /// </summary>
    public (PowerSourceType ActiveSource, List<Load> UpdatedLoads) CalculateLoadState(
        PowerSourceState powerState,
        IEnumerable<Load> currentLoads)
    {
        var loadsList = currentLoads.ToList();

        // -------------------------------------------------------------
        // Case 1: Grid မီးလာနေပါက Loads အားလုံးကို မီးပေးမည် (Green / Active)
        // -------------------------------------------------------------
        if (powerState.IsGridAvailable)
        {
            var gridLoads = loadsList.Select(l => l with { IsActive = true }).ToList();
            return (PowerSourceType.Grid, gridLoads);
        }

        // -------------------------------------------------------------
        // Case 2: Grid မီးပြတ်သွားပါက Solar အား ရှိ/မရှိ စစ်ဆေးမည်
        // -------------------------------------------------------------
        double availableCapacityKw = 0;
        PowerSourceType activeSource = PowerSourceType.None;

        if (powerState.SolarOutputKw > 0)
        {
            activeSource = PowerSourceType.Solar;
            availableCapacityKw = powerState.SolarOutputKw;
        }
        else if (powerState.GeneratorOutputKw > 0)
        {
            // Solar အားမရှိပါက Generator အား ရှိ/မရှိ စစ်ဆေးမည်
            activeSource = PowerSourceType.Generator;
            availableCapacityKw = powerState.GeneratorOutputKw;
        }
        else
        {
            // Source တစ်ခုမှ မရှိပါက Load အားလုံး ပိတ်မည်
            var blackoutLoads = loadsList.Select(l => l with { IsActive = false }).ToList();
            return (PowerSourceType.None, blackoutLoads);
        }

        // -------------------------------------------------------------
        // Case 3: Solar သို့မဟုတ် Generator သုံးချိန် Load Shedding Logic တွက်ချက်ခြင်း
        // Priority အလိုက် (P1 -> P2 -> P3) စီပြီး အားလောက်သလောက် မီးပေးမည်
        // -------------------------------------------------------------
        var resultLoads = new List<Load>();
        double usedPowerKw = 0;

        // CriticalityScore သို့မဟုတ် Priority အလိုက် အစဉ်လိုက် စီစဉ်ခြင်း
        var sortedLoads = loadsList
            .OrderByDescending(l => l.CriticalityScore)
            .ThenBy(l => GetPriorityRank(l.Priority))
            .ToList();

        foreach (var load in sortedLoads)
        {
            // Available Capacity ထက် မပိုပါက မီးပေးမည် (IsActive = true)
            if (usedPowerKw + load.PowerRatingKw <= availableCapacityKw)
            {
                usedPowerKw += load.PowerRatingKw;
                resultLoads.Add(load with { IsActive = true });
            }
            else
            {
                // အားမလောက်ပါက မီးဖြတ်မည် (Load Shedding: IsActive = false)
                resultLoads.Add(load with { IsActive = false });
            }
        }

        return (activeSource, resultLoads);
    }

    private static int GetPriorityRank(string priority)
    {
        return priority?.ToUpper() switch
        {
            "CRITICAL" => 1,
            "P1" => 1,
            "ESSENTIAL" => 2,
            "P2" => 2,
            "NON-ESSENTIAL" => 3,
            "P3" => 3,
            _ => 99
        };
    }
}