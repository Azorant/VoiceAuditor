using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceAuditor.Database.Migrations
{
    /// <inheritdoc />
    public partial class Removedurationcolumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "AuditLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "AuditLogs",
                type: "time(6)",
                nullable: true);
        }
    }
}
