using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TelematicsPipeline.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelematicsRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    SpeedKmh = table.Column<double>(type: "double precision", nullable: false),
                    HeadingDegrees = table.Column<double>(type: "double precision", nullable: false),
                    EngineRpm = table.Column<int>(type: "integer", nullable: true),
                    EngineCoolantTempC = table.Column<double>(type: "double precision", nullable: true),
                    FuelLevelPercent = table.Column<double>(type: "double precision", nullable: true),
                    OdometerKm = table.Column<double>(type: "double precision", nullable: false),
                    IsIdling = table.Column<bool>(type: "boolean", nullable: false),
                    IsIgnitionOn = table.Column<bool>(type: "boolean", nullable: false),
                    AccelerationXG = table.Column<double>(type: "double precision", nullable: true),
                    AccelerationYG = table.Column<double>(type: "double precision", nullable: true),
                    AccelerationZG = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelematicsRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelematicsRecords");
        }
    }
}
