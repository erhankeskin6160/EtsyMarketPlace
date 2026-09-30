IF OBJECT_ID(N'dbo.EtsyShopConnections', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.EtsyShopConnections
	(
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EtsyShopConnections PRIMARY KEY,
		ShopId NVARCHAR(64) NOT NULL,
		ShopName NVARCHAR(255) NOT NULL,
		UserId NVARCHAR(128) NOT NULL,
		LastSynchronizedAt DATETIMEOFFSET NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_EtsyShopConnections_IsActive DEFAULT 1
	);
	CREATE UNIQUE INDEX UX_EtsyShopConnections_UserId_ShopId ON dbo.EtsyShopConnections(UserId, ShopId);
END;

IF OBJECT_ID(N'dbo.EtsyOAuthTokens', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.EtsyOAuthTokens
	(
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EtsyOAuthTokens PRIMARY KEY,
		ShopId NVARCHAR(64) NOT NULL,
		AccessToken NVARCHAR(4096) NOT NULL,
		RefreshToken NVARCHAR(4096) NOT NULL,
		AccessTokenExpiresAt DATETIMEOFFSET NOT NULL,
		TokenType NVARCHAR(32) NOT NULL CONSTRAINT DF_EtsyOAuthTokens_TokenType DEFAULT 'Bearer'
	);
	CREATE UNIQUE INDEX UX_EtsyOAuthTokens_ShopId ON dbo.EtsyOAuthTokens(ShopId);
END;

IF OBJECT_ID(N'dbo.EtsySyncStates', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.EtsySyncStates
	(
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EtsySyncStates PRIMARY KEY,
		ShopId NVARCHAR(64) NOT NULL,
		DataType NVARCHAR(64) NOT NULL,
		LastSuccessfulSyncAt DATETIMEOFFSET NULL,
		LastCursorAt DATETIMEOFFSET NULL,
		LastError NVARCHAR(MAX) NULL
	);
	CREATE UNIQUE INDEX UX_EtsySyncStates_ShopId_DataType ON dbo.EtsySyncStates(ShopId, DataType);
END;

IF OBJECT_ID(N'dbo.EtsyBankPayouts', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.EtsyBankPayouts
	(
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EtsyBankPayouts PRIMARY KEY,
		ShopId NVARCHAR(64) NOT NULL,
		ReferenceId NVARCHAR(128) NOT NULL,
		OccurredAt DATETIMEOFFSET NOT NULL,
		Amount DECIMAL(19,4) NOT NULL,
		Currency NVARCHAR(8) NOT NULL,
		ExchangeRateToTry DECIMAL(19,8) NULL,
		Status NVARCHAR(64) NOT NULL,
		Description NVARCHAR(1024) NOT NULL
	);
	CREATE UNIQUE INDEX UX_EtsyBankPayouts_ShopId_ReferenceId ON dbo.EtsyBankPayouts(ShopId, ReferenceId);
END;

IF OBJECT_ID(N'dbo.EtsyFinancialTransactions', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.EtsyFinancialTransactions
	(
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EtsyFinancialTransactions PRIMARY KEY,
		ShopId NVARCHAR(64) NOT NULL,
		ReferenceId NVARCHAR(128) NOT NULL,
		OccurredAt DATETIMEOFFSET NOT NULL,
		GrossSales DECIMAL(19,4) NOT NULL,
		PlatformFees DECIMAL(19,4) NOT NULL,
		InternalAdsCost DECIMAL(19,4) NOT NULL,
		ExternalAdsCost DECIMAL(19,4) NOT NULL,
		ProductCost DECIMAL(19,4) NOT NULL,
		ShippingCost DECIMAL(19,4) NOT NULL,
		Refunds DECIMAL(19,4) NOT NULL,
		Currency NVARCHAR(8) NOT NULL
	);
	CREATE UNIQUE INDEX UX_EtsyFinancialTransactions_ShopId_ReferenceId ON dbo.EtsyFinancialTransactions(ShopId, ReferenceId);
END;

IF OBJECT_ID(N'dbo.EtsyOrderCosts', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.EtsyOrderCosts
	(
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EtsyOrderCosts PRIMARY KEY,
		ShopId NVARCHAR(64) NOT NULL,
		OrderId NVARCHAR(128) NOT NULL,
		CreatedAt DATETIMEOFFSET NOT NULL,
		Currency NVARCHAR(8) NOT NULL,
		OrderTotal DECIMAL(19,4) NOT NULL,
		ProductCost DECIMAL(19,4) NULL,
		ShippingCost DECIMAL(19,4) NULL,
		AlertReason NVARCHAR(1024) NULL
	);
	CREATE UNIQUE INDEX UX_EtsyOrderCosts_ShopId_OrderId ON dbo.EtsyOrderCosts(ShopId, OrderId);
END;
