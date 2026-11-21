namespace WinDesign.Domain;

public enum ProjectStatus
{
    Active = 0,
    Archived = 1,
    Completed = 2
}

public enum Discipline
{
    Instrument = 0,
    Telecom = 1,
    IT = 2
}

public enum PbsNodeType
{
    Root = 0,
    Area = 1,
    Unit = 2,
    System = 3,
    SubSystem = 4,
    Equipment = 5,
    Loop = 6,
    Building = 7
}

/// <summary>
/// 旧版设备样本类别，仅用于兼容既有数据库列。
/// 新逻辑改用「专业 + 设备类型编码」，设计端不再按该枚举过滤样本。
/// </summary>
public enum SwitchType
{
    Access = 0,
    PoE = 1,
    Cabinet = 2,
    Other = 3
}
