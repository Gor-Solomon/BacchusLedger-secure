// UI strings: English / Georgian (section 10).
// NOTE: Georgian strings — verify with a native speaker before any public use.
// Strings without a Georgian entry fall back to English.
window.I18N = {
  ka: {
    "Overview": "მიმოხილვა",
    "Batches": "პარტიები",
    "Record operation": "ოპერაციის ჩაწერა",
    "Vessels": "ჭურჭელი",
    "Farmers & plots": "ფერმერები და ნაკვეთები",
    "Deliveries": "ყურძნის მიღება",
    "Journals": "ჟურნალები",
    "Settings": "პარამეტრები",
    "Separation": "ჭაჭიდან გამოყოფა",
    "Racking": "ლექიდან მოხსნა",
    "Filtration": "ფილტრაცია",
    "Transfer": "გადატანა",
    "Stock check": "ნაშთის შემოწმება",
    "Bottling": "ჩამოსხმა",
    "Litres": "ლიტრი",
    "Loss": "დანაკარგი",
    "Dip reading (cm)": "სიღრმის ზომა (სმ)",
    "Save": "შენახვა",
    "Qvevri": "ქვევრი",
    "Tank": "ავზი",
    "Batch": "პარტია",
    "Farmer": "ფერმერი",
    "Plot": "ნაკვეთი",
    "Harvest": "რთველი",
    "Fermentation": "დუღილი",
    "Above normal": "ნორმაზე მეტი",
    "Normal": "ნორმაშია",
    "Run the A→Z demo": "დემოს გაშვება",
    // Update 2 — verify with a native speaker
    "Initial Brix": "საწყისი ბრიქსი",
    "Refractometer": "რეფრაქტომეტრი",
    "Hydrometer": "ჰიდრომეტრი",
    "Density": "სიმკვრივე",
    "Alcohol": "ალკოჰოლი",
    "Potential alcohol": "პოტენციური ალკოჰოლი",
    "Titratable acidity": "ტიტრული მჟავიანობა",
    "Temperature": "ტემპერატურა",
    "Readings": "გაზომვები",
    "Cellar label": "მარნის ეტიკეტი",
    "Volume unknown": "მოცულობა უცნობია",
    // Update 4 — Marani Assistant (verify with a native speaker)
    "Quick questions": "სწრაფი კითხვები",
    "Send": "გაგზავნა",
    "Open in app": "აპში გახსნა",
    "Used": "გამოყენებულია",
    "Typing": "წერს",
    "Ask about a batch, vessel or reading…": "იკითხეთ პარტიის, ჭურჭლის ან გაზომვის შესახებ…",
    "AI is offline right now — use the quick questions above.": "AI ახლა მიუწვდომელია — გამოიყენეთ ზემოთ მოცემული სწრაფი კითხვები.",
    "Assistant unavailable": "ასისტენტი მიუწვდომელია",
  },
};

window.LANG = (() => { try { return localStorage.getItem("bl.lang") || "en"; } catch { return "en"; } })();

window.t = function (s) {
  if (window.LANG === "ka" && window.I18N.ka[s]) return window.I18N.ka[s];
  return s;
};

window.setLang = function (l) {
  window.LANG = l;
  try { localStorage.setItem("bl.lang", l); } catch { /* storage unavailable */ }
  document.documentElement.lang = l === "ka" ? "ka" : "en";
};
