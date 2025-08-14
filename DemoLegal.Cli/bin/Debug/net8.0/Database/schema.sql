CREATE TABLE IF NOT EXISTS account (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ls TEXT NOT NULL,
  ls_code TEXT,
  fio TEXT,
  address_raw TEXT NOT NULL,
  address_norm TEXT,
  premises_type TEXT,
  ls_status TEXT,
  ls_close_date TEXT,
  ls_type TEXT,
  mgmt_status TEXT,
  organization TEXT NOT NULL,
  group_company TEXT,
  division TEXT,
  division_head TEXT,
  accrual_center TEXT,
  object_name TEXT,
  district TEXT,
  house TEXT,
  adrN TEXT
);

CREATE TABLE IF NOT EXISTS period_balance (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  account_id INTEGER NOT NULL REFERENCES account(id) ON DELETE CASCADE,
  period_date TEXT NOT NULL,
  debt_start NUMERIC NOT NULL DEFAULT 0,
  accrued NUMERIC NOT NULL DEFAULT 0,
  paid NUMERIC NOT NULL DEFAULT 0,
  debt_end NUMERIC NOT NULL DEFAULT 0,
  months_in_debt INTEGER,
  debt_category TEXT,
  debt_structure TEXT,
  src_file TEXT,
  room_no TEXT
);

CREATE TABLE IF NOT EXISTS case_file (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  account_id INTEGER NOT NULL REFERENCES account(id) ON DELETE CASCADE,
  created_at TEXT NOT NULL,
  status TEXT NOT NULL,
  debtor_type TEXT NOT NULL,
  debt_amount NUMERIC NOT NULL,
  period_from TEXT NOT NULL,
  period_to TEXT NOT NULL,
  service_kind TEXT NOT NULL,
  mgmt_status_text TEXT NOT NULL,
  enrichment_flags TEXT
);
