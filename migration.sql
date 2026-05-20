CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE "ChampionPredictions" (
    "Id" uuid NOT NULL,
    "PoolId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ChampionTeamId" uuid NOT NULL,
    "RunnerUpTeamId" uuid,
    "ThirdPlaceTeamId" uuid,
    "TopScorerPlayerId" uuid,
    "MvpPlayerId" uuid,
    "PointsEarned" integer NOT NULL,
    CONSTRAINT "PK_ChampionPredictions" PRIMARY KEY ("Id")
);

CREATE TABLE "Matches" (
    "Id" uuid NOT NULL,
    "FifaMatchCode" text NOT NULL,
    "HomeTeamId" uuid NOT NULL,
    "AwayTeamId" uuid NOT NULL,
    "KickoffAt" timestamp with time zone NOT NULL,
    "Stage" integer NOT NULL,
    "GroupName" text,
    "HomeScore" integer,
    "AwayScore" integer,
    "IsFinished" boolean NOT NULL,
    "ApiFootballFixtureId" integer,
    CONSTRAINT "PK_Matches" PRIMARY KEY ("Id")
);

CREATE TABLE "Players" (
    "Id" uuid NOT NULL,
    "TeamId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Position" text NOT NULL,
    "Club" text,
    "Age" integer,
    "Goals" integer NOT NULL,
    "Assists" integer NOT NULL,
    CONSTRAINT "PK_Players" PRIMARY KEY ("Id")
);

CREATE TABLE "PoolParticipants" (
    "Id" uuid NOT NULL,
    "PoolId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TotalPoints" integer NOT NULL,
    "ExactScores" integer NOT NULL,
    "CorrectResults" integer NOT NULL,
    "JoinedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PoolParticipants" PRIMARY KEY ("Id")
);

CREATE TABLE "Pools" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "OwnerUserId" uuid NOT NULL,
    "IsPrivate" boolean NOT NULL,
    "InviteCode" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Pools" PRIMARY KEY ("Id")
);

CREATE TABLE "Predictions" (
    "Id" uuid NOT NULL,
    "PoolId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "MatchId" uuid NOT NULL,
    "HomeScorePrediction" integer NOT NULL,
    "AwayScorePrediction" integer NOT NULL,
    "PointsEarned" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_Predictions" PRIMARY KEY ("Id")
);

CREATE TABLE "Teams" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text NOT NULL,
    "IsoCode" text,
    "GroupName" text,
    "ApiFootballTeamId" integer,
    CONSTRAINT "PK_Teams" PRIMARY KEY ("Id")
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_Pools_InviteCode" ON "Pools" ("InviteCode");

CREATE UNIQUE INDEX "IX_Predictions_PoolId_UserId_MatchId" ON "Predictions" ("PoolId", "UserId", "MatchId");

CREATE UNIQUE INDEX "IX_Teams_Code" ON "Teams" ("Code");

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260519174205_InitialCreate', '8.0.11');

COMMIT;

