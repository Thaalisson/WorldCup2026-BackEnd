-- Week 3 migration: scoring config on Pools + GroupPredictions table

-- Scoring config columns on Pools (with defaults matching domain entity)
ALTER TABLE "Pools"
    ADD COLUMN IF NOT EXISTS "PointsExactScore"     INTEGER NOT NULL DEFAULT 10,
    ADD COLUMN IF NOT EXISTS "PointsCorrectResult"  INTEGER NOT NULL DEFAULT 5,
    ADD COLUMN IF NOT EXISTS "PointsChampion"       INTEGER NOT NULL DEFAULT 15,
    ADD COLUMN IF NOT EXISTS "PointsRunnerUp"       INTEGER NOT NULL DEFAULT 10,
    ADD COLUMN IF NOT EXISTS "PointsThirdPlace"     INTEGER NOT NULL DEFAULT 5,
    ADD COLUMN IF NOT EXISTS "PointsGroupQualifier" INTEGER NOT NULL DEFAULT 3;

-- Group predictions: which 2 teams the user thinks will qualify from each group
CREATE TABLE IF NOT EXISTS "GroupPredictions" (
    "Id"                 UUID     NOT NULL PRIMARY KEY,
    "PoolId"             UUID     NOT NULL,
    "UserId"             UUID     NOT NULL,
    "GroupName"          TEXT     NOT NULL,
    "FirstPlaceTeamId"   UUID     NOT NULL,
    "SecondPlaceTeamId"  UUID     NOT NULL,
    "PointsEarned"       INTEGER  NOT NULL DEFAULT 0,
    CONSTRAINT uq_grouppred UNIQUE ("PoolId", "UserId", "GroupName")
);

CREATE INDEX IF NOT EXISTS ix_grouppredictions_poolid_userid
    ON "GroupPredictions" ("PoolId", "UserId");
