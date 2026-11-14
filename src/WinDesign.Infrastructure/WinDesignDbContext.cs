using Microsoft.EntityFrameworkCore;
using WinDesign.Domain.Entities;

namespace WinDesign.Infrastructure;

public class WinDesignDbContext : DbContext
{
    public WinDesignDbContext(DbContextOptions<WinDesignDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectEquipment> ProjectEquipments => Set<ProjectEquipment>();
    public DbSet<PbsNode> PbsNodes => Set<PbsNode>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<EquipmentSpec> EquipmentSpecs => Set<EquipmentSpec>();
    public DbSet<MaterialItem> MaterialItems => Set<MaterialItem>();
    public DbSet<SwitchModel> SwitchModels => Set<SwitchModel>();
    public DbSet<TelecomRoom> TelecomRooms => Set<TelecomRoom>();
    public DbSet<NumberingRule> NumberingRules => Set<NumberingRule>();
    public DbSet<DictionaryType> DictionaryTypes => Set<DictionaryType>();
    public DbSet<DictionaryItem> DictionaryItems => Set<DictionaryItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(64).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
            e.Property(x => x.Email).HasMaxLength(128);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(128).IsRequired();
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.Module).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<Plant>(e =>
        {
            e.ToTable("plants");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.ClientName).HasMaxLength(200);
            e.Property(x => x.Location).HasMaxLength(200);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.HasIndex(x => new { x.PlantId, x.Code }).IsUnique();
            e.HasOne(x => x.Plant).WithMany(x => x.Projects).HasForeignKey(x => x.PlantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.ClientName).HasMaxLength(200);
            e.Property(x => x.Location).HasMaxLength(200);
            e.Property(x => x.Phase).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            // 项目根节点是工厂顶层 PBS 节点；被项目引用时禁止删除该节点。
            e.HasOne(x => x.PbsRootNode).WithMany().HasForeignKey(x => x.PbsRootNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectMember>(e =>
        {
            e.ToTable("project_members");
            e.HasKey(x => new { x.ProjectId, x.UserId });
            e.HasOne(x => x.Project).WithMany(x => x.Members).HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.Property(x => x.RoleInProject).HasMaxLength(64);
        });

        modelBuilder.Entity<ProjectEquipment>(e =>
        {
            e.ToTable("project_equipments");
            e.HasKey(x => new { x.ProjectId, x.EquipmentId });
            e.HasOne(x => x.Project).WithMany(x => x.ProjectEquipments).HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.Equipment).WithMany(x => x.ProjectEquipments).HasForeignKey(x => x.EquipmentId);
        });

        modelBuilder.Entity<PbsNode>(e =>
        {
            e.ToTable("pbs_nodes");
            e.HasIndex(x => new { x.PlantId, x.Code });
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.NodeType).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Plant).WithMany(x => x.PbsNodes).HasForeignKey(x => x.PlantId);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Equipment>(e =>
        {
            e.ToTable("equipments");
            e.HasIndex(x => new { x.PlantId, x.TagNo }).IsUnique();
            e.Property(x => x.TagNo).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TypeCode).HasMaxLength(64);
            e.Property(x => x.LoopNo).HasMaxLength(64);
            e.Property(x => x.PipeEquipmentNo).HasMaxLength(120);
            e.Property(x => x.Purpose).HasMaxLength(120);
            e.Property(x => x.PidDrawingNo).HasMaxLength(120);
            e.Property(x => x.SpecNo).HasMaxLength(120);
            e.Property(x => x.SpecPageNo).HasMaxLength(32);
            e.Property(x => x.LoopDrawingNo).HasMaxLength(120);
            e.Property(x => x.InstallDrawingNo).HasMaxLength(120);
            e.Property(x => x.ElectricalDrawingNo).HasMaxLength(120);
            e.Property(x => x.HeatSupplyDrawingNo).HasMaxLength(120);
            e.Property(x => x.AirSupplyDrawingNo).HasMaxLength(120);
            e.Property(x => x.LayingDrawingNo).HasMaxLength(120);
            e.Property(x => x.JunctionBoxDrawingNo).HasMaxLength(120);
            e.Property(x => x.Keyword).HasMaxLength(64);
            e.Property(x => x.IoSetting).HasMaxLength(64);
            e.Property(x => x.IndexNo).HasMaxLength(64);
            e.Property(x => x.Discipline).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Plant).WithMany(x => x.Equipments).HasForeignKey(x => x.PlantId);
            e.HasOne(x => x.PbsNode).WithMany().HasForeignKey(x => x.PbsNodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EquipmentSpec>(e =>
        {
            e.ToTable("equipment_specs");
            e.HasIndex(x => new { x.EquipmentId, x.SpecCode });
            e.Property(x => x.SpecCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.SpecName).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Equipment).WithMany(x => x.Specs).HasForeignKey(x => x.EquipmentId);
        });

        modelBuilder.Entity<MaterialItem>(e =>
        {
            e.ToTable("material_items");
            e.HasIndex(x => new { x.ProjectId, x.Code });
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.Equipment).WithMany(x => x.Materials).HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SwitchModel>(e =>
        {
            e.ToTable("switch_models");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.SwitchType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Remark).HasMaxLength(500);
        });

        modelBuilder.Entity<TelecomRoom>(e =>
        {
            e.ToTable("telecom_rooms");
            e.HasIndex(x => new { x.ProjectId, x.PbsNodeId, x.Floor, x.Code });
            e.Property(x => x.Floor).HasMaxLength(64).IsRequired();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Remark).HasMaxLength(500);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PbsNode).WithMany().HasForeignKey(x => x.PbsNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NumberingRule>(e =>
        {
            e.ToTable("numbering_rules");
            e.HasIndex(x => x.ProjectId).IsUnique();
            e.Property(x => x.PrefixField).HasMaxLength(64);
            e.Property(x => x.PrefixSeparator).HasMaxLength(4);
            e.Property(x => x.EquipmentTypeField).HasMaxLength(64);
            e.Property(x => x.TypeSeparator).HasMaxLength(4);
            e.Property(x => x.Suffix).HasMaxLength(16);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DictionaryType>(e =>
        {
            e.ToTable("dictionary_types");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<DictionaryItem>(e =>
        {
            e.ToTable("dictionary_items");
            e.HasIndex(x => new { x.TypeId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Type).WithMany(x => x.Items).HasForeignKey(x => x.TypeId);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.UserName).HasMaxLength(64);
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.EntityType).HasMaxLength(64);
        });
    }
}
