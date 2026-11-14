using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace WinDesign.Infrastructure;

/// <summary>
/// 骨架阶段使用 EnsureCreated，已有数据库不会自动补建新表。
/// 这里用幂等的 CREATE TABLE IF NOT EXISTS 为电信统计模块补齐表结构，
/// SQLite / PostgreSQL 各自提供对应方言。
/// </summary>
public static class DatabaseSchemaUpgrader
{
    public static async Task EnsureAsync(WinDesignDbContext db, CancellationToken ct = default)
    {
        // 早期编号规定是系统级单条（无 ProjectId），且尚未产生业务数据；
        // 检测到旧结构时直接重建为项目级结构。
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT \"ProjectId\" FROM \"numbering_rules\" LIMIT 1", ct);
        }
        catch (DbException)
        {
            await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"numbering_rules\"", ct);
        }

        var script = db.Database.IsNpgsql() ? PostgresScript : SqliteScript;

        foreach (var statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await db.Database.ExecuteSqlRawAsync(statement, ct);
        }

        // 字段长度是后加的能力：旧表已有数据时只补列，不重建。
        await EnsureColumnAsync(db, "PrefixLength", 4, ct);
        await EnsureColumnAsync(db, "TypeLength", 5, ct);
        await EnsureColumnAsync(db, "SequenceLength", 4, ct);
        await EnsureColumnAsync(db, "SuffixLength", 1, ct);
    }

    private static async Task EnsureColumnAsync(
        WinDesignDbContext db,
        string column,
        int defaultValue,
        CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"SELECT \"{column}\" FROM \"numbering_rules\" LIMIT 1", ct);
        }
        catch (DbException)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"numbering_rules\" ADD COLUMN \"{column}\" INTEGER NOT NULL DEFAULT {defaultValue}", ct);
        }
    }

    private const string SqliteScript = """
        CREATE TABLE IF NOT EXISTS "switch_models" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_switch_models" PRIMARY KEY,
            "Code" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "SwitchType" TEXT NOT NULL,
            "ElectricalPorts" INTEGER NOT NULL,
            "UplinkPorts" INTEGER NULL,
            "IsActive" INTEGER NOT NULL,
            "SortOrder" INTEGER NOT NULL,
            "Remark" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_switch_models_Code" ON "switch_models" ("Code");

        CREATE TABLE IF NOT EXISTS "telecom_rooms" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_telecom_rooms" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "PbsNodeId" TEXT NOT NULL,
            "Floor" TEXT NOT NULL,
            "Code" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "DataPointCount" INTEGER NOT NULL,
            "PhonePointCount" INTEGER NOT NULL,
            "ApCount" INTEGER NOT NULL,
            "SortOrder" INTEGER NOT NULL,
            "Remark" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            CONSTRAINT "FK_telecom_rooms_projects_ProjectId" FOREIGN KEY ("ProjectId")
                REFERENCES "projects" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_telecom_rooms_pbs_nodes_PbsNodeId" FOREIGN KEY ("PbsNodeId")
                REFERENCES "pbs_nodes" ("Id") ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS "IX_telecom_rooms_ProjectId_PbsNodeId_Floor_Code"
            ON "telecom_rooms" ("ProjectId", "PbsNodeId", "Floor", "Code");
        CREATE INDEX IF NOT EXISTS "IX_telecom_rooms_PbsNodeId" ON "telecom_rooms" ("PbsNodeId");

        CREATE TABLE IF NOT EXISTS "numbering_rules" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_numbering_rules" PRIMARY KEY,
            "ProjectId" TEXT NOT NULL,
            "PrefixField" TEXT NOT NULL,
            "PrefixSeparator" TEXT NOT NULL,
            "PrefixLength" INTEGER NOT NULL,
            "EquipmentTypeField" TEXT NOT NULL,
            "TypeSeparator" TEXT NOT NULL,
            "TypeLength" INTEGER NOT NULL,
            "SequenceLength" INTEGER NOT NULL,
            "Suffix" TEXT NOT NULL,
            "SuffixLength" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            CONSTRAINT "FK_numbering_rules_projects_ProjectId" FOREIGN KEY ("ProjectId")
                REFERENCES "projects" ("Id") ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_numbering_rules_ProjectId"
            ON "numbering_rules" ("ProjectId")
        """;

    private const string PostgresScript = """
        CREATE TABLE IF NOT EXISTS "switch_models" (
            "Id" uuid NOT NULL CONSTRAINT "PK_switch_models" PRIMARY KEY,
            "Code" text NOT NULL,
            "Name" text NOT NULL,
            "SwitchType" text NOT NULL,
            "ElectricalPorts" integer NOT NULL,
            "UplinkPorts" integer NULL,
            "IsActive" boolean NOT NULL,
            "SortOrder" integer NOT NULL,
            "Remark" text NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_switch_models_Code" ON "switch_models" ("Code");

        CREATE TABLE IF NOT EXISTS "telecom_rooms" (
            "Id" uuid NOT NULL CONSTRAINT "PK_telecom_rooms" PRIMARY KEY,
            "ProjectId" uuid NOT NULL,
            "PbsNodeId" uuid NOT NULL,
            "Floor" text NOT NULL,
            "Code" text NOT NULL,
            "Name" text NOT NULL,
            "DataPointCount" integer NOT NULL,
            "PhonePointCount" integer NOT NULL,
            "ApCount" integer NOT NULL,
            "SortOrder" integer NOT NULL,
            "Remark" text NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "FK_telecom_rooms_projects_ProjectId" FOREIGN KEY ("ProjectId")
                REFERENCES "projects" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_telecom_rooms_pbs_nodes_PbsNodeId" FOREIGN KEY ("PbsNodeId")
                REFERENCES "pbs_nodes" ("Id") ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS "IX_telecom_rooms_ProjectId_PbsNodeId_Floor_Code"
            ON "telecom_rooms" ("ProjectId", "PbsNodeId", "Floor", "Code");
        CREATE INDEX IF NOT EXISTS "IX_telecom_rooms_PbsNodeId" ON "telecom_rooms" ("PbsNodeId");

        CREATE TABLE IF NOT EXISTS "numbering_rules" (
            "Id" uuid NOT NULL CONSTRAINT "PK_numbering_rules" PRIMARY KEY,
            "ProjectId" uuid NOT NULL,
            "PrefixField" text NOT NULL,
            "PrefixSeparator" text NOT NULL,
            "PrefixLength" integer NOT NULL,
            "EquipmentTypeField" text NOT NULL,
            "TypeSeparator" text NOT NULL,
            "TypeLength" integer NOT NULL,
            "SequenceLength" integer NOT NULL,
            "Suffix" text NOT NULL,
            "SuffixLength" integer NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            CONSTRAINT "FK_numbering_rules_projects_ProjectId" FOREIGN KEY ("ProjectId")
                REFERENCES "projects" ("Id") ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_numbering_rules_ProjectId"
            ON "numbering_rules" ("ProjectId")
        """;
}
