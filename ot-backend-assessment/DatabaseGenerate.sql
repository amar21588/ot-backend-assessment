
IF DB_ID('OT_Assessment_DB') IS NULL
BEGIN
    CREATE DATABASE OT_Assessment_DB;
END
GO

USE OT_Assessment_DB;
GO

IF OBJECT_ID('dbo.CasinoWagers', 'U') IS NOT NULL
    DROP TABLE dbo.CasinoWagers;
GO

CREATE TABLE dbo.CasinoWagers
(
    WagerId UNIQUEIDENTIFIER NOT NULL,

    AccountId UNIQUEIDENTIFIER NOT NULL,

    Username NVARCHAR(100) NOT NULL,

    Theme NVARCHAR(100) NULL,

    Provider NVARCHAR(200) NOT NULL,

    GameName NVARCHAR(200) NOT NULL,

    TransactionId UNIQUEIDENTIFIER NOT NULL,

    BrandId UNIQUEIDENTIFIER NOT NULL,

    ExternalReferenceId UNIQUEIDENTIFIER NOT NULL,

    TransactionTypeId UNIQUEIDENTIFIER NOT NULL,

    Amount DECIMAL(19,4) NOT NULL,

    CreatedDateTime DATETIMEOFFSET(7) NOT NULL,

    NumberOfBets INT NOT NULL,

    CountryCode CHAR(2) NOT NULL,

    Duration BIGINT NOT NULL,

    CONSTRAINT PK_CasinoWagers
        PRIMARY KEY CLUSTERED (WagerId),

    CONSTRAINT CK_CasinoWagers_Amount
        CHECK (Amount >= 0),

    CONSTRAINT CK_CasinoWagers_NumberOfBets
        CHECK (NumberOfBets >= 0),

    CONSTRAINT CK_CasinoWagers_Duration
        CHECK (Duration >= 0)
);
GO

CREATE NONCLUSTERED INDEX IX_CasinoWagers_AccountId_CreatedDateTime
ON dbo.CasinoWagers
(
    AccountId,
    CreatedDateTime DESC
)
INCLUDE
(
    WagerId,
    GameName,
    Provider,
    Amount
);
GO

CREATE NONCLUSTERED INDEX IX_CasinoWagers_AccountId_Amount
ON dbo.CasinoWagers
(
    AccountId,
    Amount
)
INCLUDE
(
    Username
);
GO

--Stored Procedures


--dbo.usp_GetPlayerCasinoWagers

CREATE OR ALTER PROCEDURE dbo.usp_GetPlayerCasinoWagers
    @AccountId UNIQUEIDENTIFIER,
    @Page INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        WagerId,
        GameName AS Game,
        Provider,
        Amount,
        CreatedDateTime AS CreatedDate
    FROM dbo.CasinoWagers
    WHERE AccountId = @AccountId
    ORDER BY CreatedDateTime DESC, WagerId DESC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT_BIG(*) AS Total
    FROM dbo.CasinoWagers
    WHERE AccountId = @AccountId;
END;
GO


--dbo.usp_GetTopSpenders

CREATE OR ALTER PROCEDURE dbo.usp_GetTopSpenders
    @Count INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Count)
        AccountId,
        MAX(Username) AS Username,
        SUM(Amount) AS TotalAmountSpend
    FROM dbo.CasinoWagers
    GROUP BY AccountId
    ORDER BY SUM(Amount) DESC, AccountId;
END;
GO
