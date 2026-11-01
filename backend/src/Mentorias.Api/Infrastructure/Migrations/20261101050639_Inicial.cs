using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mentorias.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Mentores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Correo = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mentores", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Mentorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Slug = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Titulo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Categoria = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    DuracionMinutos = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mentorias", x => x.Id);
                    table.CheckConstraint("CK_Mentoria_Duracion", "DuracionMinutos IN (30, 60, 90, 120)");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Sesiones",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    MentorId = table.Column<int>(type: "int", nullable: false),
                    Ticket = table.Column<byte[]>(type: "longblob", nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sesiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sesiones_Mentores_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Mentores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MentoriaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Correo = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Nivel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Mensaje = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    InicioUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservas_Mentorias_MentoriaId",
                        column: x => x.MentoriaId,
                        principalTable: "Mentorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Adjuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReservaId = table.Column<int>(type: "int", nullable: false),
                    NombreOriginal = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    TipoContenido = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adjuntos_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BloquesAgenda",
                columns: table => new
                {
                    InicioUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReservaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloquesAgenda", x => x.InicioUtc);
                    table.ForeignKey(
                        name: "FK_BloquesAgenda_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Mentorias",
                columns: new[] { "Id", "Activa", "Categoria", "Descripcion", "DuracionMinutos", "Slug", "Titulo" },
                values: new object[,]
                {
                    { 1, true, "fundamentos", "Del enunciado al pseudocódigo y a la prueba de escritorio, con ejercicios de condicionales y ciclos.", 60, "logica-algoritmos", "Lógica y algoritmos" },
                    { 2, true, "java", "Variables, métodos, arreglos y manejo de entradas para escribir programas que compilan a la primera.", 60, "java-desde-cero", "Java desde cero" },
                    { 3, true, "java", "Clases, herencia, polimorfismo e interfaces explicados con ejemplos que puedes ejecutar.", 90, "poo", "Programación orientada a objetos" },
                    { 4, true, "web", "Páginas semánticas, diseño responsivo y accesible, e interacciones sin dependencias innecesarias.", 60, "html-css-js", "HTML, CSS y JavaScript" },
                    { 5, true, "datos", "Tablas, relaciones y consultas para diseñar una base de datos que no se rompa al crecer.", 90, "sql-modelado", "SQL y modelado de datos" },
                    { 6, true, "java", "Estructura, organización en paquetes y revisión de código de tu proyecto de fin de ciclo.", 90, "proyecto-java", "Proyecto final en Java" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adjuntos_ReservaId",
                table: "Adjuntos",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_BloquesAgenda_ReservaId",
                table: "BloquesAgenda",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_Mentores_Correo",
                table: "Mentores",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mentorias_Slug",
                table: "Mentorias",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Correo",
                table: "Reservas",
                column: "Correo");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Estado_InicioUtc",
                table: "Reservas",
                columns: new[] { "Estado", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_InicioUtc",
                table: "Reservas",
                column: "InicioUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_MentoriaId",
                table: "Reservas",
                column: "MentoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_ExpiraUtc",
                table: "Sesiones",
                column: "ExpiraUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_MentorId",
                table: "Sesiones",
                column: "MentorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Adjuntos");

            migrationBuilder.DropTable(
                name: "BloquesAgenda");

            migrationBuilder.DropTable(
                name: "Sesiones");

            migrationBuilder.DropTable(
                name: "Reservas");

            migrationBuilder.DropTable(
                name: "Mentores");

            migrationBuilder.DropTable(
                name: "Mentorias");
        }
    }
}
