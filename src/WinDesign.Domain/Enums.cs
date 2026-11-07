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
    Equipment = 5
}

public enum EquipmentStatus
{
    Planned = 0,
    Designed = 1,
    Approved = 2
}
