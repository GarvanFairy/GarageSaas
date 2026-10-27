SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'GarageSaas_Staging' THROW 51000, 'Only GarageSaas_Staging is allowed.', 1;
IF CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) NOT IN (N'garagesaasdbserver', N'garagesaasdbserver.database.windows.net') THROW 51000, 'Wrong SQL server.', 1;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 51000, 'Database must be empty.', 1;
BEGIN TRANSACTION;
CREATE TABLE [FuelType] (
    [Id] int NOT NULL IDENTITY,
    [Fuel] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_FuelType] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [GarageBusiness] (
    [Id] int NOT NULL IDENTITY,
    [GarageBusinessName] nvarchar(255) NOT NULL,
    [GarageAddressLine1] nvarchar(255) NULL,
    [GarageAddressLine2] nvarchar(255) NULL,
    [GarageAddressLine3] nvarchar(255) NULL,
    [GarageAddressLine4] nvarchar(255) NULL,
    [Postcode] nvarchar(20) NULL,
    [GarageEmailAddress] nvarchar(255) NULL,
    [GaragePhoneNumber] nvarchar(255) NULL,
    [GarageMobileNumber] nvarchar(255) NULL,
    [LogoImage] nvarchar(255) NULL,
    [VatNumber] nvarchar(255) NULL,
    [BusinessRegistrationNumber] nvarchar(255) NULL,
    [CreatedDate] datetime NOT NULL,
    [CreatedBy] nvarchar(255) NULL,
    [UpdatedDate] datetime NULL,
    [UpdatedBy] nvarchar(255) NULL,
    [Active] bit NOT NULL,
    [Blocked] bit NOT NULL,
    CONSTRAINT [PK_GarageBusiness] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [GarageRegistrationRequest] (
    [Id] int NOT NULL IDENTITY,
    [GarageBusinessName] nvarchar(150) NOT NULL,
    [AddressLine1] nvarchar(150) NOT NULL,
    [AddressLine2] nvarchar(150) NULL,
    [TownOrCity] nvarchar(100) NOT NULL,
    [County] nvarchar(100) NOT NULL,
    [Eircode] nvarchar(20) NULL,
    [BusinessPhone] nvarchar(50) NOT NULL,
    [BusinessEmail] nvarchar(256) NULL,
    [Website] nvarchar(500) NULL,
    [BusinessType] nvarchar(50) NOT NULL,
    [ContactFirstName] nvarchar(100) NOT NULL,
    [ContactLastName] nvarchar(100) NOT NULL,
    [ContactEmail] nvarchar(256) NOT NULL,
    [ContactMobile] nvarchar(50) NOT NULL,
    [AdditionalInformation] nvarchar(2000) NULL,
    [Status] nvarchar(30) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ReviewedDate] datetime2 NULL,
    [ReviewedBy] nvarchar(256) NULL,
    [CompletedDate] datetime2 NULL,
    [OnboardingTokenHash] nvarchar(64) NULL,
    [OnboardingTokenExpiresDate] datetime2 NULL,
    [OnboardingRevoked] bit NOT NULL DEFAULT (((0))),
    CONSTRAINT [PK_GarageRegistrationRequest] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [InvoiceWorkQuote] (
    [Id] int NOT NULL IDENTITY,
    [GarageBusinessCustomerId] int NULL,
    [InvoiceId] int NULL,
    [WorkQuoteId] int NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_InvoiceWorkQuote] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Mileage] (
    [Id] int NOT NULL IDENTITY,
    [Kilometers] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Mileage] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ServiceHistory] (
    [Id] int NOT NULL IDENTITY,
    [VehicleId] int NULL,
    [ServiceDate] datetime2 NOT NULL,
    [CustomerId] int NULL,
    [InvoiceId] int NULL,
    [WorkQuoteId] int NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ServiceHistory] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TransmissionType] (
    [Id] int NOT NULL IDENTITY,
    [Transmission] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_TransmissionType] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [ExternalUserId] nvarchar(100) NULL,
    [FirstName] nvarchar(255) NOT NULL,
    [LastName] nvarchar(255) NOT NULL,
    [EmailAddress] nvarchar(255) NULL,
    [PhoneNumber] nvarchar(255) NULL,
    [MobileNumber] nvarchar(255) NULL,
    [Admin_Owner] bit NOT NULL,
    [CreatedDate] datetime NOT NULL,
    [CreatedBy] nvarchar(255) NULL,
    [UpdatedDate] datetime NULL,
    [UpdatedBy] nvarchar(255) NULL,
    [Active] bit NOT NULL,
    [Blocked] bit NOT NULL,
    [GarageBusinessId] int NOT NULL,
    [IsPlatformAdministrator] bit NOT NULL DEFAULT (((0))),
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [VehicleMake] (
    [Id] int NOT NULL IDENTITY,
    [Make] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_VehicleMake] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [VehicleModel] (
    [Id] int NOT NULL IDENTITY,
    [Model] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [MakeId] int NOT NULL,
    CONSTRAINT [PK_VehicleModel] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [VehiclePart] (
    [Id] int NOT NULL IDENTITY,
    [Part] nvarchar(max) NULL,
    [VehiclePartName] nvarchar(max) NULL,
    [PartNumber] nvarchar(max) NULL,
    [VehiclePartDescription] nvarchar(max) NULL,
    [VehiclePartPrice] nvarchar(max) NULL,
    [VehiclePartQuantity] nvarchar(max) NULL,
    [VehiclePartImage] nvarchar(max) NULL,
    [VehiclePartCategory] nvarchar(max) NULL,
    [VehiclePartSubCategory] nvarchar(max) NULL,
    [VehiclePartMake] nvarchar(max) NULL,
    [VehiclePartModel] nvarchar(max) NULL,
    [VehiclePartModelDetail] nvarchar(max) NULL,
    [VehiclePartYear] nvarchar(max) NULL,
    [VehiclePartEngine] nvarchar(max) NULL,
    [VehiclePartEngineSize] nvarchar(max) NULL,
    [VehiclePartEngineModel] nvarchar(max) NULL,
    [VehiclePartEngineFuelType] nvarchar(max) NULL,
    [VehiclePartEngineCode] nvarchar(max) NULL,
    [VehiclePartEngineBhp] nvarchar(max) NULL,
    [VehiclePartEngineKW] nvarchar(max) NULL,
    [VehiclePartEngineCylinders] nvarchar(max) NULL,
    [VehiclePartEngineValves] nvarchar(max) NULL,
    [VehiclePartEngineManufactureDate] nvarchar(max) NULL,
    [VehiclePartComment] nvarchar(max) NULL,
    [VehiclePartStatus] nvarchar(max) NULL,
    [VehiclePartManufacturer] nvarchar(max) NULL,
    [VehiclePartLocation] nvarchar(max) NULL,
    [VehiclePartSupplier] nvarchar(max) NULL,
    [VehiclePartSupplierContact] nvarchar(max) NULL,
    [VehiclePartSupplierEmail] nvarchar(max) NULL,
    [VehiclePartSupplierPhone] nvarchar(max) NULL,
    [VehiclePartSupplierAddress] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_VehiclePart] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [VehicleYears] (
    [Id] int NOT NULL IDENTITY,
    [Years] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_VehicleYears] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [WorkItem] (
    [Id] int NOT NULL IDENTITY,
    [RepairInstructions] nvarchar(max) NULL,
    [GarageBusinessCustomerId] int NULL,
    [VehicleId] int NULL,
    [CustomerId] int NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkItem] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [WorkQuote] (
    [Id] int NOT NULL IDENTITY,
    [QuoteDate] datetime2 NULL,
    [GarageBusinessCustomerId] int NULL,
    [VehicleId] int NULL,
    [CustomerId] int NULL,
    [WorkRequest] nvarchar(max) NULL,
    [VehicleProblem] nvarchar(max) NULL,
    [InvoiceNumber] nvarchar(max) NULL,
    [EnvironmentCost] decimal(8,2) NULL,
    [Paint] decimal(8,2) NULL,
    [SundryExpenses] decimal(8,2) NULL,
    [CarHire] decimal(8,2) NULL,
    [SubTotal] decimal(8,2) NULL,
    [Vat] decimal(8,2) NULL,
    [Total] decimal(8,2) NULL,
    [Comment] nvarchar(max) NULL,
    [Labour] decimal(8,2) NULL,
    [Tax] decimal(8,2) NULL,
    [WorkQuoteDate] datetime2 NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkQuote] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [WorkQuoteWorkItem] (
    [Id] int NOT NULL IDENTITY,
    [GarageBusinessCustomerId] int NULL,
    [WorkQuoteId] int NULL,
    [WorkItemId] int NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkQuoteWorkItem] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [CustomerOwnedVehicles] (
    [Id] int NOT NULL IDENTITY,
    [GarageBusinessCustomerId] int NOT NULL,
    [VehicleId] int NOT NULL,
    [GarageBusinessId] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [Blocked] bit NOT NULL,
    CONSTRAINT [PK_CustomerOwnedVehicles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerOwnedVehicles_GarageBusiness_GarageBusinessId] FOREIGN KEY ([GarageBusinessId]) REFERENCES [GarageBusiness] ([Id])
);
GO


CREATE TABLE [GarageBusinessCustomer] (
    [Id] int NOT NULL IDENTITY,
    [GarageCustomerForename] nvarchar(max) NULL,
    [GarageCustomerSurname] nvarchar(max) NULL,
    [GarageCustomerAddressline1] nvarchar(max) NULL,
    [GarageCustomerAddressline2] nvarchar(max) NULL,
    [GarageCustomerAddressline3] nvarchar(max) NULL,
    [GarageCustomerAddressline4] nvarchar(max) NULL,
    [GarageCustomerPostcode] nvarchar(max) NULL,
    [GarageCustomerEmailAddress] nvarchar(max) NULL,
    [GarageCustomerPhoneNumber] nvarchar(max) NULL,
    [GarageCustomerMobileNumber] nvarchar(max) NULL,
    [GarageBusinessId] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [Blocked] bit NOT NULL,
    CONSTRAINT [PK_GarageBusinessCustomer] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GarageBusinessCustomer_GarageBusiness_GarageBusinessId] FOREIGN KEY ([GarageBusinessId]) REFERENCES [GarageBusiness] ([Id])
);
GO


CREATE TABLE [GarageOwnedVehicles] (
    [Id] int NOT NULL IDENTITY,
    [GarageVehicleOwnerId] int NOT NULL,
    [VehicleId] int NOT NULL,
    [GarageBusinessId] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [Blocked] bit NOT NULL,
    CONSTRAINT [PK_GarageOwnedVehicles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GarageOwnedVehicles_GarageBusiness_GarageBusinessId] FOREIGN KEY ([GarageBusinessId]) REFERENCES [GarageBusiness] ([Id])
);
GO


CREATE TABLE [GarageVehicleOwner] (
    [Id] int NOT NULL IDENTITY,
    [GarageVehicleOwnerName] nvarchar(max) NULL,
    [GarageBusinessId] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [Blocked] bit NOT NULL,
    CONSTRAINT [PK_GarageVehicleOwner] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GarageVehicleOwner_GarageBusiness_Id] FOREIGN KEY ([GarageBusinessId]) REFERENCES [GarageBusiness] ([Id])
);
GO


CREATE TABLE [GarageBusinessUser] (
    [Id] int NOT NULL IDENTITY,
    [GarageBusinessId] int NOT NULL,
    [UserId] int NOT NULL,
    [Role] nvarchar(50) NOT NULL,
    [IsOwner] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_GarageBusinessUser] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GarageBusinessUser_GarageBusiness] FOREIGN KEY ([GarageBusinessId]) REFERENCES [GarageBusiness] ([Id]),
    CONSTRAINT [FK_GarageBusinessUser_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO


CREATE TABLE [GarageUserInvitation] (
    [Id] int NOT NULL IDENTITY,
    [GarageBusinessId] int NOT NULL,
    [EmailAddress] nvarchar(256) NOT NULL,
    [FirstName] nvarchar(100) NULL,
    [LastName] nvarchar(100) NULL,
    [Role] nvarchar(50) NOT NULL,
    [InvitationTokenHash] nvarchar(64) NOT NULL,
    [ExpiresDate] datetime2 NOT NULL,
    [Accepted] bit NOT NULL,
    [AcceptedDate] datetime2 NULL,
    [InvitedByUserId] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [Revoked] bit NOT NULL,
    [RevokedDate] datetime NULL,
    CONSTRAINT [PK_GarageUserInvitation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GarageUserInvitation_GarageBusiness] FOREIGN KEY ([GarageBusinessId]) REFERENCES [GarageBusiness] ([Id]),
    CONSTRAINT [FK_GarageUserInvitation_InvitedBy] FOREIGN KEY ([InvitedByUserId]) REFERENCES [Users] ([Id])
);
GO


CREATE TABLE [CustomerVehicle] (
    [Id] int NOT NULL IDENTITY,
    [VehicleModelId] int NULL,
    [VehicleMakeId] int NULL,
    [VehicleYearId] int NULL,
    [VehicleMileageId] int NULL,
    [VehicleMileageDate] datetime2 NOT NULL,
    [VehicleRegistration] nvarchar(max) NULL,
    [VehicleTransmissionId] int NULL,
    [VehicleFuelTypeId] int NULL,
    [VehicleTaxDue] nvarchar(max) NULL,
    [VehicleNCTDue] nvarchar(max) NULL,
    [GarageBusinessId] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [Blocked] bit NOT NULL,
    [GarageOwned] bit NOT NULL DEFAULT ((CONVERT([bit],(0)))),
    CONSTRAINT [PK_CustomerVehicle] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerVehicle_GarageBusiness_Id] FOREIGN KEY ([GarageBusinessId]) REFERENCES [GarageBusiness] ([Id]),
    CONSTRAINT [FK_CustomerVehicle_VehicleMake_VehicleMakeId] FOREIGN KEY ([VehicleMakeId]) REFERENCES [VehicleMake] ([Id]),
    CONSTRAINT [FK_CustomerVehicle_VehicleModel_VehicleModelId] FOREIGN KEY ([VehicleModelId]) REFERENCES [VehicleModel] ([Id])
);
GO


CREATE TABLE [VehicleInvoice] (
    [Id] int NOT NULL IDENTITY,
    [VehicleId] int NULL,
    [CustomerId] int NULL,
    [InvoiceNumber] nvarchar(max) NULL,
    [GarageBusinessId] int NULL,
    [WorkQuoteId] int NULL,
    [EnvironmentCost] decimal(8,2) NULL,
    [Paint] decimal(8,2) NULL,
    [SundryExpenses] decimal(8,2) NULL,
    [CarHire] decimal(8,2) NULL,
    [SubTotal] decimal(8,2) NULL,
    [Vat] decimal(8,2) NULL,
    [Total] decimal(8,2) NULL,
    [DateDue] datetime2 NULL,
    [DatePaid] datetime2 NULL,
    [InvoiceDate] datetime2 NULL,
    [Paid] bit NULL,
    [PaidDate] datetime2 NULL,
    [Labour] decimal(8,2) NULL,
    [InvoiceAmount] nvarchar(max) NULL,
    [InvoiceDescription] nvarchar(max) NULL,
    [InvoiceImage] nvarchar(max) NULL,
    [InvoiceStatus] nvarchar(max) NULL,
    [InvoiceType] nvarchar(max) NULL,
    [Comment] nvarchar(max) NULL,
    [Tax] nvarchar(max) NULL,
    [strTotal] nvarchar(max) NULL,
    [GarageBusinessCustomerId] int NULL,
    [CreatedDate] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedDate] datetime2 NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_VehicleInvoice] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VehicleInvoice_CustomerVehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [CustomerVehicle] ([Id]),
    CONSTRAINT [FK_VehicleInvoice_GarageBusinessCustomer_GarageBusinessCustomerId] FOREIGN KEY ([GarageBusinessCustomerId]) REFERENCES [GarageBusinessCustomer] ([Id])
);
GO


CREATE INDEX [IX_CustomerOwnedVehicles_GarageBusinessId] ON [CustomerOwnedVehicles] ([GarageBusinessId]);
GO


CREATE INDEX [IX_CustomerVehicle_GarageBusinessId] ON [CustomerVehicle] ([GarageBusinessId]);
GO


CREATE INDEX [IX_CustomerVehicle_VehicleMakeId] ON [CustomerVehicle] ([VehicleMakeId]);
GO


CREATE INDEX [IX_CustomerVehicle_VehicleModelId] ON [CustomerVehicle] ([VehicleModelId]);
GO


CREATE INDEX [IX_GarageBusinessCustomer_GarageBusinessId] ON [GarageBusinessCustomer] ([GarageBusinessId]);
GO


CREATE INDEX [IX_GarageBusinessUser_UserId] ON [GarageBusinessUser] ([UserId]);
GO


CREATE UNIQUE INDEX [UX_GarageBusinessUser_Garage_User] ON [GarageBusinessUser] ([GarageBusinessId], [UserId]);
GO


CREATE INDEX [IX_GarageOwnedVehicles_GarageBusinessId] ON [GarageOwnedVehicles] ([GarageBusinessId]);
GO


CREATE INDEX [IX_GarageRegistrationRequest_ContactEmail] ON [GarageRegistrationRequest] ([ContactEmail]);
GO


CREATE INDEX [IX_GarageRegistrationRequest_Status] ON [GarageRegistrationRequest] ([Status]);
GO


CREATE INDEX [IX_GarageUserInvitation_GarageBusinessId] ON [GarageUserInvitation] ([GarageBusinessId]);
GO


CREATE INDEX [IX_GarageUserInvitation_InvitedByUserId] ON [GarageUserInvitation] ([InvitedByUserId]);
GO


CREATE UNIQUE INDEX [UX_GarageUserInvitation_TokenHash] ON [GarageUserInvitation] ([InvitationTokenHash]);
GO


CREATE INDEX [IX_GarageVehicleOwner_GarageBusinessId] ON [GarageVehicleOwner] ([GarageBusinessId]);
GO


CREATE UNIQUE INDEX [UX_Users_ExternalUserId] ON [Users] ([ExternalUserId]) WHERE [ExternalUserId] IS NOT NULL;
GO


CREATE INDEX [IX_VehicleInvoice_GarageBusinessCustomerId] ON [VehicleInvoice] ([GarageBusinessCustomerId]);
GO


CREATE INDEX [IX_VehicleInvoice_VehicleId] ON [VehicleInvoice] ([VehicleId]);
GO



COMMIT TRANSACTION;
