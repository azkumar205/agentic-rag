-- ═══════════════════════════════════════════════════════════
--  Agentic RAG — SQL Database Setup
--  Run this script on the Azure SQL Database after Bicep deploy
-- ═══════════════════════════════════════════════════════════

-- ── Tables ──────────────────────────────────────────────────
CREATE TABLE dbo.Vendors (
    VendorId        INT IDENTITY(1,1) PRIMARY KEY,
    VendorName      NVARCHAR(200) NOT NULL,
    ContactEmail    NVARCHAR(200),
    Phone           NVARCHAR(50),
    Category        NVARCHAR(100),
    IsActive        BIT DEFAULT 1,
    CreatedAt       DATETIME2 DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Contracts (
    ContractId      INT IDENTITY(1,1) PRIMARY KEY,
    VendorId        INT NOT NULL REFERENCES dbo.Vendors(VendorId),
    ContractNumber  NVARCHAR(50) NOT NULL,
    StartDate       DATE NOT NULL,
    EndDate         DATE NOT NULL,
    TotalValue      DECIMAL(18,2),
    Status          NVARCHAR(50) DEFAULT 'Active',
    CreatedAt       DATETIME2 DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Invoices (
    InvoiceId       INT IDENTITY(1,1) PRIMARY KEY,
    ContractId      INT NOT NULL REFERENCES dbo.Contracts(ContractId),
    InvoiceNumber   NVARCHAR(50) NOT NULL,
    InvoiceDate     DATE NOT NULL,
    DueDate         DATE NOT NULL,
    Amount          DECIMAL(18,2) NOT NULL,
    PaidAmount      DECIMAL(18,2) DEFAULT 0,
    Status          NVARCHAR(30) DEFAULT 'Pending',
    CreatedAt       DATETIME2 DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.BillingLineItems (
    LineItemId      INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId       INT NOT NULL REFERENCES dbo.Invoices(InvoiceId),
    Description     NVARCHAR(500),
    Quantity        INT,
    UnitPrice       DECIMAL(18,2),
    LineTotal       AS (Quantity * UnitPrice) PERSISTED,
    Category        NVARCHAR(100)
);
GO

-- ── Security Views (agent queries these, NOT base tables) ──
CREATE OR ALTER VIEW dbo.vw_BillingOverview AS
SELECT
    v.VendorName,
    c.ContractNumber,
    c.Status AS ContractStatus,
    i.InvoiceNumber,
    i.InvoiceDate,
    i.Amount,
    i.PaidAmount,
    i.Status AS InvoiceStatus,
    (i.Amount - i.PaidAmount) AS OutstandingBalance
FROM dbo.Invoices i
JOIN dbo.Contracts c ON i.ContractId = c.ContractId
JOIN dbo.Vendors v ON c.VendorId = v.VendorId;
GO

CREATE OR ALTER VIEW dbo.vw_ContractSummary AS
SELECT
    v.VendorName,
    c.ContractNumber,
    c.StartDate,
    c.EndDate,
    c.TotalValue,
    c.Status,
    COUNT(i.InvoiceId) AS InvoiceCount,
    ISNULL(SUM(i.Amount), 0) AS TotalInvoiced,
    ISNULL(SUM(i.PaidAmount), 0) AS TotalPaid
FROM dbo.Contracts c
JOIN dbo.Vendors v ON c.VendorId = v.VendorId
LEFT JOIN dbo.Invoices i ON c.ContractId = i.ContractId
GROUP BY v.VendorName, c.ContractNumber, c.StartDate, c.EndDate, c.TotalValue, c.Status;
GO

CREATE OR ALTER VIEW dbo.vw_InvoiceDetail AS
SELECT
    i.InvoiceNumber,
    i.InvoiceDate,
    i.DueDate,
    li.Description AS LineItemDescription,
    li.Quantity,
    li.UnitPrice,
    li.LineTotal,
    li.Category AS LineCategory,
    i.Amount AS InvoiceTotal
FROM dbo.Invoices i
JOIN dbo.BillingLineItems li ON i.InvoiceId = li.InvoiceId;
GO

CREATE OR ALTER VIEW dbo.vw_VendorAnalysis AS
SELECT
    v.VendorName,
    v.Category AS VendorCategory,
    COUNT(DISTINCT c.ContractId) AS ContractCount,
    COUNT(DISTINCT i.InvoiceId) AS InvoiceCount,
    ISNULL(SUM(i.Amount), 0) AS TotalBilled,
    ISNULL(SUM(i.PaidAmount), 0) AS TotalPaid,
    ISNULL(SUM(i.Amount) - SUM(i.PaidAmount), 0) AS TotalOutstanding
FROM dbo.Vendors v
LEFT JOIN dbo.Contracts c ON v.VendorId = c.VendorId
LEFT JOIN dbo.Invoices i ON c.ContractId = i.ContractId
WHERE v.IsActive = 1
GROUP BY v.VendorName, v.Category;
GO

-- ── Sample Data ──────────────────────────────────────────────
INSERT INTO dbo.Vendors (VendorName, ContactEmail, Category) VALUES
    ('Contoso Ltd',    'billing@contoso.com',   'IT Services'),
    ('Fabrikam Inc',   'finance@fabrikam.com',  'Cloud Infrastructure'),
    ('Northwind Corp', 'ap@northwind.com',      'Consulting');

INSERT INTO dbo.Contracts (VendorId, ContractNumber, StartDate, EndDate, TotalValue) VALUES
    (1, 'CTR-2024-001', '2024-01-01', '2024-12-31', 120000.00),
    (2, 'CTR-2024-002', '2024-03-01', '2025-02-28', 250000.00),
    (3, 'CTR-2024-003', '2024-06-01', '2024-11-30',  45000.00);

INSERT INTO dbo.Invoices (ContractId, InvoiceNumber, InvoiceDate, DueDate, Amount, PaidAmount, Status) VALUES
    (1, 'INV-001', '2024-01-15', '2024-02-15', 10000.00, 10000.00, 'Paid'),
    (1, 'INV-002', '2024-02-15', '2024-03-15', 10000.00, 10000.00, 'Paid'),
    (1, 'INV-003', '2024-03-15', '2024-04-15', 10000.00,  5000.00, 'Partial'),
    (2, 'INV-004', '2024-03-15', '2024-04-15', 25000.00, 25000.00, 'Paid'),
    (2, 'INV-005', '2024-04-15', '2024-05-15', 25000.00,     0.00, 'Pending'),
    (3, 'INV-006', '2024-06-15', '2024-07-15',  7500.00,  7500.00, 'Paid');

INSERT INTO dbo.BillingLineItems (InvoiceId, Description, Quantity, UnitPrice, Category) VALUES
    (1, 'App Development - Sprint 1', 80, 125.00, 'Development'),
    (2, 'App Development - Sprint 2', 80, 125.00, 'Development'),
    (3, 'App Development - Sprint 3', 80, 125.00, 'Development'),
    (4, 'Azure VM (D4s_v3) - March',  1, 15000.00, 'Infrastructure'),
    (4, 'Azure Storage - March',       1, 5000.00,  'Infrastructure'),
    (4, 'Support SLA - March',         1, 5000.00,  'Support'),
    (5, 'Azure VM (D4s_v3) - April',  1, 15000.00, 'Infrastructure'),
    (5, 'Azure Storage - April',       1, 5000.00,  'Infrastructure'),
    (5, 'Support SLA - April',         1, 5000.00,  'Support'),
    (6, 'UX Consulting - June',       50, 150.00,   'Consulting');

PRINT '✓ Database setup complete — tables, views, and sample data created.';
GO
