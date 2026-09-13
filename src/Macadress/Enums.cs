namespace Macadress;

/// <summary>
/// The IEEE registry an assignment comes from. MA-L is a /24 (the classic
/// OUI), MA-M a /28, MA-S a /36; IAB and CID are legacy blocks. An
/// unrecognised value from a newer API version is kept as-is rather than
/// rejected (see <see cref="Unknown"/>).
/// </summary>
public enum BlockType
{
    MAL,
    MAM,
    MAS,
    IAB,
    CID,
    Unknown,
}

/// <summary>Classifies the destination: unicast, multicast or broadcast.</summary>
public enum TransmissionType
{
    Unicast,
    Multicast,
    Broadcast,
}

/// <summary>Whether the address is universally (IEEE-assigned) or locally administered.</summary>
public enum AdministrationType
{
    UniversallyAdministered,
    LocallyAdministered,
}

/// <summary>How strongly the address looks like an OS privacy-randomized MAC.</summary>
public enum RandomizationConfidence
{
    None,
    Possible,
    Likely,
}

/// <summary>
/// The controlled device taxonomy from the <c>device.category</c> field.
/// <see cref="Unknown"/> is by far the most common value.
/// </summary>
public enum DeviceCategory
{
    Computer,
    Smartphone,
    Tablet,
    Router,
    Switch,
    WirelessAccessPoint,
    Firewall,
    Server,
    Storage,
    Printer,
    Camera,
    SmartTv,
    MediaDevice,
    GamingConsole,
    Iot,
    EmbeddedDevice,
    Industrial,
    Medical,
    Automotive,
    VirtualMachine,
    Container,
    NetworkInterface,
    ConsumerElectronics,
    Unknown,
}
