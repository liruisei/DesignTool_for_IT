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
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<PbsNode> PbsNodes => Set<PbsNode>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<EquipmentSpec> EquipmentSpecs => Set<EquipmentSpec>();
    public DbSet<MaterialItem> MaterialItems => Set<MaterialItem>();
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

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.ClientName).HasMaxLength(200);
            e.Property(x => x.Location).HasMaxLength(200);
            e.Property(x => x.Phase).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectMember>(e =>
        {
            e.ToTable("project_members");
            e.HasKey(x => new { x.ProjectId, x.UserId });
            e.HasOne(x => x.Project).WithMany(x => x.Members).HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.Property(x => x.RoleInProject).HasMaxLength(64);
        });

        modelBuilder.Entity<PbsNode>(e =>
        {
            e.ToTable("pbs_nodes");
            e.HasIndex(x => new { x.ProjectId, x.Code });
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.NodeType).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Project).WithMany(x => x.PbsNodes).HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Equipment>(e =>
        {
            e.ToTable("equipments");
            e.HasIndex(x => new { x.ProjectId, x.TagNo }).IsUnique();
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
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Project).WithMany(x => x.Equipments).HasForeignKey(x => x.ProjectId);
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
