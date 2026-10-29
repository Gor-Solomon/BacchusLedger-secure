namespace BacchusLedger.Dataset;

/// Section 4 seed data. Numbers are exactly as given in BACCHUS_LEDGER_CLI_PROMPT.md.
public static class SeedDataset
{
    public static readonly SeedWinery[] Wineries =
    [
        new("W01", "Large", 2000, true, "L1"),
        new("W02", "Medium", 300, true, "L1"),
        new("W03", "Small to medium", 100, true, "L2"),
        new("W04", "Small", 80, true, "L1"),
        new("W05", "Medium", 500, false, "L2"),
    ];

    public static readonly SeedFarmerPlot[] W01Farmers =
    [
        new("DF-1", "Demo Farmer 1", "K-101", "DEMO-KV-0001", 0.8, "Saperavi", 5200, "W01"),
        new("DF-2", "Demo Farmer 2", "K-102", "DEMO-KV-0002", 0.6, "Saperavi", 4300, "W01"),
        new("DF-3", "Demo Farmer 3", "K-103", "DEMO-KV-0003", 0.5, "Saperavi", 3800, "W01"),
        new("DF-4", "Demo Farmer 4", "K-104", "DEMO-KV-0004", 0.4, "Saperavi", 2700, "W01"),
    ];
    public static readonly SeedFarmerPlot W03Farmer = new("DF-5", "Demo Farmer 5", "P-301", "DEMO-KV-0005", 0.3, "Rkatsiteli", 1850, "W03");

    // Terroir reference point M1, Kvareli (Dataset A1 + B2)
    public const double Lat = 41.92, Lng = 45.78, SoilPh = 6.7, RainMm = 603.8, SummerT = 24.3;
    public const string SoilTexture = "clay 27.3–30.6%, silt 46.3–48.2%, sand 23.1–24.7%";
    public const string Municipality = "Kvareli", Region = "Kakheti";

    public static readonly string[] PdoMunicipalities = ["Akhmeta", "Telavi", "Gurjaani", "Sighnaghi", "Dedoplistskaro", "Kvareli", "Lagodekhi", "Sagarejo"];

    public const double PriceSaperavi = 1.50, PriceOther = 1.20;

    public const string W01Batch = "W01-B1-2025", W03Batch = "W03-B3-2025";

    public static readonly SeedBatch[] Batches =
    [
        new(W01Batch, "W01", "2025-09-18", 16000, "Classic red wine", "classic", "Saperavi", 13.5),
        new("W02-B2-2025", "W02", "2025-09-22", 9800, "Classic white wine", "classic", "Rkatsiteli", null),
        new(W03Batch, "W03", "2025-09-19", 1850, "Qvevri amber wine", "qvevri", "Rkatsiteli", 12.8),
        new("W04-B4-2025", "W04", "2025-09-27", 1500, "Qvevri red wine", "qvevri", "Saperavi", null),
        new("W05-B5-2025", "W05", "2025-09-21", 7000, "Classic white wine", "classic", "Rkatsiteli", null),
    ];

    // Dataset A3: W01 batch 1, F-1, daily from 18.09.2025
    public static readonly double[] A3Sugar = [233.0, 230.0, 218.0, 193.0, 159.0, 123.0, 88.0, 60.0, 38.0, 21.0, 10.0, 4.6, 2.6, 2.4];
    public static readonly double[] A3Temp = [21.8, 22.4, 24.1, 26.0, 27.2, 27.5, 27.0, 26.4, 25.8, 25.1, 24.6, 24.0, 23.5, 23.2];
    public static string A3Date(int i) => new DateTime(2025, 9, 18).AddDays(i).ToString("yyyy-MM-dd");

    // Dataset A4.1: W02 batch 2
    public static readonly SeedPhTaRow[] A41 =
    [
        new("2025-09-23", 1, 3.18, 7.1), new("2025-09-26", 4, 3.16, 7.3), new("2025-09-29", 7, 3.17, 7.2), new("2025-10-02", 10, 3.20, 7.0),
        new("2025-10-05", 13, 3.22, 6.9), new("2025-10-08", 16, 3.23, 6.8), new("2025-10-11", 19, 3.23, 6.8),
    ];

    // Dataset A4.2: W03 batch 3
    public static readonly SeedLabRow[] A42 =
    [
        new("2025-10-15", "Q-3", "on pomace", 17.6, 3.46, 6.0, 2.3),
        new("2025-11-20", "T3-1", "storage", 15.2, 3.47, 5.9, 2.3),
        new("2025-12-15", "T3-2", "ageing", 12.8, 3.47, 5.8, 2.2),
        new("2026-03-26", "T3-2", "ageing", 14.0, 3.48, 5.8, 2.2),
        new("2026-06-09", "T3-2", "before bottling", 16.1, 3.48, 5.7, 2.2),
    ];

    // Dataset A6.2: W01 batch 1 (paper log)
    public static readonly SeedOperation[] W01Ops =
    [
        new("2025-09-18", "intake", null, "F-1", null, Note: "16,000 kg grapes"),
        new("2025-10-02", "separation", "F-1", "T1-1", 10100),
        new("2025-10-12", "racking", "T1-1", "T1-2", 9820),
        new("2025-12-15", "stock_check", "T1-2", null, 9820),
        new("2026-03-12", "racking", "T1-2", "T1-3", 9720),
        new("2026-04-14", "filtration", "T1-3", "BT-1", 9670),
        new("2026-04-16", "bottling", "BT-1", null, 9630, 12840, 750, 20),
        new("2026-04-17", "warehouse", null, null, null, 12840, 750, Note: "to finished goods"),
    ];

    // Dataset A6.1: W03 batch 3
    public static readonly SeedOperation[] W03Ops =
    [
        new("2025-09-19", "intake", null, "Q-3", null, Note: "1,850 kg grapes"),
        new("2025-11-15", "separation", "Q-3", "T3-1", 1230),
        new("2025-12-12", "racking", "T3-1", "T3-2", 1200),
        new("2026-03-26", "stock_check", "T3-2", null, 1200),
        new("2026-06-10", "filtration", "T3-2", "T3-3", 1188),
        new("2026-06-12", "bottling", "T3-3", null, 1182, 1576, 750, 0),
        new("2026-06-13", "warehouse", null, null, null, 1576, 750, Note: "to finished goods"),
    ];

    /// Dataset rows carry dates only: readings are stored at 09:00 and their note starts with this text.
    public const string NoTime = "time not in dataset";
}
