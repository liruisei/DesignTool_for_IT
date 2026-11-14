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
/// 设备样本类型。目前设计端电信统计只消费 Access / PoE，
/// 机柜与其他设备作为样本库中的通用设备维护。
/// </summary>
public enum SwitchType
{
    Access = 0,
    PoE = 1,
    Cabinet = 2,
    Other = 3
}
