-- Week 2 migration: FeedEvents + RankingSnapshots tables

CREATE TABLE IF NOT EXISTS "FeedEvents" (
    "Id"          UUID         NOT NULL PRIMARY KEY,
    "PoolId"      UUID         NOT NULL,
    "UserId"      UUID         NOT NULL,
    "MatchId"     UUID         NOT NULL,
    "UserName"    TEXT         NOT NULL,
    "MatchLabel"  TEXT         NOT NULL,
    "EventType"   INTEGER      NOT NULL,
    "Points"      INTEGER      NOT NULL,
    "OccurredAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_feedevents_poolid_occurredat
    ON "FeedEvents" ("PoolId", "OccurredAt" DESC);

CREATE TABLE IF NOT EXISTS "RankingSnapshots" (
    "Id"           UUID         NOT NULL PRIMARY KEY,
    "PoolId"       UUID         NOT NULL,
    "UserId"       UUID         NOT NULL,
    "Round"        TEXT         NOT NULL,
    "TotalPoints"  INTEGER      NOT NULL,
    "SnappedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_rankingsnapshots_poolid_userid
    ON "RankingSnapshots" ("PoolId", "UserId");
