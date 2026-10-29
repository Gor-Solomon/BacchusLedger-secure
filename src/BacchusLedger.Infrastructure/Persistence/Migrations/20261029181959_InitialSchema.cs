using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BacchusLedger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// The schema the app always had (same tables, columns, types, keys, unique constraints, foreign keys, CHECKs and triggers).
    /// Edited by hand after generation: key columns without NOT NULL (as the old CREATE TABLE statements), no AUTOINCREMENT (the old tables reuse ids after the demo reset) and no declared rowid
    /// column on the tables without a key (demo_rows, maintenance, seal_tamper, batch_origins use SQLite's implicit rowid).
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demo_progress",
                columns: table => new
                {
                    script = table.Column<string>(type: "TEXT", nullable: true),
                    step = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_progress", x => new { x.script, x.step });
                });

            migrationBuilder.CreateTable(
                name: "demo_rows",
                columns: table => new
                {
                    script = table.Column<string>(type: "TEXT", nullable: true),
                    tbl = table.Column<string>(type: "TEXT", nullable: true),
                    row_id = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "farmers",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    code = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: true),
                    phone = table.Column<string>(type: "TEXT", nullable: true),
                    is_demo = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "live_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: true),
                    sim_time = table.Column<string>(type: "TEXT", nullable: true),
                    vessel_id = table.Column<long>(type: "INTEGER", nullable: true),
                    vessel_code = table.Column<string>(type: "TEXT", nullable: true),
                    vessel_name = table.Column<string>(type: "TEXT", nullable: true),
                    kind = table.Column<string>(type: "TEXT", nullable: true),
                    metric = table.Column<string>(type: "TEXT", nullable: true),
                    value = table.Column<double>(type: "REAL", nullable: true),
                    unit = table.Column<string>(type: "TEXT", nullable: true),
                    level = table.Column<string>(type: "TEXT", nullable: true),
                    rule = table.Column<string>(type: "TEXT", nullable: true),
                    message = table.Column<string>(type: "TEXT", nullable: true),
                    value_usd = table.Column<double>(type: "REAL", nullable: true),
                    scenario = table.Column<string>(type: "TEXT", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    acknowledged = table.Column<long>(type: "INTEGER", nullable: true, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_events", x => x.id);
                    table.CheckConstraint("CK_live_events_kind", "kind IN ('reading','notification')");
                    table.CheckConstraint("CK_live_events_source", "source IN ('SIMULATED','SENSOR')");
                });

            migrationBuilder.CreateTable(
                name: "maintenance",
                columns: table => new
                {
                    reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "seal_batch",
                columns: table => new
                {
                    batch_id = table.Column<long>(type: "INTEGER", nullable: true),
                    batch_code = table.Column<string>(type: "TEXT", nullable: true),
                    final_hash = table.Column<string>(type: "TEXT", nullable: true),
                    signature = table.Column<string>(type: "TEXT", nullable: true),
                    sealed_at = table.Column<string>(type: "TEXT", nullable: true),
                    entries = table.Column<long>(type: "INTEGER", nullable: true),
                    key_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seal_batch", x => x.batch_id);
                });

            migrationBuilder.CreateTable(
                name: "seal_chain",
                columns: table => new
                {
                    batch_id = table.Column<long>(type: "INTEGER", nullable: true),
                    seq = table.Column<long>(type: "INTEGER", nullable: true),
                    tbl = table.Column<string>(type: "TEXT", nullable: true),
                    row_id = table.Column<long>(type: "INTEGER", nullable: true),
                    prev_hash = table.Column<string>(type: "TEXT", nullable: true),
                    hash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seal_chain", x => new { x.batch_id, x.seq });
                });

            migrationBuilder.CreateTable(
                name: "seal_meta",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: true),
                    value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seal_meta", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "seal_tamper",
                columns: table => new
                {
                    tbl = table.Column<string>(type: "TEXT", nullable: true),
                    row_id = table.Column<long>(type: "INTEGER", nullable: true),
                    col = table.Column<string>(type: "TEXT", nullable: true),
                    old_value = table.Column<double>(type: "REAL", nullable: true),
                    batch_id = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: true),
                    value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "wineries",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    code = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: true),
                    size = table.Column<string>(type: "TEXT", nullable: true),
                    yearly_t = table.Column<double>(type: "REAL", nullable: true),
                    uses_qvevri = table.Column<long>(type: "INTEGER", nullable: true),
                    readiness_level = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wineries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    winery_id = table.Column<long>(type: "INTEGER", nullable: true),
                    code = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: true),
                    wine_type = table.Column<string>(type: "TEXT", nullable: true),
                    technology = table.Column<string>(type: "TEXT", nullable: true),
                    variety = table.Column<string>(type: "TEXT", nullable: true),
                    vintage = table.Column<long>(type: "INTEGER", nullable: true),
                    abv = table.Column<double>(type: "REAL", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: true),
                    source_note = table.Column<string>(type: "TEXT", nullable: true),
                    grapes_kg = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches", x => x.id);
                    table.CheckConstraint("CK_batches_technology", "technology IN ('classic','qvevri')");
                    table.ForeignKey(
                        name: "FK_batches_wineries_winery_id",
                        column: x => x.winery_id,
                        principalTable: "wineries",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "plots",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    farmer_id = table.Column<long>(type: "INTEGER", nullable: true),
                    winery_id = table.Column<long>(type: "INTEGER", nullable: true),
                    code = table.Column<string>(type: "TEXT", nullable: true),
                    cadastre_no = table.Column<string>(type: "TEXT", nullable: true),
                    village = table.Column<string>(type: "TEXT", nullable: true),
                    municipality = table.Column<string>(type: "TEXT", nullable: true),
                    region = table.Column<string>(type: "TEXT", nullable: true),
                    area_ha = table.Column<double>(type: "REAL", nullable: true),
                    variety = table.Column<string>(type: "TEXT", nullable: true),
                    lat = table.Column<double>(type: "REAL", nullable: true),
                    lng = table.Column<double>(type: "REAL", nullable: true),
                    licence_no = table.Column<string>(type: "TEXT", nullable: true),
                    soil_texture = table.Column<string>(type: "TEXT", nullable: true),
                    soil_ph = table.Column<double>(type: "REAL", nullable: true),
                    rain_mm = table.Column<double>(type: "REAL", nullable: true),
                    summer_temp_c = table.Column<double>(type: "REAL", nullable: true),
                    is_demo = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plots", x => x.id);
                    table.ForeignKey(
                        name: "FK_plots_farmers_farmer_id",
                        column: x => x.farmer_id,
                        principalTable: "farmers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_plots_wineries_winery_id",
                        column: x => x.winery_id,
                        principalTable: "wineries",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "vessels",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    winery_id = table.Column<long>(type: "INTEGER", nullable: true),
                    code = table.Column<string>(type: "TEXT", nullable: true),
                    type = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: true),
                    calibration_json = table.Column<string>(type: "TEXT", nullable: true),
                    capacity_l = table.Column<double>(type: "REAL", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: true),
                    cellar_label = table.Column<string>(type: "TEXT", nullable: true),
                    cellar_label_source = table.Column<string>(type: "TEXT", nullable: true),
                    depth_cm = table.Column<double>(type: "REAL", nullable: true),
                    neck_d_cm = table.Column<double>(type: "REAL", nullable: true),
                    max_d_cm = table.Column<double>(type: "REAL", nullable: true),
                    known_volume_l = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vessels", x => x.id);
                    table.CheckConstraint("CK_vessels_type", "type IN ('tank','qvevri')");
                    table.ForeignKey(
                        name: "FK_vessels_wineries_winery_id",
                        column: x => x.winery_id,
                        principalTable: "wineries",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    winery_id = table.Column<long>(type: "INTEGER", nullable: true),
                    farmer_id = table.Column<long>(type: "INTEGER", nullable: true),
                    plot_id = table.Column<long>(type: "INTEGER", nullable: true),
                    variety = table.Column<string>(type: "TEXT", nullable: true),
                    kg_planned = table.Column<double>(type: "REAL", nullable: true),
                    price_gel_per_kg = table.Column<double>(type: "REAL", nullable: true),
                    date = table.Column<string>(type: "TEXT", nullable: true),
                    is_demo = table.Column<long>(type: "INTEGER", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_orders_farmers_farmer_id",
                        column: x => x.farmer_id,
                        principalTable: "farmers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_orders_plots_plot_id",
                        column: x => x.plot_id,
                        principalTable: "plots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_orders_wineries_winery_id",
                        column: x => x.winery_id,
                        principalTable: "wineries",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "lab",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    batch_id = table.Column<long>(type: "INTEGER", nullable: true),
                    date = table.Column<string>(type: "TEXT", nullable: true),
                    vessel_id = table.Column<long>(type: "INTEGER", nullable: true),
                    stage = table.Column<string>(type: "TEXT", nullable: true),
                    sugar_gl = table.Column<double>(type: "REAL", nullable: true),
                    temp_c = table.Column<double>(type: "REAL", nullable: true),
                    ph = table.Column<double>(type: "REAL", nullable: true),
                    ta_gl = table.Column<double>(type: "REAL", nullable: true),
                    rs_gl = table.Column<double>(type: "REAL", nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab", x => x.id);
                    table.ForeignKey(
                        name: "FK_lab_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_lab_vessels_vessel_id",
                        column: x => x.vessel_id,
                        principalTable: "vessels",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "operations",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    batch_id = table.Column<long>(type: "INTEGER", nullable: true),
                    date = table.Column<string>(type: "TEXT", nullable: true),
                    type = table.Column<string>(type: "TEXT", nullable: true),
                    from_vessel_id = table.Column<long>(type: "INTEGER", nullable: true),
                    to_vessel_id = table.Column<long>(type: "INTEGER", nullable: true),
                    litres = table.Column<double>(type: "REAL", nullable: true),
                    bottles = table.Column<long>(type: "INTEGER", nullable: true),
                    bottle_ml = table.Column<double>(type: "REAL", nullable: true),
                    remainder_l = table.Column<double>(type: "REAL", nullable: true),
                    method = table.Column<string>(type: "TEXT", nullable: true),
                    dip_cm = table.Column<double>(type: "REAL", nullable: true),
                    person = table.Column<string>(type: "TEXT", nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations", x => x.id);
                    table.CheckConstraint("CK_operations_method", "method IS NULL OR method IN ('paper log','dip','bottle count','scale')");
                    table.CheckConstraint("CK_operations_type", "type IN ('intake','separation','racking','stock_check','filtration','transfer','bottling','warehouse')");
                    table.ForeignKey(
                        name: "FK_operations_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_operations_vessels_from_vessel_id",
                        column: x => x.from_vessel_id,
                        principalTable: "vessels",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_operations_vessels_to_vessel_id",
                        column: x => x.to_vessel_id,
                        principalTable: "vessels",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    batch_id = table.Column<long>(type: "INTEGER", nullable: true),
                    vessel_id = table.Column<long>(type: "INTEGER", nullable: true),
                    taken_at = table.Column<string>(type: "TEXT", nullable: true),
                    type = table.Column<string>(type: "TEXT", nullable: true),
                    value = table.Column<double>(type: "REAL", nullable: true),
                    unit = table.Column<string>(type: "TEXT", nullable: true),
                    instrument = table.Column<string>(type: "TEXT", nullable: true),
                    person = table.Column<string>(type: "TEXT", nullable: true),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_readings", x => x.id);
                    table.CheckConstraint("CK_readings_type", "type IN ('initial_brix','ph','temperature','hydrometer_density','hydrometer_abv','ta')");
                    table.ForeignKey(
                        name: "FK_readings_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_readings_vessels_vessel_id",
                        column: x => x.vessel_id,
                        principalTable: "vessels",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "sensor_readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    vessel_id = table.Column<long>(type: "INTEGER", nullable: true),
                    dip_cm = table.Column<double>(type: "REAL", nullable: true),
                    litres = table.Column<double>(type: "REAL", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_readings", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensor_readings_vessels_vessel_id",
                        column: x => x.vessel_id,
                        principalTable: "vessels",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: true),
                    order_id = table.Column<long>(type: "INTEGER", nullable: true),
                    plot_id = table.Column<long>(type: "INTEGER", nullable: true),
                    farmer_id = table.Column<long>(type: "INTEGER", nullable: true),
                    date = table.Column<string>(type: "TEXT", nullable: true),
                    kg = table.Column<double>(type: "REAL", nullable: true),
                    sugar_gl = table.Column<double>(type: "REAL", nullable: true),
                    quality_note = table.Column<string>(type: "TEXT", nullable: true),
                    accepted = table.Column<long>(type: "INTEGER", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_deliveries_farmers_farmer_id",
                        column: x => x.farmer_id,
                        principalTable: "farmers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_deliveries_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_deliveries_plots_plot_id",
                        column: x => x.plot_id,
                        principalTable: "plots",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "batch_origins",
                columns: table => new
                {
                    batch_id = table.Column<long>(type: "INTEGER", nullable: true),
                    delivery_id = table.Column<long>(type: "INTEGER", nullable: true),
                    kg = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_batch_origins_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_batch_origins_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_batches_code",
                table: "batches",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_farmers_code",
                table: "farmers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plots_code",
                table: "plots",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vessels_winery_id_code",
                table: "vessels",
                columns: new[] { "winery_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wineries_code",
                table: "wineries",
                column: "code",
                unique: true);

            // Append-only: operations and readings may never be changed or removed (demo reset is the only exception).
            // Triggers have no EF Core model, so they are plain SQL here (raw SQL rule: triggers and pragmas live in the migration).
            migrationBuilder.Sql("CREATE TRIGGER IF NOT EXISTS operations_no_update BEFORE UPDATE ON operations BEGIN SELECT RAISE(ABORT, 'operations are append-only'); END;");
            migrationBuilder.Sql("CREATE TRIGGER IF NOT EXISTS operations_no_delete BEFORE DELETE ON operations WHEN NOT EXISTS (SELECT 1 FROM maintenance) BEGIN SELECT RAISE(ABORT, 'operations are append-only'); END;");
            migrationBuilder.Sql("CREATE TRIGGER IF NOT EXISTS readings_no_update BEFORE UPDATE ON readings BEGIN SELECT RAISE(ABORT, 'readings are append-only'); END;");
            migrationBuilder.Sql("CREATE TRIGGER IF NOT EXISTS readings_no_delete BEFORE DELETE ON readings WHEN NOT EXISTS (SELECT 1 FROM maintenance) BEGIN SELECT RAISE(ABORT, 'readings are append-only'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Triggers have no EF Core model (raw SQL rule: triggers live in the migration).
            foreach (var trigger in new[] { "operations_no_update", "operations_no_delete", "readings_no_update", "readings_no_delete" })
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");

            migrationBuilder.DropTable(
                name: "batch_origins");

            migrationBuilder.DropTable(
                name: "demo_progress");

            migrationBuilder.DropTable(
                name: "demo_rows");

            migrationBuilder.DropTable(
                name: "lab");

            migrationBuilder.DropTable(
                name: "live_events");

            migrationBuilder.DropTable(
                name: "maintenance");

            migrationBuilder.DropTable(
                name: "operations");

            migrationBuilder.DropTable(
                name: "readings");

            migrationBuilder.DropTable(
                name: "seal_batch");

            migrationBuilder.DropTable(
                name: "seal_chain");

            migrationBuilder.DropTable(
                name: "seal_meta");

            migrationBuilder.DropTable(
                name: "seal_tamper");

            migrationBuilder.DropTable(
                name: "sensor_readings");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "batches");

            migrationBuilder.DropTable(
                name: "vessels");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "plots");

            migrationBuilder.DropTable(
                name: "farmers");

            migrationBuilder.DropTable(
                name: "wineries");
        }
    }
}
