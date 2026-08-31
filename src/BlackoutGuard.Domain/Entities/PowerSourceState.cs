using System;

namespace BlackoutGuard.Domain.Entities;

public enum PowerSourceType
{
    Grid,
    Solar,
    Generator,
    None
}

public sealed record PowerSourceState(
    Guid FacilityId,
    bool IsGridAvailable,        // Grid မီးလာနေသလား (true/false)
    double SolarOutputKw,        // Solar မှ ထွက်နေသော အား (kW)
    double GeneratorOutputKw,    // Generator မှ ထွက်နေသော အား (kW)
    PowerSourceType ActiveSource // လက်ရှိ အသုံးပြုနေသော Source (Grid / Solar / Generator / None)
);