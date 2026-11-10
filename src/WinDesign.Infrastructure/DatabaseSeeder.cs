using Microsoft.EntityFrameworkCore;
using WinDesign.Domain;
using WinDesign.Domain.Entities;

namespace WinDesign.Infrastructure;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(WinDesignDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var permissionDefs = new (string Code, string Name, string Module)[]
        {
            ("users.read", "查看用户", "用户管理"),
            ("users.write", "维护用户", "用户管理"),
            ("projects.read", "查看项目", "项目管理"),
            ("projects.write", "维护项目", "项目管理"),
            ("dictionaries.read", "查看编码定义", "编码定义"),
            ("dictionaries.write", "维护编码定义", "编码定义"),
            ("pbs.read", "查看 PBS", "PBS管理"),
            ("pbs.write", "维护 PBS", "PBS管理"),
            ("equipment.read", "查看设备索引", "设备索引"),
            ("equipment.write", "维护设备索引", "设备索引"),
            ("specs.read", "查看设备规格", "设备规格"),
            ("specs.write", "维护设备规格", "设备规格"),
            ("materials.read", "查看材料附件", "材料附件"),
            ("materials.write", "维护材料附件", "材料附件"),
            ("reports.read", "查看报表", "报表输出")
        };

        var permissions = permissionDefs.Select(p => new Permission
        {
            Code = p.Code,
            Name = p.Name,
            Module = p.Module
        }).ToList();
        db.Permissions.AddRange(permissions);

        var adminRole = new Role { Code = "Admin", Name = "系统管理员", Description = "全部权限", IsSystem = true };
        var designerRole = new Role { Code = "Designer", Name = "设计人员", Description = "项目与设计数据维护", IsSystem = true };
        var viewerRole = new Role { Code = "Viewer", Name = "只读用户", Description = "只读访问", IsSystem = true };
        db.Roles.AddRange(adminRole, designerRole, viewerRole);

        var designerCodes = new HashSet<string>
        {
            "projects.read", "projects.write",
            "dictionaries.read",
            "pbs.read", "pbs.write",
            "equipment.read", "equipment.write",
            "specs.read", "specs.write",
            "materials.read", "materials.write",
            "reports.read"
        };
        foreach (var permission in permissions)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permission.Id });
            if (designerCodes.Contains(permission.Code))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = designerRole.Id, PermissionId = permission.Id });
            }
            if (permission.Code.EndsWith(".read", StringComparison.Ordinal))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = viewerRole.Id, PermissionId = permission.Id });
            }
        }

        var admin = new User
        {
            UserName = "admin",
            DisplayName = "系统管理员",
            Email = "admin@windesign.local",
            PasswordHash = PasswordHasher.Hash("admin123"),
            IsSystemAdmin = true
        };
        var designer = new User
        {
            UserName = "designer",
            DisplayName = "设计工程师",
            Email = "designer@windesign.local",
            PasswordHash = PasswordHasher.Hash("designer123")
        };
        var viewer = new User
        {
            UserName = "viewer",
            DisplayName = "只读用户",
            Email = "viewer@windesign.local",
            PasswordHash = PasswordHasher.Hash("viewer123")
        };
        db.Users.AddRange(admin, designer, viewer);
        db.UserRoles.AddRange(
            new UserRole { UserId = admin.Id, RoleId = adminRole.Id },
            new UserRole { UserId = designer.Id, RoleId = designerRole.Id },
            new UserRole { UserId = viewer.Id, RoleId = viewerRole.Id });

        // 工厂（PBS 根节点）；一个工厂可以承载多个项目。
        var plantA = new Plant
        {
            Code = "PLANT-A",
            Name = "某石化厂",
            ClientName = "示范客户",
            Location = "华东",
            Description = "演示工厂：包含常减压等装置。"
        };
        var plantB = new Plant
        {
            Code = "PLANT-B",
            Name = "某 LNG 接收站",
            ClientName = "示范客户",
            Location = "华南",
            Description = "演示工厂：电信及 IT 设计。"
        };
        db.Plants.AddRange(plantA, plantB);

        var project1 = new Project
        {
            PlantId = plantA.Id,
            Code = "DEMO-001",
            Name = "某石化仪表改造项目",
            ClientName = "示范客户",
            Phase = "详细设计",
            Status = ProjectStatus.Active,
            OwnerUserId = admin.Id,
            Description = "演示项目：仪表、电信与 IT 一体化设计。"
        };
        var project2 = new Project
        {
            PlantId = plantA.Id,
            Code = "DEMO-002",
            Name = "常减压装置扩能改造",
            ClientName = "示范客户",
            Phase = "基础设计",
            Status = ProjectStatus.Active,
            OwnerUserId = designer.Id,
            Description = "同一工厂下的第二个项目，与 DEMO-001 共享设备。"
        };
        var project3 = new Project
        {
            PlantId = plantB.Id,
            Code = "DEMO-003",
            Name = "LNG 接收站电信设计",
            ClientName = "示范客户",
            Phase = "基础设计",
            Status = ProjectStatus.Active,
            OwnerUserId = designer.Id,
            Description = "演示项目：电信及 IT 设计。"
        };
        db.Projects.AddRange(project1, project2, project3);
        db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = project1.Id, UserId = admin.Id, RoleInProject = "Owner" },
            new ProjectMember { ProjectId = project1.Id, UserId = designer.Id, RoleInProject = "Designer" },
            new ProjectMember { ProjectId = project1.Id, UserId = viewer.Id, RoleInProject = "Viewer" },
            new ProjectMember { ProjectId = project2.Id, UserId = designer.Id, RoleInProject = "Owner" },
            new ProjectMember { ProjectId = project3.Id, UserId = designer.Id, RoleInProject = "Owner" });

        // PBS 节点归工厂；根节点由工厂本身承载，不再单独建 Root 节点。
        var area = new PbsNode { PlantId = plantA.Id, Code = "AREA-A", Name = "A 区", NodeType = PbsNodeType.Area, SortOrder = 1 };
        var unit = new PbsNode { PlantId = plantA.Id, ParentId = area.Id, Code = "CDU", Name = "常减压装置", NodeType = PbsNodeType.Unit, SortOrder = 2 };
        var loop = new PbsNode { PlantId = plantA.Id, ParentId = unit.Id, Code = "L-1001", Name = "压力控制回路", NodeType = PbsNodeType.Loop, SortOrder = 3 };
        var areaB = new PbsNode { PlantId = plantB.Id, Code = "AREA-LNG", Name = "接收站区", NodeType = PbsNodeType.Area, SortOrder = 1 };
        db.PbsNodes.AddRange(area, unit, loop, areaB);

        // 项目挂在工厂的顶层 PBS 节点上；DEMO-001 / DEMO-002 共用 A 区，DEMO-003 使用接收站区。
        project1.PbsRootNodeId = area.Id;
        project2.PbsRootNodeId = area.Id;
        project3.PbsRootNodeId = areaB.Id;

        var equipment = new Equipment
        {
            PlantId = plantA.Id,
            PbsNodeId = loop.Id,
            TagNo = "PT-1001",
            LoopNo = "L-1001",
            Name = "压力变送器",
            TypeCode = "PT",
            Discipline = Discipline.Instrument,
            Location = "A 区 / 常减压装置"
        };
        db.Equipments.Add(equipment);

        // 同一台设备同时纳入两个项目的设计范围。
        db.ProjectEquipments.AddRange(
            new ProjectEquipment { ProjectId = project1.Id, EquipmentId = equipment.Id },
            new ProjectEquipment { ProjectId = project2.Id, EquipmentId = equipment.Id });

        db.EquipmentSpecs.Add(new EquipmentSpec
        {
            EquipmentId = equipment.Id,
            SpecCode = "SPEC-PT-1001",
            SpecName = "压力变送器规格书",
            ContentJson = "{\"range\":\"0~1.6MPa\",\"output\":\"4~20mA\",\"accuracy\":\"0.075%\"}"
        });
        db.MaterialItems.Add(new MaterialItem
        {
            ProjectId = project1.Id,
            EquipmentId = equipment.Id,
            Code = "MAT-001",
            Name = "不锈钢管",
            Spec = "Φ14×2",
            Material = "316SS",
            Unit = "米",
            Quantity = 120
        });

        var dictionaryDefs = new (string Code, string Name, (string Code, string Name)[] Items)[]
        {
            ("DISCIPLINE", "专业", new[] { ("INS", "仪表"), ("TEL", "电信"), ("IT", "IT") }),
            ("EQUIPMENT_TYPE", "设备类型", new[]
            {
                ("PT", "压力变送器"), ("TT", "温度变送器"), ("FT", "流量计"),
                ("LV", "调节阀"), ("LT", "液位计"), ("AT", "分析仪")
            }),
            ("MATERIAL_CATEGORY", "材料分类", new[]
            {
                ("CABLE", "电缆"), ("CONDUIT", "穿管"), ("JB", "接线箱"),
                ("TERMINAL", "端子"), ("STEEL", "钢材"), ("VALVE", "阀门")
            }),
            ("PBS_LEVEL", "PBS 层级", new[]
            {
                ("AREA", "区域"), ("UNIT", "装置"), ("SYSTEM", "系统"), ("SUBSYSTEM", "子系统")
            }),
            ("DOC_STATUS", "文档状态", new[]
            {
                ("DRAFT", "草稿"), ("DESIGN", "设计中"), ("CHECK", "校核"),
                ("REVIEW", "审核"), ("APPROVED", "批准")
            }),
            ("UNIT", "计量单位", new[]
            {
                ("PCS", "个"), ("SET", "台"), ("KIT", "套"), ("M", "米"), ("PCS2", "根"), ("BATCH", "批")
            })
        };

        var sort = 0;
        foreach (var (code, name, items) in dictionaryDefs)
        {
            var type = new DictionaryType
            {
                Code = code,
                Name = name,
                IsSystem = true,
                SortOrder = ++sort
            };
            db.DictionaryTypes.Add(type);
            var itemSort = 0;
            foreach (var (itemCode, itemName) in items)
            {
                db.DictionaryItems.Add(new DictionaryItem
                {
                    TypeId = type.Id,
                    Code = itemCode,
                    Name = itemName,
                    SortOrder = ++itemSort
                });
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = admin.Id,
            UserName = admin.UserName,
            Action = "Seed",
            EntityType = "System",
            Detail = "初始化系统角色、用户、演示项目与编码字典"
        });

        await db.SaveChangesAsync(ct);
    }
}
