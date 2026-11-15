namespace Game.Sim;

/// <summary>How a resource is stored: which kind of building raises its limit.</summary>
public enum StorageClass
{
    /// <summary>No limit (Sand and Water, which become Capacity resources).</summary>
    Unlimited,

    /// <summary>Solid raw materials: a per-type limit raised by the HQ, Silos, and Stockyards.</summary>
    Bulk,

    /// <summary>Liquids and gases: a per-type limit raised by the HQ and Tank Farms.</summary>
    Fluid,

    /// <summary>Radioactive and precious materials: a per-type limit raised by the HQ and Vaults.</summary>
    Secure,

    /// <summary>Manufactured goods: one shared pool of Cargo, raised by the HQ and Warehouses.</summary>
    Cargo
}

/// <summary>
/// A resource's storage: its class, and for a per-type class what the HQ holds of it
/// (<see cref="HqBase"/>), what one class building adds (<see cref="ClassStep"/>: a
/// Stockyard, Tank Farm, or Vault), and what one Silo set to it adds (<see cref="SiloStep"/>,
/// Bulk only); for a Cargo resource, the <see cref="Cargo"/> one unit takes up in the pool.
/// </summary>
public sealed record ResourceStorage(StorageClass Class, float HqBase, float ClassStep, float SiloStep, float Cargo)
{
    public static ResourceStorage Unlimited { get; } = new(StorageClass.Unlimited, 0f, 0f, 0f, 0f);

    public static ResourceStorage Bulk(float hqBase, float siloStep, float stockyardStep)
    {
        return new ResourceStorage(StorageClass.Bulk, hqBase, stockyardStep, siloStep, 0f);
    }

    public static ResourceStorage Fluid(float tankFarmStep)
    {
        return new ResourceStorage(StorageClass.Fluid, 0f, tankFarmStep, 0f, 0f);
    }

    public static ResourceStorage Secure(float vaultStep)
    {
        return new ResourceStorage(StorageClass.Secure, 0f, vaultStep, 0f, 0f);
    }

    public static ResourceStorage Pooled(float cargo)
    {
        return new ResourceStorage(StorageClass.Cargo, 0f, 0f, 0f, cargo);
    }

    /// <summary>Whether the resource has a limit of its own (Bulk, Fluid, or Secure).</summary>
    public bool IsPerType => Class is StorageClass.Bulk or StorageClass.Fluid or StorageClass.Secure;
}
