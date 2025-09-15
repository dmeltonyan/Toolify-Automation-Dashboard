-- Create DB if missing
IF DB_ID(N'toolify') IS NULL
BEGIN
  CREATE DATABASE toolify;
END
GO

USE toolify;
GO

-- boards
IF OBJECT_ID('dbo.boards','U') IS NULL
BEGIN
  CREATE TABLE dbo.boards(
    board_id NVARCHAR(64) NOT NULL PRIMARY KEY,
    board_name NVARCHAR(256) NOT NULL
  );
END
GO

-- lists
IF OBJECT_ID('dbo.lists','U') IS NULL
BEGIN
  CREATE TABLE dbo.lists(
    list_id NVARCHAR(64) NOT NULL PRIMARY KEY,
    board_id NVARCHAR(64) NOT NULL,
    list_name NVARCHAR(256) NOT NULL,
    position INT NULL,
    CONSTRAINT fk_lists_board FOREIGN KEY(board_id) REFERENCES dbo.boards(board_id)
  );
END
GO

-- cards
IF OBJECT_ID('dbo.cards','U') IS NULL
BEGIN
  CREATE TABLE dbo.cards(
    card_id NVARCHAR(64) NOT NULL PRIMARY KEY,
    list_id NVARCHAR(64) NOT NULL,
    board_id NVARCHAR(64) NOT NULL,
    title NVARCHAR(512) NOT NULL,
    description NVARCHAR(MAX) NULL,
    status NVARCHAR(64) NULL,
    start_date DATETIME2 NULL,
    due_date DATETIME2 NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    estimate_hours DECIMAL(10,2) NULL,
    worked_hours DECIMAL(10,2) NULL,
    members NVARCHAR(512) NULL,
    CONSTRAINT fk_cards_list FOREIGN KEY(list_id) REFERENCES dbo.lists(list_id),
    CONSTRAINT fk_cards_board FOREIGN KEY(board_id) REFERENCES dbo.boards(board_id)
  );
END
GO

-- tags
IF OBJECT_ID('dbo.tags','U') IS NULL
BEGIN
  CREATE TABLE dbo.tags(
    tag_name NVARCHAR(128) NOT NULL,
    board_id NVARCHAR(64) NOT NULL,
    CONSTRAINT pk_tags PRIMARY KEY(tag_name, board_id)
  );
END
GO

-- card_tags
IF OBJECT_ID('dbo.card_tags','U') IS NULL
BEGIN
  CREATE TABLE dbo.card_tags(
    card_id NVARCHAR(64) NOT NULL,
    tag_name NVARCHAR(128) NOT NULL,
    board_id NVARCHAR(64) NOT NULL,
    CONSTRAINT pk_card_tags PRIMARY KEY(card_id, tag_name),
    CONSTRAINT fk_ct_card FOREIGN KEY(card_id) REFERENCES dbo.cards(card_id),
    CONSTRAINT fk_ct_tag FOREIGN KEY(tag_name, board_id) REFERENCES dbo.tags(tag_name, board_id)
  );
END
GO

-- employees
IF OBJECT_ID('dbo.employees','U') IS NULL
BEGIN
  CREATE TABLE dbo.employees(
    employee_id INT IDENTITY PRIMARY KEY,
    first_name NVARCHAR(128) NOT NULL,
    last_name NVARCHAR(128) NOT NULL,
    role NVARCHAR(128) NULL,
    status NVARCHAR(64) NULL, -- Active/Contract/Inactive
    email NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
  );
END
GO

-- pbi_reports
IF OBJECT_ID('dbo.pbi_reports','U') IS NULL
BEGIN
  CREATE TABLE dbo.pbi_reports(
    id INT IDENTITY PRIMARY KEY,
    serial_no INT NOT NULL,
    stream NVARCHAR(128) NULL,
    workspace NVARCHAR(256) NULL,
    report_title NVARCHAR(256) NOT NULL,
    report_link NVARCHAR(512) NULL,
    pages NVARCHAR(256) NULL,
    sources NVARCHAR(512) NULL,
    semantic_layer NVARCHAR(256) NULL,
    active_last30 NVARCHAR(64) NULL,
    schedule NVARCHAR(64) NULL,
    frequency_time NVARCHAR(128) NULL,
    lob NVARCHAR(128) NULL,
    stakeholder NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
  );
END
GO
