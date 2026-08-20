IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE TABLE [dbo].[Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [NormalizedName] nvarchar(256) NOT NULL,
        [ConcurrencyStamp] nvarchar(64) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LastLoginAtUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [UserName] nvarchar(256) NOT NULL,
        [NormalizedUserName] nvarchar(256) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(1024) NULL,
        [SecurityStamp] nvarchar(64) NULL,
        [ConcurrencyStamp] nvarchar(64) NULL,
        [PhoneNumber] nvarchar(32) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE TABLE [dbo].[RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(256) NULL,
        [ClaimValue] nvarchar(1024) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE TABLE [dbo].[UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(256) NULL,
        [ClaimValue] nvarchar(1024) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE TABLE [dbo].[UserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(256) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE TABLE [dbo].[UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE TABLE [dbo].[UserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(2048) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_RoleClaims_RoleId] ON [dbo].[RoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Roles_NormalizedName] ON [dbo].[Roles] ([NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [dbo].[UserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [dbo].[UserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [dbo].[UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_NormalizedEmail] ON [dbo].[Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_NormalizedUserName] ON [dbo].[Users] ([NormalizedUserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815195544_InitialIdentity'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815195544_InitialIdentity', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816105206_AddPermissionAuthorization'
)
BEGIN
    CREATE TABLE [dbo].[Permissions] (
        [Id] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816105206_AddPermissionAuthorization'
)
BEGIN
    CREATE TABLE [dbo].[RolePermissions] (
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] int NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816105206_AddPermissionAuthorization'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[Permissions]'))
        SET IDENTITY_INSERT [dbo].[Permissions] ON;
    EXEC(N'INSERT INTO [dbo].[Permissions] ([Id], [Name])
    VALUES (1, N''Dashboard.View''),
    (2, N''Client.View''),
    (3, N''Client.Create''),
    (4, N''Client.Edit''),
    (5, N''Client.Archive''),
    (6, N''ClientDocument.View''),
    (7, N''ClientDocument.Upload''),
    (8, N''ClientDocument.Download''),
    (9, N''ClientDocument.Replace''),
    (10, N''ClientDocument.Archive''),
    (11, N''Employee.View''),
    (12, N''Employee.Create''),
    (13, N''Employee.Edit''),
    (14, N''Employee.Archive''),
    (15, N''EmployeeDocument.View''),
    (16, N''EmployeeDocument.Upload''),
    (17, N''EmployeeDocument.Download''),
    (18, N''EmployeeDocument.Replace''),
    (19, N''EmployeeDocument.Archive''),
    (20, N''Training.View''),
    (21, N''Training.Create''),
    (22, N''Training.Edit''),
    (23, N''Training.Manage''),
    (24, N''TrainingDocument.View''),
    (25, N''TrainingDocument.Upload''),
    (26, N''BackgroundCheck.View''),
    (27, N''BackgroundCheck.Create''),
    (28, N''BackgroundCheck.Edit''),
    (29, N''BackgroundCheck.Manage''),
    (30, N''BackgroundCheckDocument.View''),
    (31, N''BackgroundCheckDocument.Upload''),
    (32, N''DHSLibrary.View''),
    (33, N''DHSLibrary.Create''),
    (34, N''DHSLibrary.Edit''),
    (35, N''DHSLibrary.PublishVersion''),
    (36, N''DHSLibrary.Archive''),
    (37, N''ExpiryReview.View''),
    (38, N''ExpiryReview.Manage''),
    (39, N''Notification.View''),
    (40, N''Notification.Manage''),
    (41, N''FinancialReport.View''),
    (42, N''User.View'');
    INSERT INTO [dbo].[Permissions] ([Id], [Name])
    VALUES (43, N''User.Manage''),
    (44, N''Role.View''),
    (45, N''Role.Manage''),
    (46, N''Permission.View''),
    (47, N''Permission.Manage''),
    (48, N''AuditLog.View'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[Permissions]'))
        SET IDENTITY_INSERT [dbo].[Permissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816105206_AddPermissionAuthorization'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Permissions_Name] ON [dbo].[Permissions] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816105206_AddPermissionAuthorization'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [dbo].[RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816105206_AddPermissionAuthorization'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260816105206_AddPermissionAuthorization', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816113531_AddAuditLogging'
)
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(128) NOT NULL,
        [ResourceType] nvarchar(64) NOT NULL,
        [ResourceId] nvarchar(128) NOT NULL,
        [Outcome] varchar(16) NOT NULL,
        [TraceId] char(32) NULL,
        [MetadataJson] nvarchar(2048) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AuditLogs_MetadataJson] CHECK ([MetadataJson] IS NULL OR ISJSON([MetadataJson]) = 1),
        CONSTRAINT [CK_AuditLogs_Outcome] CHECK ([Outcome] IN ('Succeeded', 'Failed')),
        CONSTRAINT [FK_AuditLogs_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816113531_AddAuditLogging'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorUserId_OccurredAtUtc] ON [dbo].[AuditLogs] ([ActorUserId], [OccurredAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816113531_AddAuditLogging'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OccurredAtUtc] ON [dbo].[AuditLogs] ([OccurredAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816113531_AddAuditLogging'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ResourceType_ResourceId_OccurredAtUtc] ON [dbo].[AuditLogs] ([ResourceType], [ResourceId], [OccurredAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816113531_AddAuditLogging'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AuditLogs_TraceId] ON [dbo].[AuditLogs] ([TraceId]) WHERE [TraceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260816113531_AddAuditLogging'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260816113531_AddAuditLogging', N'10.0.11');
END;

COMMIT;
GO

