using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Delivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestaurantInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantInvitations_AspNetUsers_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantInvitations_AspNetUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantInvitations_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantInvitations_AcceptedByUserId",
                table: "RestaurantInvitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantInvitations_InvitedByUserId",
                table: "RestaurantInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantInvitations_RestaurantId",
                table: "RestaurantInvitations",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "UX_RestaurantInvitations_Pending_RestaurantId_Email",
                table: "RestaurantInvitations",
                columns: new[] { "RestaurantId", "Email" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_RestaurantInvitations_TokenHash",
                table: "RestaurantInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestaurantInvitations");
        }
    }
}
